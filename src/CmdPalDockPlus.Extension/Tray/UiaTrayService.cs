using System.Windows.Automation;

namespace CmdPalDockPlus.Extension.Tray;

internal sealed record TrayEntry(
    string Key,
    string DisplayName,
    bool IsVisible,
    AutomationElement Element);

internal sealed class UiaTrayService : IDisposable
{
    private const string ShellTrayClass = "Shell_TrayWnd";
    private const string OverflowClass = "TopLevelWindowForOverflowXamlIsland";
    private const string ShowHiddenName = "Show Hidden Icons";

    private readonly object _gate = new();
    private readonly Timer _debounce;
    private readonly Timer _watchdog;
    private AutomationElement? _taskbar;
    private IReadOnlyList<TrayEntry> _entries = [];
    private bool _disposed;

    public UiaTrayService()
    {
        _debounce = new Timer(_ => RefreshAndRebind(), null, Timeout.Infinite, Timeout.Infinite);
        _watchdog = new Timer(_ => RefreshAndRebind(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        RefreshAndRebind();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<TrayEntry> Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public bool TryInvoke(string key)
    {
        TrayEntry? entry;
        lock (_gate)
        {
            entry = _entries.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
        }

        if (entry is null) return false;
        return TryInvokeElement(entry.Element);
    }

    public bool TryShowHiddenIcons()
    {
        var taskbar = FindTaskbar();
        if (taskbar is null) return false;
        try
        {
            var chevron = taskbar.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, ShowHiddenName));
            if (chevron is null || !TryInvokeElement(chevron)) return false;
            _ = Task.Run(async () =>
            {
                await Task.Delay(350).ConfigureAwait(false);
                ScheduleRefresh();
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachTaskbarHandlers();
        _debounce.Dispose();
        _watchdog.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnStructureChanged(object sender, StructureChangedEventArgs e) => ScheduleRefresh();

    private void OnPropertyChanged(object sender, AutomationPropertyChangedEventArgs e) => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        if (_disposed) return;
        _ = _debounce.Change(TimeSpan.FromMilliseconds(200), Timeout.InfiniteTimeSpan);
    }

    private void RefreshAndRebind()
    {
        if (_disposed) return;
        try
        {
            var taskbar = FindTaskbar();
            if (!SameElement(_taskbar, taskbar))
            {
                DetachTaskbarHandlers();
                _taskbar = taskbar;
                AttachTaskbarHandlers();
            }

            var next = Enumerate(taskbar);
            var changed = false;
            lock (_gate)
            {
                if (!Equivalent(_entries, next))
                {
                    _entries = next;
                    changed = true;
                }
                else
                {
                    // Keep fresh AutomationElement handles even when metadata is unchanged.
                    _entries = next;
                }
            }

            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // UIA can throw while Explorer is rebuilding the XAML island. The
            // watchdog/relevant UIA event will retry without a tight loop.
        }
    }

    private void AttachTaskbarHandlers()
    {
        if (_taskbar is null) return;
        try
        {
            Automation.AddStructureChangedEventHandler(
                _taskbar,
                TreeScope.Subtree,
                OnStructureChanged);
            Automation.AddAutomationPropertyChangedEventHandler(
                _taskbar,
                TreeScope.Subtree,
                OnPropertyChanged,
                AutomationElement.NameProperty,
                AutomationElement.AutomationIdProperty,
                AutomationElement.IsOffscreenProperty);
        }
        catch
        {
        }
    }

    private void DetachTaskbarHandlers()
    {
        if (_taskbar is null) return;
        try
        {
            Automation.RemoveStructureChangedEventHandler(_taskbar, OnStructureChanged);
            Automation.RemoveAutomationPropertyChangedEventHandler(_taskbar, OnPropertyChanged);
        }
        catch
        {
        }
        _taskbar = null;
    }

    private static IReadOnlyList<TrayEntry> Enumerate(AutomationElement? taskbar)
    {
        var result = new List<TrayEntry>();
        if (taskbar is not null)
        {
            Collect(taskbar, isVisible: true, result);
        }

        try
        {
            var overflow = AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty, OverflowClass));
            if (overflow is not null)
            {
                Collect(overflow, isVisible: false, result);
            }
        }
        catch
        {
        }

        return result
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.IsVisible ? 0 : 1)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Collect(AutomationElement root, bool isVisible, List<TrayEntry> result)
    {
        try
        {
            var buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement button in buttons)
            {
                if (!TryDescribe(button, isVisible, out var entry)) continue;
                result.Add(entry);
            }
        }
        catch
        {
        }
    }

    private static bool TryDescribe(AutomationElement element, bool isVisible, out TrayEntry entry)
    {
        entry = null!;
        try
        {
            var className = element.Current.ClassName ?? string.Empty;
            if (!className.StartsWith("SystemTray.", StringComparison.Ordinal)) return false;
            if (!string.Equals(element.Current.AutomationId, "NotifyItemIcon", StringComparison.Ordinal)) return false;

            var label = (element.Current.Name ?? string.Empty).Trim();
            if (string.Equals(label, ShowHiddenName, StringComparison.OrdinalIgnoreCase)) return false;
            var newline = label.IndexOfAny(['\r', '\n']);
            if (newline >= 0) label = label[..newline].Trim();
            if (string.IsNullOrWhiteSpace(label)) return false;

            var runtimeId = element.GetRuntimeId();
            var key = runtimeId is { Length: > 0 }
                ? "uia-" + string.Join('-', runtimeId.Select(value => value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)))
                : $"uia-name-{label}";
            entry = new TrayEntry(key, label, isVisible, element);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AutomationElement? FindTaskbar()
    {
        try
        {
            return AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty, ShellTrayClass));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryInvokeElement(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)
                && pattern is InvokePattern invoke)
            {
                invoke.Invoke();
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool SameElement(AutomationElement? left, AutomationElement? right)
    {
        if (left is null || right is null) return left is null && right is null;
        try
        {
            return left.GetRuntimeId().AsSpan().SequenceEqual(right.GetRuntimeId());
        }
        catch
        {
            return false;
        }
    }

    private static bool Equivalent(IReadOnlyList<TrayEntry> left, IReadOnlyList<TrayEntry> right)
    {
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Key, right[index].Key, StringComparison.Ordinal)
                || !string.Equals(left[index].DisplayName, right[index].DisplayName, StringComparison.Ordinal)
                || left[index].IsVisible != right[index].IsVisible)
            {
                return false;
            }
        }
        return true;
    }
}
