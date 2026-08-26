using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Core.Tiles;
using CmdPalDockPlus.Providers;
using CmdPalDockPlus.Windows;

namespace CmdPalDockPlus.Extension;

internal sealed partial class DockCoordinator : IAsyncDisposable
{
    private readonly IProfileStore _profileStore;
    private readonly WindowTracker _tracker;
    private readonly WindowActivator _activator;
    private readonly AppLauncher _launcher;
    private readonly ProviderHost _providers;
    private readonly TileComposer _composer = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<TileIdentity, DockTileListItem> _items = [];
    private readonly Dictionary<TileIdentity, DockTileState> _states = [];
    private ProfileDocument _document = ProfileDocument.Empty;
    private bool _initialized;

    public DockCoordinator(
        IProfileStore profileStore,
        WindowTracker tracker,
        WindowActivator activator,
        AppLauncher launcher,
        ProviderHost providers)
    {
        _profileStore = profileStore;
        _tracker = tracker;
        _activator = activator;
        _launcher = launcher;
        _providers = providers;
        _tracker.Changed += OnWindowsChanged;
        _providers.DataInvalidated += OnDataInvalidated;
    }

    public event EventHandler? TilesChanged;
    public event EventHandler? ProfilesChanged;

    public IReadOnlyList<DockTileListItem> Items { get; private set; } = [];
    public IReadOnlyList<AppProfile> Profiles => _document.Profiles;
    public IReadOnlyList<WindowSnapshot> Windows => _tracker.Snapshot;
    public string? LastError { get; private set; }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await ReloadProfilesAsync().ConfigureAwait(false);
        await _tracker.StartAsync(default).ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    public async Task ReloadProfilesAsync()
    {
        try
        {
            _document = await _profileStore.LoadAsync(default).ConfigureAwait(false);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _document = ProfileDocument.Empty;
        }

        _providers.ConfigureSampling(_document.Profiles);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        await RefreshAsync().ConfigureAwait(false);
    }

    public async Task UpsertProfileAsync(AppProfile profile)
    {
        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        var profiles = _document.Profiles.ToList();
        var index = profiles.FindIndex(candidate => string.Equals(candidate.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) profiles[index] = profile;
        else profiles.Add(profile);

        _document = new ProfileDocument(ProfileDocument.CurrentSchemaVersion, profiles);
        await _profileStore.SaveAsync(_document, default).ConfigureAwait(false);
        _providers.ConfigureSampling(_document.Profiles);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        await RefreshAsync().ConfigureAwait(false);
    }

    public async Task DeleteProfileAsync(string id)
    {
        _document = new ProfileDocument(
            ProfileDocument.CurrentSchemaVersion,
            _document.Profiles.Where(profile => !string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)).ToArray());
        await _profileStore.SaveAsync(_document, default).ConfigureAwait(false);
        _providers.ConfigureSampling(_document.Profiles);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        await RefreshAsync().ConfigureAwait(false);
    }

    public AppProfile? ProfileFor(TileIdentity identity)
        => _document.Profiles.FirstOrDefault(profile => identity.Value.StartsWith(profile.Id + ":", StringComparison.Ordinal));

