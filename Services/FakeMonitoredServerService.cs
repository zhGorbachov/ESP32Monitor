namespace ESP32Monitor.Services;

/// <summary>
/// Simulated upstream server / uplink used only when ESP32 simulation mode is on.
/// Drives healthy vs outage phases; <see cref="IsHealthy"/> matches HTTP health endpoint semantics.
/// </summary>
public sealed class FakeMonitoredServerService
{
    private readonly object _sync = new();
    private readonly int _healthyDurationTicks;
    private readonly int _outageMinTicks;
    private readonly int _outageMaxTicks;
    private readonly Random _rng;

    public string Name { get; }

    private bool _isHealthy = true;
    private MonitoredPhase _phase = MonitoredPhase.Healthy;
    private int _ticksInPhase;
    private int _outageTargetTicks = 3;
    private DateTime _lastTransitionUtc = DateTime.UtcNow;

    public FakeMonitoredServerService(IConfiguration configuration, ILogger<FakeMonitoredServerService> logger)
    {
        var section = configuration.GetSection("MonitoredServerSimulation");
        Name = section["Name"] ?? "Simulated uplink";
        _healthyDurationTicks = section.GetValue("HealthyDurationTicks", 8);
        _outageMinTicks = section.GetValue("OutageMinTicks", 2);
        _outageMaxTicks = Math.Max(_outageMinTicks, section.GetValue("OutageMaxTicks", 5));
        var seed = section.GetValue<int?>("RandomSeed");
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();

        logger.LogInformation(
            "FakeMonitoredServerService: {Name}, healthyTicks={H}, outageTicks={Min}-{Max}",
            Name, _healthyDurationTicks, _outageMinTicks, _outageMaxTicks);
    }

    public bool IsHealthy
    {
        get { lock (_sync) return _isHealthy; }
    }

    public MonitoredPhase Phase
    {
        get { lock (_sync) return _phase; }
    }

    public DateTime LastTransitionUtc
    {
        get { lock (_sync) return _lastTransitionUtc; }
    }

    public int TicksInCurrentPhase
    {
        get { lock (_sync) return _ticksInPhase; }
    }

    /// <summary>Advance one poll cycle; call once per simulation tick.</summary>
    public void Advance()
    {
        lock (_sync)
        {
            _ticksInPhase++;

            if (_phase == MonitoredPhase.Healthy)
            {
                if (_ticksInPhase >= _healthyDurationTicks)
                    TransitionTo(MonitoredPhase.Outage);
            }
            else
            {
                if (_ticksInPhase >= _outageTargetTicks)
                    TransitionTo(MonitoredPhase.Healthy);
            }
        }
    }

    private void TransitionTo(MonitoredPhase next)
    {
        _phase = next;
        _ticksInPhase = 0;
        _lastTransitionUtc = DateTime.UtcNow;
        _isHealthy = next == MonitoredPhase.Healthy;

        if (next == MonitoredPhase.Outage)
            _outageTargetTicks = _rng.Next(_outageMinTicks, _outageMaxTicks + 1);
    }
}

public enum MonitoredPhase
{
    Healthy,
    Outage
}
