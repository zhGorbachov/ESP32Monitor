using ESP32Monitor.Data;
using ESP32Monitor.Models;

namespace ESP32Monitor.Services;

public class PollingService(
    IServiceScopeFactory scopeFactory,
    Esp32Client esp32Client,
    DeviceStateHolder stateHolder,
    FakeMonitoredServerService fakeMonitoredServer,
    IConfiguration configuration,
    ILogger<PollingService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(
        configuration.GetValue<int>("Esp32:PollingIntervalMs", 5000));
    private readonly bool _simulation =
        configuration.GetValue<bool>("Esp32:SimulationMode", false);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stateHolder.IsSimulationMode = _simulation;
        if (_simulation)
            stateHolder.AutoLedMonitoring = true;

        if (_simulation)
            logger.LogWarning("PollingService: SIMULATION MODE — dashboard upstream + LED colours follow MonitoredServer scenario (no ESP32 HTTP).");
        else
            logger.LogInformation(
                "PollingService: real ESP32 polling. MonitoredServer scenario={Scenario} drives /api/monitored-service/health only — set ESP32 check URL to your PC for LEDs to match.",
                fakeMonitoredServer.Scenario);

        while (!stoppingToken.IsCancellationRequested)
        {
            fakeMonitoredServer.Advance();

            if (_simulation)
                await SimulateOnceAsync(stoppingToken);
            else
                await PollOnceAsync(stoppingToken);

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var newStatus = await esp32Client.GetStatusAsync(ct);

        if (newStatus == null)
        {
            stateHolder.IsDeviceReachable = false;
            return;
        }

        stateHolder.IsDeviceReachable = true;
        await ApplyAndLogAsync(stateHolder.GetStatus(), newStatus, ct);
    }

    private async Task SimulateOnceAsync(CancellationToken ct)
    {
        stateHolder.IsDeviceReachable = true;

        var prev = stateHolder.GetStatus();
        var healthy = fakeMonitoredServer.IsHealthy;

        var next = new DeviceStatus
        {
            WifiConnected = true,
            Ssid          = "Sim-LAN",
            Ip            = "192.168.1.55",
            Internet      = healthy,
            LastUpdated   = DateTime.UtcNow
        };

        if (stateHolder.AutoLedMonitoring)
            next.Effect = healthy ? "breathe_green" : "blink_red";
        else
            next.Effect = prev.Effect;

        await ApplyAndLogAsync(prev, next, ct);
    }

    private async Task ApplyAndLogAsync(DeviceStatus prev, DeviceStatus next, CancellationToken ct)
    {
        stateHolder.SetStatus(next);

        var changes = DetectChanges(prev, next);
        if (changes.Count == 0) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ParameterLogs.AddRange(changes);
        await db.SaveChangesAsync(ct);

        foreach (var c in changes)
            logger.LogInformation("[{Mode}] {Param}: {Old} → {New}",
                _simulation ? "SIM" : "Poll", c.ParameterName, c.OldValue, c.NewValue);
    }

    private static List<ParameterLog> DetectChanges(DeviceStatus prev, DeviceStatus next)
    {
        var logs = new List<ParameterLog>();
        var now  = DateTime.UtcNow;

        Check("wifi_connected", prev.WifiConnected.ToString(), next.WifiConnected.ToString());
        Check("internet",       prev.Internet.ToString(),      next.Internet.ToString());
        Check("effect",         prev.Effect,                   next.Effect);
        Check("ssid",           prev.Ssid,                     next.Ssid);
        Check("ip",             prev.Ip,                       next.Ip);

        return logs;

        void Check(string name, string? oldVal, string? newVal)
        {
            if (oldVal != newVal)
                logs.Add(new ParameterLog
                {
                    Timestamp     = now,
                    ParameterName = name,
                    OldValue      = oldVal,
                    NewValue      = newVal,
                    Source        = "auto"
                });
        }
    }
}
