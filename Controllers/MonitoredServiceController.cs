using ESP32Monitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace ESP32Monitor.Controllers;

[ApiController]
[Route("api/monitored-service")]
public class MonitoredServiceController(FakeMonitoredServerService fake) : ControllerBase
{
    /// <summary>
    /// Same logical health as the simulated monitored server (simulation mode only advances state).
    /// Returns 204 when healthy, 503 during simulated outage — useful for curl/demo or future ESP32 URL.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        if (fake.IsHealthy)
            return NoContent(); // 204

        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
