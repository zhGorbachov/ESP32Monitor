using ESP32Monitor.Data;
using ESP32Monitor.Models;
using Microsoft.EntityFrameworkCore;

namespace ESP32Monitor.Services;

public class PollingService(
    IServiceScopeFactory scopeFactory,
    Esp32Client esp32Client,
    DeviceStateHolder stateHolder,
    IConfiguration configuration,
    ILogger<PollingService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(
        configuration.GetValue<int>("Esp32:PollingIntervalMs", 5000));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PollingService started. Interval: {Interval}ms", _interval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
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
        var previous = stateHolder.GetStatus();
        stateHolder.SetStatus(newStatus);

        var changes = DetectChanges(previous, newStatus);
        if (changes.Count == 0)
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ParameterLogs.AddRange(changes);
        await db.SaveChangesAsync(ct);

        foreach (var c in changes)
            logger.LogInformation("[Poll] {Param}: {Old} → {New}", c.ParameterName, c.OldValue, c.NewValue);
    }

    private static List<ParameterLog> DetectChanges(DeviceStatus prev, DeviceStatus next)
    {
        var logs = new List<ParameterLog>();
        var now = DateTime.UtcNow;

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
