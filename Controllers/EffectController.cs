using ESP32Monitor.Data;
using ESP32Monitor.Models;
using ESP32Monitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace ESP32Monitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EffectController(Esp32Client esp32Client, AppDbContext db, DeviceStateHolder stateHolder) : ControllerBase
{
    public record SetEffectRequest(string Effect, string? Color = null, string? SnakeColor = null);

    [HttpPost]
    public async Task<IActionResult> SetEffect([FromBody] SetEffectRequest request, CancellationToken ct)
    {
        var previous = stateHolder.GetStatus();
        var success = await esp32Client.SetEffectAsync(request.Effect, request.Color, request.SnakeColor, ct);
        if (!success)
            return StatusCode(503, "ESP32 device is unreachable.");

        await LogChangeAsync("effect", previous.Effect, request.Effect, "user", ct);

        if (!string.IsNullOrEmpty(request.Color))
            await LogChangeAsync("static_color", null, request.Color, "user", ct);

        if (!string.IsNullOrEmpty(request.SnakeColor))
            await LogChangeAsync("snake_color", null, request.SnakeColor, "user", ct);

        return Ok(new { message = $"Effect set to '{request.Effect}'." });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        var success = await esp32Client.ResetAsync(ct);
        if (!success)
            return StatusCode(503, "ESP32 device is unreachable.");

        await LogChangeAsync("device_mode", "monitoring", "factory_reset", "user", ct);
        return Ok(new { message = "Factory reset initiated." });
    }

    [HttpPost("monitoring")]
    public async Task<IActionResult> ReturnToMonitoring(CancellationToken ct)
    {
        var previous = stateHolder.GetStatus();
        var success = await esp32Client.ReturnToMonitoringAsync(ct);
        if (!success)
            return StatusCode(503, "ESP32 device is unreachable.");

        await LogChangeAsync("effect", previous.Effect, "monitoring", "user", ct);
        return Ok(new { message = "Returned to monitoring mode." });
    }

    private async Task LogChangeAsync(string paramName, string? oldVal, string? newVal, string source, CancellationToken ct)
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
