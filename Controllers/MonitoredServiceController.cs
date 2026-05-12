using ESP32Monitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace ESP32Monitor.Controllers;

[ApiController]
[Route("api/monitored-service")]
public class MonitoredServiceController(FakeMonitoredServerService fake) : ControllerBase
{
    /// <summary>
    /// Returns 204 when healthy, 503 when not — same rules as the ESP32 Google <c>generate_204</c> check.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        if (fake.IsHealthy)
            return NoContent(); // 204

        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
