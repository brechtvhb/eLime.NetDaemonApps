using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPumpHttpClient : ISmartHeatPumpHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _baseUrl;

    public SmartHeatPumpHttpClient(string baseUrl, ILogger logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<bool> SetMaximumHotWaterTemperature(decimal temperature)
    {
        try
        {
            var data = new[]
            {
                new
                {
                    name = "val60312",
                    value = temperature.ToString("F1", CultureInfo.InvariantCulture)
                }
            };

            var jsonData = JsonSerializer.Serialize(data);

            var formContent = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("data", jsonData)
            ]);

            var url = $"{_baseUrl}/save.php";
            _logger.LogDebug("Sending maximum hot water temperature {Temperature}°C to heat pump at {Url}", temperature, url);

            var response = await _httpClient.PostAsync(url, formContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully set maximum hot water temperature to {Temperature}°C", temperature);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to set maximum hot water temperature. Status code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting maximum hot water temperature to {Temperature}°C", temperature);
            return false;
        }
    }

    public async Task<decimal?> GetMaximumHotWaterTemperature()
    {
        try
        {
            var url = $"{_baseUrl}/?s=4,3";
            _logger.LogTrace("Fetching maximum hot water temperature from heat pump at {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch maximum hot water temperature. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();

            const string pattern = @"jsvalues\['60312'\]\['val'\]='([\d,]+)';";
            var match = Regex.Match(html, pattern);

            if (!match.Success)
            {
                _logger.LogWarning("Could not find maximum hot water temperature in response HTML");
                return null;
            }

            var rawValue = match.Groups[1].Value;   // e.g., "54,0"
            var normalizedValue = rawValue.Replace(',', '.');  // "54.0"

            if (decimal.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal temperature))
            {
                _logger.LogTrace("Successfully fetched maximum hot water temperature: {Temperature}°C", temperature);
                return temperature;
            }

            _logger.LogWarning("Failed to parse temperature value: {Value}", normalizedValue);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching maximum hot water temperature");
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
