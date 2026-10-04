using System.Net;
using System.Net.Http.Headers;
using SyWater.Consumption.Application.Ports.Out;

namespace SyWater.Consumption.Infrastructure.Places;

public interface IAccessTokenProvider
{
    string? GetAccessToken();
}

public sealed class HttpPlaceAccessChecker(HttpClient http, IAccessTokenProvider tokens) : IPlaceAccessChecker
{
    public const string ServiceName = "place-service";

    public async Task<bool> CanReadAsync(Guid placeId, CancellationToken ct)
    {
        var token = tokens.GetAccessToken()
                    ?? throw new InvalidOperationException("There is no access token in the current request.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/places/{placeId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => true,
                HttpStatusCode.NotFound => false,
                _ => throw new ExternalServiceUnavailableException(ServiceName,
                    new HttpRequestException($"Unexpected status {(int)response.StatusCode}.")),
            };
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
    }
}
