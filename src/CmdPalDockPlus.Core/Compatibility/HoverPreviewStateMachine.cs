namespace CmdPalDockPlus.Core.Compatibility;

public sealed class HoverPreviewStateMachine
{
    public string? CurrentCommandId { get; private set; }
    public HoverRect? CurrentAnchor { get; private set; }

    public void Apply(HoverEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.Kind == HoverEventKind.Enter)
        {
            CurrentCommandId = evt.CommandId;
            CurrentAnchor = evt.Anchor;
            return;
        }

        if (string.Equals(CurrentCommandId, evt.CommandId, StringComparison.Ordinal))
        {
            CurrentCommandId = null;
            CurrentAnchor = null;
        }
    }

    public void Clear()
    {
        CurrentCommandId = null;
        CurrentAnchor = null;
    }
}
