using ESP32Monitor.Data;
using ESP32Monitor.Models;
using ESP32Monitor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ESP32Monitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController(DeviceStateHolder stateHolder, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetStatus()
    {
        var status = stateHolder.GetStatus();
        return Ok(new
        {
            status.WifiConnected,
            status.Ssid,
            status.Ip,
            status.Internet,
            status.Effect,
            status.LastUpdated,
            DeviceReachable = stateHolder.IsDeviceReachable
        });
    }

    [HttpGet("logs")]
    public async Task<ActionResult<object>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? parameter = null,
        [FromQuery] string? source = null)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var query = db.ParameterLogs.AsNoTracking();

        if (!string.IsNullOrEmpty(parameter))
            query = query.Where(l => l.ParameterName == parameter);

        if (!string.IsNullOrEmpty(source))
            query = query.Where(l => l.Source == source);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}
