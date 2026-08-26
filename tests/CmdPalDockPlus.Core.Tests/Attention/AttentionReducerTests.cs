using CmdPalDockPlus.Core.Attention;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Attention;

public sealed class AttentionReducerTests
{
    [Theory]
    [InlineData(AttentionLevel.None, AttentionLevel.Attention, AttentionLevel.Attention)]
    [InlineData(AttentionLevel.Urgent, AttentionLevel.Attention, AttentionLevel.Urgent)]
    [InlineData(AttentionLevel.Informational, AttentionLevel.None, AttentionLevel.Informational)]
    public void GroupUsesHighestLevel(AttentionLevel first, AttentionLevel second, AttentionLevel expected)
    {
        var result = AttentionReducer.Combine([new AttentionSignal(first, "first"), new AttentionSignal(second, "second")]);
        result.Level.Should().Be(expected);
    }

    [Fact]
    public void HighestLevelReasonWinsDeterministically()
    {
        var result = AttentionReducer.Combine([new AttentionSignal(AttentionLevel.Attention, "needs input"), new AttentionSignal(AttentionLevel.Informational, "done")]);
        result.Reason.Should().Be("needs input");
        result.IsActive.Should().BeTrue();
    }
}
