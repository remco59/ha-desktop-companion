using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DesktopCompanion.HomeAssistant.Authentication;

/// <summary>
/// Implements Home Assistant's native-app OAuth authorization-code flow with PKCE.
/// A temporary IPv4 loopback listener receives the redirect; no fixed port or URL ACL is required.
/// </summary>
public sealed class HomeAssistantOAuthClient : IDisposable
{
    private const string CallbackPath = "/oauth/callback";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HomeAssistantOAuthClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsHttpClient = httpClient is null;
    }

    public async Task<HomeAssistantOAuthTokens> AuthorizeAsync(
        Uri homeAssistantUri,
        CancellationToken cancellationToken = default)
    {
        ValidateHomeAssistantUri(homeAssistantUri);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var clientId = new Uri($"http://127.0.0.1:{endpoint.Port}/");
            var redirectUri = new Uri(clientId, CallbackPath.TrimStart('/'));
            var state = CreateRandomToken(32);
            var verifier = CreateRandomToken(32);
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

            var authorizeUri = AddQuery(
                BuildEndpoint(homeAssistantUri, "auth/authorize"),
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId.AbsoluteUri,
                    ["redirect_uri"] = redirectUri.AbsoluteUri,
                    ["response_type"] = "code",
                    ["state"] = state,
                    ["code_challenge"] = challenge,
                    ["code_challenge_method"] = "S256"
                });

            Process.Start(new ProcessStartInfo(authorizeUri.AbsoluteUri) { UseShellExecute = true });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            var callback = await ReceiveCallbackAsync(listener, timeout.Token).ConfigureAwait(false);

            var receivedState = Encoding.UTF8.GetBytes(callback.State ?? string.Empty);
            var expectedState = Encoding.UTF8.GetBytes(state);
            if (receivedState.Length != expectedState.Length ||
                !CryptographicOperations.FixedTimeEquals(receivedState, expectedState))
            {
                throw new UnauthorizedAccessException("The Home Assistant OAuth response did not match the sign-in request.");
            }

            if (!string.IsNullOrWhiteSpace(callback.Error))
            {
                throw new InvalidOperationException($"Home Assistant authorization failed: {callback.Error}");
            }

            if (string.IsNullOrWhiteSpace(callback.Code))
            {
                throw new InvalidOperationException("Home Assistant did not return an authorization code.");
            }

            var tokens = await ExchangeAuthorizationCodeAsync(
                homeAssistantUri, clientId, callback.Code, verifier, cancellationToken).ConfigureAwait(false);
            tokens.ClientId = clientId.AbsoluteUri;
            return tokens;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task<HomeAssistantOAuthTokens> RefreshAsync(
        Uri homeAssistantUri,
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ValidateHomeAssistantUri(homeAssistantUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        using var response = await _httpClient.PostAsync(
            BuildEndpoint(homeAssistantUri, "auth/token"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId
            }),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<HomeAssistantOAuthTokens>(
                   cancellationToken: cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Home Assistant returned an empty token response.");
    }

    private async Task<HomeAssistantOAuthTokens> ExchangeAuthorizationCodeAsync(
        Uri homeAssistantUri, Uri clientId, string code, string verifier, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            BuildEndpoint(homeAssistantUri, "auth/token"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId.AbsoluteUri,
                ["code_verifier"] = verifier
            }),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var tokens = await response.Content.ReadFromJsonAsync<HomeAssistantOAuthTokens>(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (tokens is null ||
            string.IsNullOrWhiteSpace(tokens.AccessToken) ||
            string.IsNullOrWhiteSpace(tokens.RefreshToken) ||
            !string.Equals(tokens.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Home Assistant returned an incomplete OAuth token response.");
        }

        return tokens;
    }

    private static async Task<OAuthCallback> ReceiveCallbackAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            throw new InvalidOperationException("The OAuth callback request was empty.");
        }

        var headerBytes = 0;
        string? line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            headerBytes += line?.Length ?? 0;
            if (headerBytes > 16 * 1024)
            {
                throw new InvalidOperationException("The OAuth callback request headers were too large.");
            }
        } while (!string.IsNullOrEmpty(line));

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !string.Equals(parts[0], "GET", StringComparison.Ordinal) ||
            !Uri.TryCreate(new Uri("http://127.0.0.1/"), parts[1], out var requestUri) ||
            !string.Equals(requestUri.AbsolutePath, CallbackPath, StringComparison.Ordinal))
        {
            await WriteCallbackResponseAsync(stream, false, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("The OAuth callback did not target the expected path.");
        }

        var values = ParseQuery(requestUri.Query);
        var callback = new OAuthCallback(
            values.GetValueOrDefault("code"),
            values.GetValueOrDefault("state"),
            values.GetValueOrDefault("error"));

        await WriteCallbackResponseAsync(stream, true, cancellationToken).ConfigureAwait(false);
        return callback;
    }

    private static async Task WriteCallbackResponseAsync(NetworkStream stream, bool success, CancellationToken cancellationToken)
    {
        var body = success
            ? "<!doctype html><html><head><meta charset=\"utf-8\"><title>Desktop Companion</title></head><body><h2>Home Assistant connected</h2><p>You can close this tab and return to Desktop Companion.</p></body></html>"
            : "<!doctype html><html><body><h2>Sign-in could not be completed</h2><p>Return to Desktop Companion and try again.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(success ? "200 OK" : "400 Bad Request")}\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            var value = separator < 0 ? string.Empty : part[(separator + 1)..];
            values[WebUtility.UrlDecode(key)] = WebUtility.UrlDecode(value);
        }
        return values;
    }

    private static Uri AddQuery(Uri endpoint, IReadOnlyDictionary<string, string> values)
    {
        var query = string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new UriBuilder(endpoint) { Query = query }.Uri;
    }

    private static Uri BuildEndpoint(Uri baseUri, string relativePath) =>
        new($"{baseUri.AbsoluteUri.TrimEnd('/')}/{relativePath}", UriKind.Absolute);

    private static string CreateRandomToken(int byteCount) => Base64Url(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void ValidateHomeAssistantUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The Home Assistant URL must be an absolute HTTP or HTTPS URL.", nameof(uri));
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "Home Assistant denied access. Check that the account is allowed to sign in.",
            HttpStatusCode.BadRequest => "Home Assistant rejected the OAuth request. Check the instance URL and retry.",
            _ => $"Home Assistant authentication failed with HTTP {(int)response.StatusCode}."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private sealed record OAuthCallback(string? Code, string? State, string? Error);
}
