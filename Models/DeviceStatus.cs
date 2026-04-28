using System.Text.Json.Serialization;

namespace ESP32Monitor.Models;

public class DeviceStatus
{
    [JsonPropertyName("wifi_connected")]
    public bool WifiConnected { get; set; }

    [JsonPropertyName("ssid")]
    public string Ssid { get; set; } = string.Empty;

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonPropertyName("internet")]
    public bool Internet { get; set; }

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = string.Empty;

    /// <summary>Populated by the polling service, not from ESP32.</summary>
    [JsonIgnore]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
