using ESP32Monitor.Models;

namespace ESP32Monitor.Services;

/// <summary>
/// Singleton in-memory cache of the last known ESP32 status.
/// Shared between PollingService and API controllers/Blazor pages.
/// </summary>
public class DeviceStateHolder
{
    private DeviceStatus _status = new() { Effect = "unknown" };
    private readonly ReaderWriterLockSlim _lock = new();

    public DeviceStatus GetStatus()
    {
        _lock.EnterReadLock();
        try { return _status; }
        finally { _lock.ExitReadLock(); }
    }

    public void SetStatus(DeviceStatus status)
    {
        _lock.EnterWriteLock();
        try { _status = status; }
        finally { _lock.ExitWriteLock(); }
    }

    public bool IsDeviceReachable { get; set; } = false;
    public bool IsSimulationMode  { get; set; } = false;
}
