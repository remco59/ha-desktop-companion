using DesktopCompanion.Core.Abstractions;

namespace DesktopCompanion.HomeAssistant.Authentication;

/// <summary>
/// Owns the short-lived access token in memory and persists only the refresh token
/// through the injected operating-system credential store.
/// </summary>
public sealed class HomeAssistantTokenProvider : IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);

    private readonly Uri _homeAssistantUri;
    private readonly string _credentialKey;
    private readonly ISecretStore _secretStore;
    private readonly HomeAssistantOAuthClient _oauthClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;
    private bool _disposed;

    public HomeAssistantTokenProvider(
        Uri homeAssistantUri,
        ISecretStore secretStore,
        HomeAssistantOAuthClient? oauthClient = null)
    {
        ArgumentNullException.ThrowIfNull(homeAssistantUri);
        _homeAssistantUri = homeAssistantUri;
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _oauthClient = oauthClient ?? new HomeAssistantOAuthClient();
        _credentialKey = CreateCredentialKey(homeAssistantUri);
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tokens = await _oauthClient.AuthorizeAsync(_homeAssistantUri, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            throw new InvalidOperationException("Home Assistant did not return a refresh token.");
        }

        _secretStore.Write(_credentialKey, tokens.RefreshToken);
        SetAccessToken(tokens);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!string.IsNullOrWhiteSpace(_accessToken) &&
            DateTimeOffset.UtcNow < _accessTokenExpiresAt - RefreshSkew)
        {
            return _accessToken;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) &&
                DateTimeOffset.UtcNow < _accessTokenExpiresAt - RefreshSkew)
            {
                return _accessToken;
            }

            var refreshToken = _secretStore.Read(_credentialKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new UnauthorizedAccessException("No Home Assistant account is connected.");
            }

            HomeAssistantOAuthTokens tokens;
            try
            {
                tokens = await _oauthClient.RefreshAsync(
                    _homeAssistantUri,
                    refreshToken,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Forbidden)
            {
                _secretStore.Delete(_credentialKey);
                _accessToken = null;
                _accessTokenExpiresAt = DateTimeOffset.MinValue;
                throw new UnauthorizedAccessException(
                    "The saved Home Assistant sign-in has expired. Connect the account again.",
                    exception);
            }

            if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                _secretStore.Write(_credentialKey, tokens.RefreshToken);
            }

            SetAccessToken(tokens);
            return _accessToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Disconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _secretStore.Delete(_credentialKey);
        _accessToken = null;
        _accessTokenExpiresAt = DateTimeOffset.MinValue;
    }

    private void SetAccessToken(HomeAssistantOAuthTokens tokens)
    {
        if (string.IsNullOrWhiteSpace(tokens.AccessToken) || tokens.ExpiresIn <= 0)
        {
            throw new InvalidOperationException("Home Assistant returned an invalid access token.");
        }

        _accessToken = tokens.AccessToken;
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
    }

    private static string CreateCredentialKey(Uri uri)
    {
        var normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/').ToLowerInvariant() +
                         uri.AbsolutePath.TrimEnd('/');
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(normalized));
        return $"DesktopCompanion/HomeAssistant/{Convert.ToHexString(hash)}";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshLock.Dispose();
        _oauthClient.Dispose();
    }
}
