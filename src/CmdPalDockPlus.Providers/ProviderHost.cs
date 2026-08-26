using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Providers.Media;
using CmdPalDockPlus.Windows;
using CmdPalDockPlus.Windows.Media;

namespace CmdPalDockPlus.Providers;

public sealed class ProviderHost : IDisposable, IAsyncDisposable
{
    private static readonly IReadOnlySet<string> ProcessMetricFields = new HashSet<string>(["process.cpu", "process.memory", "process.uptime"], StringComparer.Ordinal);
    private readonly IReadOnlyList<IWindowDataAdapter> _adapters;
    private readonly IProcessMetricsReader _metrics;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _samplingGate = new();
    private Task? _samplingTask;
    private bool _needsSampling;
    private bool _disposed;

    public ProviderHost(
        IProcessMetricsReader? metrics = null,
        IEnumerable<IWindowDataAdapter>? adapters = null,
        IMediaSessionService? mediaSessionService = null)
    {
        _metrics = metrics ?? new ProcessMetricsReader();
        _adapters = (adapters ??
        [
            new VSCodeAdapter(),
            new BrowserAdapter(),
            new TerminalAdapter(),
            new ExplorerAdapter(),
            new MediaProvider(mediaSessionService ?? new MediaSessionService()),
        ]).ToArray();

        foreach (var invalidating in _adapters.OfType<IInvalidatingWindowDataAdapter>())
        {
            invalidating.DataInvalidated += OnAdapterInvalidated;
        }
    }

    public event EventHandler? DataInvalidated;

    public IReadOnlySet<string> RequestedFields(AppProfile profile) => ProfileFieldDependencies.Resolve(profile);

    public IReadOnlyList<CapabilityDescriptor> Probe(WindowSnapshot? window)
    {
        var result = GenericCapabilities().ToList();
        if (window is not null)
        {
            foreach (var adapter in _adapters.Where(adapter => adapter.Supports(window)))
            {
                result.AddRange(adapter.Capabilities);
            }
        }

        return result
            .GroupBy(capability => capability.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(capability => capability.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyDictionary<string, object?> Enrich(
        AppProfile profile,
        WindowSnapshot window,
        IReadOnlyDictionary<string, object?> genericValues)
    {
        var requested = RequestedFields(profile);
        Dictionary<string, object?> values = new(genericValues, StringComparer.Ordinal);

        if (requested.Contains("app.name")) values["app.name"] = profile.DisplayName;
        if (requested.Contains("attention.level")) values["attention.level"] = "None";
        if (requested.Contains("attention.reason")) values["attention.reason"] = null;
        if (requested.Contains("attention.isActive")) values["attention.isActive"] = false;

        foreach (var adapter in _adapters.Where(adapter => adapter.Supports(window)))
        {
            adapter.Enrich(window, requested, values);
        }

        if (Overlaps(requested, ProcessMetricFields))
        {
            try
            {
                var sample = _metrics.Read(window.ProcessId);
                if (requested.Contains("process.cpu")) values["process.cpu"] = sample.CpuPercent;
                if (requested.Contains("process.memory")) values["process.memory"] = sample.MemoryBytes;
                if (requested.Contains("process.uptime")) values["process.uptime"] = sample.Uptime.TotalSeconds;
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        return values;
    }

    public void ConfigureSampling(IEnumerable<AppProfile> profiles)
    {
        var needed = profiles.Where(profile => profile.Enabled).Any(profile => Overlaps(RequestedFields(profile), ProcessMetricFields));
        lock (_samplingGate)
        {
            _needsSampling = needed;
            if (needed && _samplingTask is null)
            {
                _samplingTask = Task.Run(() => SamplingLoopAsync(_disposeCts.Token), CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        UnsubscribeAdapters();
        foreach (var disposable in _adapters.OfType<IDisposable>())
        {
            disposable.Dispose();
        }

        _disposeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        UnsubscribeAdapters();

        if (_samplingTask is not null)
        {
            try
            {
                await _samplingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (var adapter in _adapters)
        {
            if (adapter is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (adapter is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _disposeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SamplingLoopAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            lock (_samplingGate)
            {
                if (!_needsSampling)
                {
                    _samplingTask = null;
                    return;
                }
            }

            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAdapterInvalidated(object? sender, EventArgs e) => DataInvalidated?.Invoke(this, EventArgs.Empty);

    private void UnsubscribeAdapters()
    {
        foreach (var invalidating in _adapters.OfType<IInvalidatingWindowDataAdapter>())
        {
            invalidating.DataInvalidated -= OnAdapterInvalidated;
        }
    }

    private static bool Overlaps(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        foreach (var value in left)
        {
            if (right.Contains(value)) return true;
        }

        return false;
    }

    private static IEnumerable<CapabilityDescriptor> GenericCapabilities()
    {
        yield return new("app.name", "Application name", "Configured profile display name.", "profile");
        yield return new("process.executable", "Executable", "Full executable path/name.", "snapshot");
        yield return new("process.pid", "Process ID", "Owning process id.", "event-driven");
        yield return new("process.cpu", "CPU", "CPU usage percentage; sampled only when selected.", "sampled/2s");
        yield return new("process.memory", "Memory", "Working-set bytes; sampled only when selected.", "sampled/2s");
        yield return new("process.uptime", "Process uptime", "Uptime seconds; sampled only when selected.", "sampled/2s");
        yield return new("window.title", "Window title", "Current top-level window title.", "event-driven");
        yield return new("window.state", "Window state", "Restored/minimized/maximized.", "event-driven");
        yield return new("window.isActive", "Active", "Whether this is the foreground window.", "event-driven");
        yield return new("window.isMinimized", "Minimized", "Whether the window is minimized.", "event-driven");
        yield return new("window.monitor", "Monitor", "Monitor device name.", "event-driven");
        yield return new("window.class", "Window class", "Win32 class name.", "snapshot/event");
        yield return new("window.count", "Window count", "Number of windows rendered by this tile.", "event-driven");
        yield return new("attention.level", "Attention level", "Normalized None/Informational/Attention/Urgent state.", "event-driven");
        yield return new("attention.reason", "Attention reason", "Human-readable reason supplied by a provider when available.", "event-driven");
        yield return new("attention.isActive", "Needs attention", "True when attention level is not None.", "event-driven");
    }
}
