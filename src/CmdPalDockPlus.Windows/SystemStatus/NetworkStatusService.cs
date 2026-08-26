using Windows.Networking.Connectivity;

namespace CmdPalDockPlus.Windows.SystemStatus;

public readonly record struct NetworkSnapshot(
    string ProfileName,
    NetworkConnectivityLevel Connectivity,
    bool IsWifi,
    bool IsCellular)
{
    public bool HasInternet => Connectivity == NetworkConnectivityLevel.InternetAccess;
}

public sealed class NetworkStatusService : IDisposable
{
    private readonly object _gate = new();
    private NetworkSnapshot _snapshot;
    private bool _disposed;

    public NetworkStatusService()
    {
        _snapshot = ReadSnapshot();
        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
    }

    public event EventHandler? Changed;

    public NetworkSnapshot Snapshot
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
        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        GC.SuppressFinalize(this);
    }

    private void OnNetworkStatusChanged(object sender)
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

    private static NetworkSnapshot ReadSnapshot()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            return new NetworkSnapshot(
                profile?.ProfileName ?? "Disconnected",
                profile?.GetNetworkConnectivityLevel() ?? NetworkConnectivityLevel.None,
                profile?.IsWlanConnectionProfile == true,
                profile?.IsWwanConnectionProfile == true);
        }
        catch
        {
            return new NetworkSnapshot("Unavailable", NetworkConnectivityLevel.None, false, false);
        }
    }
}
