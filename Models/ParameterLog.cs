namespace ESP32Monitor.Models;

public class ParameterLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Name of the parameter that changed (e.g. "internet", "effect", "wifi_connected").</summary>
    public string ParameterName { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>"auto" for polling-detected changes, "user" for API-triggered commands.</summary>
    public string Source { get; set; } = "auto";
}
