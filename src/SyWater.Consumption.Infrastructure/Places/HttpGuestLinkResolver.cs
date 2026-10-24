using System.Net;
using System.Net.Http.Json;
using SyWater.Consumption.Application.Ports.Out;

namespace SyWater.Consumption.Infrastructure.Places;

/// <summary>
/// ms-places "GET /internal/guest-links/{token}" with X-Internal-Key. 200 = {"placeId", ...}; 404 = the link opens nothing.
/// Any other answer is "service unavailable" (HTTP 503): a guest is never let in because ms-places failed.
/// </summary>
public sealed class HttpGuestLinkResolver(HttpClient http, string internalKey) : IGuestLinkResolver
{
    public const string ServiceName = "place-service";
    public const string InternalKeyHeader = "X-Internal-Key";

    private sealed record GuestPlaceDto(Guid PlaceId);

    public async Task<Guid?> ResolvePlaceAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/guest-links/{Uri.EscapeDataString(token)}");
        request.Headers.Add(InternalKeyHeader, internalKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode != HttpStatusCode.OK)
                throw new ExternalServiceUnavailableException(ServiceName,
                    new HttpRequestException($"Unexpected status {(int)response.StatusCode}."));

            var dto = await response.Content.ReadFromJsonAsync<GuestPlaceDto>(ct)
                      ?? throw new ExternalServiceUnavailableException(ServiceName);
            return dto.PlaceId == Guid.Empty ? throw new ExternalServiceUnavailableException(ServiceName) : dto.PlaceId;
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
    }
}

/// <summary>Used while "Internal:ApiKey" is not set: no guest link opens anything.</summary>
public sealed class GuestLinksNotConfiguredResolver : IGuestLinkResolver
{
    public Task<Guid?> ResolvePlaceAsync(string token, CancellationToken ct) => Task.FromResult<Guid?>(null);
}
