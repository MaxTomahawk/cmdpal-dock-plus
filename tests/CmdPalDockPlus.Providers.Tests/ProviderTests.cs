using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Providers;
using CmdPalDockPlus.Windows;
using FluentAssertions;

namespace CmdPalDockPlus.Providers.Tests;

public sealed class ProviderTests
{
    [Fact]
    public void DependenciesContainOnlyReferencedFields()
    {
        var profile = Profile("{vscode.workspace ?? window.title}", "{window.count}");
        ProfileFieldDependencies.Resolve(profile).Should().BeEquivalentTo(["vscode.workspace", "window.title", "window.count"]);
    }

    [Fact]
    public void VsCodeAdapterExtractsWorkspaceAndFile()
    {
        var adapter = new VSCodeAdapter();
        var window = Window("Code.exe", "DockItemControl.xaml - PowerToys - Visual Studio Code", "Chrome_WidgetWin_1");
        Dictionary<string, object?> values = [];
        adapter.Enrich(window, new HashSet<string> { "vscode.workspace", "vscode.file" }, values);
        values["vscode.workspace"].Should().Be("PowerToys");
        values["vscode.file"].Should().Be("DockItemControl.xaml");
    }

    [Fact]
    public void BrowserAdapterRecognizesPrivateWindow()
    {
        var adapter = new BrowserAdapter();
        Dictionary<string, object?> values = [];
        adapter.Enrich(Window("msedge.exe", "New tab - InPrivate - Microsoft Edge", "Chrome_WidgetWin_1"), new HashSet<string> { "browser.isPrivate" }, values);
        values["browser.isPrivate"].Should().Be(true);
    }

    [Fact]
    public void MetricsReaderIsNotCalledWhenMetricsAreUnused()
    {
        var metrics = new RecordingMetrics();
        using var host = new ProviderHost(metrics);
        var profile = Profile("{window.title}", "");
        _ = host.Enrich(profile, Window("app.exe", "Title", "AppClass"), new Dictionary<string, object?> { ["window.title"] = "Title" });
        metrics.ReadCount.Should().Be(0);
    }

    private static AppProfile Profile(string title, string subtitle) => new("app", "App", new ApplicationMatch(@"C:\App\app.exe", null), GroupingMode.Grouped, new DisplayTemplate(title, subtitle));
    private static WindowSnapshot Window(string exe, string title, string cls) => new((nint)1, Environment.ProcessId, exe, title, cls, WindowState.Restored, false, "DISPLAY1", 0, @"C:\App\" + exe);

    private sealed class RecordingMetrics : IProcessMetricsReader
    {
        public int ReadCount { get; private set; }
        public ProcessMetricSnapshot Read(int processId) { ReadCount++; return new ProcessMetricSnapshot(1, 2, TimeSpan.FromSeconds(3)); }
    }
}
