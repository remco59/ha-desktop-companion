using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.HomeAssistant.Rest;

public sealed class HomeAssistantRestClient(HttpClient httpClient) : IHomeAssistantClient
{
    public async Task<bool> CheckConnectionAsync(
        Uri baseUri,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, new Uri(EnsureTrailingSlash(baseUri), "api/"), accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<HomeAssistantEntity>> GetEntitiesAsync(
        Uri baseUri,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, new Uri(EnsureTrailingSlash(baseUri), "api/states"), accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var entities = new List<HomeAssistantEntity>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var entityId = item.TryGetProperty("entity_id", out var id) ? id.GetString() : null;
            var state = item.TryGetProperty("state", out var stateValue) ? stateValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(entityId) || state is null)
            {
                continue;
            }

            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (item.TryGetProperty("attributes", out var attributeElement) &&
                attributeElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in attributeElement.EnumerateObject())
                {
                    attributes[property.Name] = property.Value.Clone();
                }
            }

            var friendlyName = attributes.TryGetValue("friendly_name", out var friendly)
                ? friendly?.ToString()
                : null;

            var separator = entityId.IndexOf('.');
            var domain = separator > 0 ? entityId[..separator] : null;

            entities.Add(new HomeAssistantEntity(entityId, state, friendlyName, domain, attributes));
        }

        return entities;
    }

    public async Task CallServiceAsync(
        Uri baseUri,
        string accessToken,
        string domain,
        string service,
        IReadOnlyDictionary<string, object?> serviceData,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(service))
        {
            throw new ArgumentException("A Home Assistant domain and service are required.");
        }

        var endpoint = new Uri(EnsureTrailingSlash(baseUri), $"api/services/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(service)}");
        using var request = CreateRequest(HttpMethod.Post, endpoint, accessToken);
        request.Content = JsonContent.Create(serviceData);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : uri.AbsoluteUri + "/";
        return new Uri(value, UriKind.Absolute);
    }
}
