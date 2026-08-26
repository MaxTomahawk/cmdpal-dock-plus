namespace CmdPalDockPlus.Core.Providers;

public enum ProviderValueType
{
    String,
    Boolean,
    Number,
    Image,
    Enum,
}

public enum UpdateModel
{
    EventDriven,
    Sampled,
    SnapshotOnly,
}

public sealed record ProviderField(
    string Id,
    string DisplayName,
    string Description,
    ProviderValueType ValueType,
    object? CurrentValue,
    UpdateModel UpdateModel);

public sealed record ProviderActionDescriptor(string Id, string DisplayName, string Description);

public sealed record ProviderProbeResult(
    string ProviderId,
    bool Supported,
    IReadOnlyList<ProviderField> Fields,
    IReadOnlyList<ProviderActionDescriptor> Actions);

public readonly record struct DockTargetId(string Value);
public sealed record DockTarget(DockTargetId Id, string AppId, nint? Hwnd, int? ProcessId);
public sealed record DockDataChange(string FieldId, object? Value, DateTimeOffset Timestamp);

public sealed record ProviderDescriptor(string Id, IReadOnlySet<string> Fields)
{
    public ProviderDescriptor(string id, IEnumerable<string> fields)
        : this(id, new HashSet<string>(fields, StringComparer.Ordinal))
    {
    }
}

public sealed class ProviderCatalog(IEnumerable<ProviderDescriptor> providers)
{
    private readonly IReadOnlyList<ProviderDescriptor> _providers = providers.ToArray();

    public IReadOnlyList<ProviderDescriptor> Providers => _providers;
}

public interface IDockDataProvider
{
    string Id { get; }
    string DisplayName { get; }

    ValueTask<ProviderProbeResult> ProbeAsync(
        AppSnapshot app,
        WindowSnapshot? window,
        CancellationToken cancellationToken);

    IAsyncEnumerable<DockDataChange> WatchAsync(
        DockTarget target,
        IReadOnlySet<string> requestedFields,
        CancellationToken cancellationToken);
}

public sealed record AppSnapshot(string Id, string DisplayName, string? ExecutablePath, string? Aumid, int? ProcessId);
public sealed record WindowSnapshot(nint Hwnd, int ProcessId, string Title, string ClassName);
