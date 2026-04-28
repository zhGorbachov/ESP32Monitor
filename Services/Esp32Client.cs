using ESP32Monitor.Models;
using System.Text.Json;

namespace ESP32Monitor.Services;

/// <summary>
/// Wraps all HTTP communication with the ESP32 device.
/// Uses a named HttpClient ("esp32") so it is safe to inject into singletons.
/// </summary>
public class Esp32Client(IHttpClientFactory factory, ILogger<Esp32Client> logger)
{
    private const string ClientName = "esp32";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient Create() => factory.CreateClient(ClientName);

    public async Task<DeviceStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await Create().GetAsync("/status", ct);
            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadFromJsonAsync<DeviceStatus>(JsonOptions, ct);
            if (status != null)
                status.LastUpdated = DateTime.UtcNow;
            return status;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reach ESP32");
            return null;
        }
    }

    public async Task<bool> SetEffectAsync(string effect, string? color = null, string? snakeColor = null, CancellationToken ct = default)
    {
        try
        {
            var body = $"effect={Uri.EscapeDataString(effect)}";
            if (!string.IsNullOrEmpty(color))
                body += $"&color={Uri.EscapeDataString(color)}";
            if (!string.IsNullOrEmpty(snakeColor))
                body += $"&snakeColor={Uri.EscapeDataString(snakeColor)}";

            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await Create().PostAsync("/effect", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set effect on ESP32");
            return false;
        }
    }

    public async Task<bool> ResetAsync(CancellationToken ct = default)
    {
        try
        {
            return (await Create().PostAsync("/reset", null, ct)).IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send reset to ESP32");
            return false;
        }
    }

    public async Task<bool> ReturnToMonitoringAsync(CancellationToken ct = default)
    {
        try
        {
            return (await Create().PostAsync("/monitoring", null, ct)).IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send return-to-monitoring to ESP32");
            return false;
        }
    }
}
