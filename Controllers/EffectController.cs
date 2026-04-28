using ESP32Monitor.Data;
using ESP32Monitor.Models;
using ESP32Monitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace ESP32Monitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EffectController(
    Esp32Client esp32Client,
    AppDbContext db,
    DeviceStateHolder stateHolder) : ControllerBase
{
    public record SetEffectRequest(string Effect, string? Color = null, string? SnakeColor = null);

    [HttpPost]
    public async Task<IActionResult> SetEffect([FromBody] SetEffectRequest request, CancellationToken ct)
    {
        var previous = stateHolder.GetStatus();

        if (!stateHolder.IsSimulationMode)
        {
            var success = await esp32Client.SetEffectAsync(request.Effect, request.Color, request.SnakeColor, ct);
            if (!success)
                return StatusCode(503, "ESP32 device is unreachable.");
        }

        // Update in-memory state so Dashboard reflects the change immediately
        var updated = stateHolder.GetStatus();
        updated.Effect = request.Effect;
        stateHolder.SetStatus(updated);

        await LogAsync("effect", previous.Effect, request.Effect, "user", ct);

        if (!string.IsNullOrEmpty(request.Color))
            await LogAsync("static_color", null, request.Color, "user", ct);

        if (!string.IsNullOrEmpty(request.SnakeColor))
            await LogAsync("snake_color", null, request.SnakeColor, "user", ct);

        var mode = stateHolder.IsSimulationMode ? " (simulation)" : "";
        return Ok(new { message = $"Effect set to '{request.Effect}'{mode}." });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        if (!stateHolder.IsSimulationMode)
        {
            var success = await esp32Client.ResetAsync(ct);
            if (!success)
                return StatusCode(503, "ESP32 device is unreachable.");
        }

        await LogAsync("device_mode", "monitoring", "factory_reset", "user", ct);
        var mode = stateHolder.IsSimulationMode ? " (simulation)" : "";
        return Ok(new { message = $"Factory reset initiated{mode}." });
    }

    [HttpPost("monitoring")]
    public async Task<IActionResult> ReturnToMonitoring(CancellationToken ct)
    {
        var previous = stateHolder.GetStatus();

        if (!stateHolder.IsSimulationMode)
        {
            var success = await esp32Client.ReturnToMonitoringAsync(ct);
            if (!success)
                return StatusCode(503, "ESP32 device is unreachable.");
        }

        var updated = stateHolder.GetStatus();
        updated.Effect = "monitoring";
        stateHolder.SetStatus(updated);

        await LogAsync("effect", previous.Effect, "monitoring", "user", ct);
        var mode = stateHolder.IsSimulationMode ? " (simulation)" : "";
        return Ok(new { message = $"Returned to monitoring mode{mode}." });
    }

    private async Task LogAsync(string paramName, string? oldVal, string? newVal, string source, CancellationToken ct)
    {
        db.ParameterLogs.Add(new ParameterLog
        {
            Timestamp     = DateTime.UtcNow,
            ParameterName = paramName,
            OldValue      = oldVal,
            NewValue      = newVal,
            Source        = source
        });
        await db.SaveChangesAsync(ct);
    }
}
