namespace ESP32Monitor.Services;

/// <summary>
/// Configurable "fake" upstream health used by <c>GET /api/monitored-service/health</c> (204 vs 503).
/// Point the ESP32 internet check at this URL so the LED strip reflects the chosen scenario.
/// </summary>
public sealed class FakeMonitoredServerService
{
    private readonly object _sync = new();
    private readonly int _healthyDurationTicks;
    private readonly int _outageMinTicks;
    private readonly int _outageMaxTicks;
    private readonly Random _rng;

    public string Name { get; }
    public MonitoredHealthScenario Scenario { get; }

    private bool _isHealthy;
    private MonitoredPhase _phase;
    private int _ticksInPhase;
    private int _outageTargetTicks = 3;
    private DateTime _lastTransitionUtc;

    public FakeMonitoredServerService(IConfiguration configuration, ILogger<FakeMonitoredServerService> logger)
    {
        var server = configuration.GetSection("MonitoredServer");
        var legacy = configuration.GetSection("MonitoredServerSimulation");

        Name = server["Name"] ?? legacy["Name"] ?? "Monitored uplink (health endpoint)";

        Scenario = ParseScenario(server["Scenario"] ?? legacy["Scenario"]);

        var intermittent = server.GetSection("Intermittent");
        _healthyDurationTicks = intermittent.GetValue("HealthyDurationTicks",
            legacy.GetValue<int?>("HealthyDurationTicks") ?? 8);
        _outageMinTicks = intermittent.GetValue("OutageMinTicks",
            legacy.GetValue<int?>("OutageMinTicks") ?? 2);
        _outageMaxTicks = Math.Max(_outageMinTicks, intermittent.GetValue("OutageMaxTicks",
            legacy.GetValue<int?>("OutageMaxTicks") ?? 5));

        var seed = intermittent.GetValue<int?>("RandomSeed") ?? legacy.GetValue<int?>("RandomSeed");
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();

        lock (_sync)
        {
            switch (Scenario)
            {
                case MonitoredHealthScenario.AlwaysHealthy:
                    _isHealthy = true;
                    _phase = MonitoredPhase.Healthy;
                    _lastTransitionUtc = DateTime.UtcNow;
                    break;
                case MonitoredHealthScenario.EthernetLost:
                    _isHealthy = false;
                    _phase = MonitoredPhase.Outage;
                    _lastTransitionUtc = DateTime.UtcNow;
                    break;
                default:
                    _isHealthy = true;
                    _phase = MonitoredPhase.Healthy;
                    _ticksInPhase = 0;
                    _lastTransitionUtc = DateTime.UtcNow;
                    break;
            }
        }

        logger.LogInformation(
            "FakeMonitoredServerService: Scenario={Scenario}, Name={Name}, health endpoint returns 204 when healthy. Intermittent ticks: H={H}, O={Min}-{Max}",
            Scenario, Name, _healthyDurationTicks, _outageMinTicks, _outageMaxTicks);
    }

    public bool IsIntermittent => Scenario == MonitoredHealthScenario.Intermittent;

    public string ScenarioDisplay => Scenario switch
    {
        MonitoredHealthScenario.AlwaysHealthy => "AlwaysHealthy — always 204",
        MonitoredHealthScenario.EthernetLost => "EthernetLost — always 503 (no uplink)",
        MonitoredHealthScenario.Intermittent => "Intermittent — 204/503 alternates by ticks",
        _ => Scenario.ToString()
    };

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

    /// <summary>Advance intermittent scenario by one poll interval; no-op for static scenarios.</summary>
    public void Advance()
    {
        if (!IsIntermittent) return;

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

    private static MonitoredHealthScenario ParseScenario(string? s) =>
        s?.Trim().ToUpperInvariant() switch
        {
            "ETHERNETLOST" or "ETHERNET_LOST" or "DOWN" or "TROUBLE" or "TROUBLES" or "OUTAGE" =>
                MonitoredHealthScenario.EthernetLost,
            "INTERMITTENT" or "FLAPPING" =>
                MonitoredHealthScenario.Intermittent,
            "ALWAYSHEALTHY" or "ALWAYS_HEALTHY" or "HEALTHY" or "UP" or "" or null =>
                MonitoredHealthScenario.AlwaysHealthy,
            _ => MonitoredHealthScenario.AlwaysHealthy
        };
}

public enum MonitoredHealthScenario
{
    AlwaysHealthy,
    EthernetLost,
    Intermittent
}

public enum MonitoredPhase
{
    Healthy,
    Outage
}
