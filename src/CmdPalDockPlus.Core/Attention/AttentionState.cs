namespace CmdPalDockPlus.Core.Attention;

public enum AttentionLevel
{
    None = 0,
    Informational = 1,
    Attention = 2,
    Urgent = 3,
}

public sealed record AttentionSignal(AttentionLevel Level, string? Reason = null)
{
    public bool IsActive => Level != AttentionLevel.None;

    public static AttentionSignal None { get; } = new(AttentionLevel.None);
}

public static class AttentionReducer
{
    public static AttentionSignal Combine(IEnumerable<AttentionSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var best = AttentionSignal.None;
        foreach (var signal in signals)
        {
            if (signal.Level > best.Level)
            {
                best = signal;
            }
        }

        return best;
    }
}
