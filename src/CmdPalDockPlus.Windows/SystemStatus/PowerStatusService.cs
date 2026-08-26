using Windows.System.Power;

namespace CmdPalDockPlus.Windows.SystemStatus;

public readonly record struct PowerSnapshot(
    int RemainingChargePercent,
    BatteryStatus BatteryStatus,
    PowerSupplyStatus PowerSupplyStatus)
{
    public int ClampedPercent => Math.Clamp(RemainingChargePercent, 0, 100);
    public bool IsCharging => BatteryStatus == BatteryStatus.Charging;
    public bool IsOnExternalPower => PowerSupplyStatus == PowerSupplyStatus.Adequate;
}

public sealed class PowerStatusService : IDisposable
{
    private readonly object _gate = new();
    private PowerSnapshot _snapshot;
    private bool _disposed;

    public PowerStatusService()
    {
        _snapshot = ReadSnapshot();
        PowerManager.RemainingChargePercentChanged += OnPowerChanged;
        PowerManager.BatteryStatusChanged += OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
    }

    public event EventHandler? Changed;

    public PowerSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PowerManager.RemainingChargePercentChanged -= OnPowerChanged;
        PowerManager.BatteryStatusChanged -= OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged -= OnPowerChanged;
        GC.SuppressFinalize(this);
    }

    private void OnPowerChanged(object? sender, object args)
    {
        if (_disposed) return;
        var next = ReadSnapshot();
        var changed = false;
        lock (_gate)
        {
            if (_snapshot != next)
            {
                _snapshot = next;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static PowerSnapshot ReadSnapshot()
    {
        try
        {
            return new PowerSnapshot(
                PowerManager.RemainingChargePercent,
                PowerManager.BatteryStatus,
                PowerManager.PowerSupplyStatus);
        }
        catch
        {
            return new PowerSnapshot(0, BatteryStatus.NotPresent, PowerSupplyStatus.NotPresent);
        }
    }
}