    public bool TryGetStateByCommandId(string commandId, out DockTileState state)
    {
        state = default!;
        const string prefix = "tile:";
        if (!commandId.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return _states.TryGetValue(new TileIdentity(commandId[prefix.Length..]), out state!);
    }

    public IReadOnlyList<ProviderActionDescriptor> ProviderActions(TileIdentity identity)
    {
        var window = PrimaryWindow(identity);
        return window is null ? [] : _providers.Actions(window);
    }

    public async Task RunProviderActionAsync(TileIdentity identity, string actionId)
    {
        var action = ProviderActions(identity).FirstOrDefault(candidate =>
            string.Equals(candidate.Id, actionId, StringComparison.Ordinal));
        if (action is not null)
        {
            await action.InvokeAsync().ConfigureAwait(false);
        }
    }

    public async Task ActivateTileAsync(TileIdentity identity)
    {
        if (!_states.TryGetValue(identity, out var state)) return;
        var profile = ProfileFor(identity);
        if (profile is null) return;
        if (state.Windows.Count == 0 || state.PrimaryHwnd is null)
        {
            await _launcher.LaunchAsync(profile.Application, default).ConfigureAwait(false);
            return;
        }

        var window = _tracker.Snapshot.FirstOrDefault(candidate => candidate.Hwnd == state.PrimaryHwnd.Value);
        if (window is not null) await _activator.FocusAsync(window, default).ConfigureAwait(false);
    }

    public async Task LaunchNewAsync(TileIdentity identity)
    {
        var profile = ProfileFor(identity);
        if (profile is not null) await _launcher.LaunchAsync(profile.Application, default).ConfigureAwait(false);
    }

    public async Task OpenFileLocationAsync(TileIdentity identity)
    {
        var path = ProfileFor(identity)?.Application.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path))
            await _launcher.OpenFileLocationAsync(path, default).ConfigureAwait(false);
    }

    public Task FocusWindowAsync(nint hwnd)
    {
        var window = _tracker.Snapshot.FirstOrDefault(candidate => candidate.Hwnd == hwnd);
        return window is null ? Task.CompletedTask : _activator.FocusAsync(window, default).AsTask();
    }

    public Task MinimizeWindowAsync(nint hwnd) => _activator.MinimizeAsync(hwnd, default).AsTask();
    public Task MaximizeWindowAsync(nint hwnd) => _activator.MaximizeAsync(hwnd, default).AsTask();
    public Task CloseWindowAsync(nint hwnd) => _activator.CloseAsync(hwnd, default).AsTask();

    public async Task CloseAllAsync(TileIdentity identity)
    {
        if (!_states.TryGetValue(identity, out var state)) return;
        foreach (var window in state.Windows)
            await _activator.CloseAsync(window.Hwnd, default).ConfigureAwait(false);
    }

    public async Task RunUserActionAsync(TileIdentity identity, string actionId)
    {
        var action = ProfileFor(identity)?.UserActions.FirstOrDefault(candidate => candidate.Id == actionId);
        if (action is not null) await _launcher.RunUserActionAsync(action, default).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _tracker.Changed -= OnWindowsChanged;
        _providers.DataInvalidated -= OnDataInvalidated;
        await _tracker.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private WindowSnapshot? PrimaryWindow(TileIdentity identity)
    {
        if (!_states.TryGetValue(identity, out var state) || state.PrimaryHwnd is not { } hwnd)
        {
            return null;
        }

        return _tracker.Snapshot.FirstOrDefault(candidate => candidate.Hwnd == hwnd);
    }

    private async Task RefreshAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var states = new List<DockTileState>();
            foreach (var profile in _document.Profiles.Where(profile => profile.Enabled))
            {
                var windows = new List<TileWindow>();
                foreach (var snapshot in _tracker.Snapshot.Where(window => ApplicationWindowMatcher.Matches(profile.Application, window)))
                {
                    var generic = GenericValues(snapshot);
                    var values = _providers.Enrich(profile, snapshot, generic);
                    windows.Add(new TileWindow(snapshot.Hwnd, snapshot.ProcessId, snapshot.Title, snapshot.IsActive, snapshot.MruRank, values));
                }

                states.AddRange(_composer.Compose(profile, windows));
            }

            var seen = new HashSet<TileIdentity>();
            foreach (var state in states)
            {
                seen.Add(state.Identity);
                _states[state.Identity] = state;
                if (_items.TryGetValue(state.Identity, out var existing)) existing.Update(state);
                else _items.Add(state.Identity, new DockTileListItem(this, state));
            }

            foreach (var stale in _items.Keys.Where(key => !seen.Contains(key)).ToArray())
            {
                _items.Remove(stale);
                _states.Remove(stale);
            }

            Items = states.Select(state => _items[state.Identity]).ToArray();
        }
        finally
        {
            _gate.Release();
        }

        TilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowsChanged(object? sender, WindowSetChanged e) => _ = RefreshAsync();
    private void OnDataInvalidated(object? sender, EventArgs e) => _ = RefreshAsync();

    private static IReadOnlyDictionary<string, object?> GenericValues(WindowSnapshot window)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["window.title"] = window.Title,
            ["window.state"] = window.State.ToString(),
            ["window.isActive"] = window.IsActive,
            ["window.isMinimized"] = window.State == WindowState.Minimized,
            ["window.monitor"] = window.Monitor,
            ["window.class"] = window.ClassName,
            ["process.pid"] = window.ProcessId,
            ["process.executable"] = window.ExecutablePath ?? window.ExecutableName,
            ["process.aumid"] = window.AppUserModelId,
        };
}