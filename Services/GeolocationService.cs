using System.Text.Json;

namespace Ticketing.Api.Services;

public interface IGeolocationService
{
    Task<(string? Country, string? City)> GetLocationFromIpAsync(string ipAddress);
}

public class GeolocationService : IGeolocationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeolocationService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public GeolocationService(HttpClient httpClient, ILogger<GeolocationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(string? Country, string? City)> GetLocationFromIpAsync(string ipAddress)
    {
        // Skip localhost/private IPs
        if (string.IsNullOrEmpty(ipAddress) || 
            ipAddress == "Unknown" || 
            ipAddress == "::1" || 
            ipAddress.StartsWith("127.", StringComparison.Ordinal) || 
            ipAddress.StartsWith("192.168.", StringComparison.Ordinal) ||
            ipAddress.StartsWith("10.", StringComparison.Ordinal))
        {
            _logger.LogDebug("Skipping geolocation for local IP: {IpAddress}", ipAddress);
            return (null, null);
        }

        try
        {
            var uri = new Uri($"http://ip-api.com/json/{ipAddress}?fields=status,country,city");
            var response = await _httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Geolocation API returned status: {StatusCode}", response.StatusCode);
                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IpApiResponse>(json, _jsonOptions);

            if (data?.Status == "success" && !string.IsNullOrEmpty(data.Country))
            {
                _logger.LogInformation("Location detected for IP {IpAddress}: {City}, {Country}", 
                    ipAddress, data.City ?? "Unknown", data.Country);
                return (data.Country, data.City);
            }

            _logger.LogWarning("Geolocation failed for IP {IpAddress}: {Status}", ipAddress, data?.Status);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location for IP {IpAddress}: {ErrorMessage}", ipAddress, ex.Message);
            return (null, null);
        }
    }

    private class IpApiResponse
    {
        public string? Status { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
    }
}
