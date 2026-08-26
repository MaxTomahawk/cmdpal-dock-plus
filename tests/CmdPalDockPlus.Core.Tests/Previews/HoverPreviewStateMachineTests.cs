using CmdPalDockPlus.Core.Compatibility;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Previews;

public sealed class HoverPreviewStateMachineTests
{
    [Fact]
    public void EnterAThenEnterBReplacesPreviewTarget()
    {
        var state = new HoverPreviewStateMachine();
        state.Apply(new HoverEvent(HoverEventKind.Enter, "tile:a", new HoverRect(10, 10, 48, 40)));
        state.Apply(new HoverEvent(HoverEventKind.Enter, "tile:b", new HoverRect(20, 20, 48, 40)));
        state.CurrentCommandId.Should().Be("tile:b");
    }

    [Fact]
    public void StaleLeaveDoesNotClearNewerTarget()
    {
        var state = new HoverPreviewStateMachine();
        state.Apply(new HoverEvent(HoverEventKind.Enter, "tile:a", new HoverRect(10, 10, 48, 40)));
        state.Apply(new HoverEvent(HoverEventKind.Enter, "tile:b", new HoverRect(20, 20, 48, 40)));
        state.Apply(new HoverEvent(HoverEventKind.Leave, "tile:a", null));
        state.CurrentCommandId.Should().Be("tile:b");
    }

    [Fact]
    public void MatchingLeaveClearsTarget()
    {
        var state = new HoverPreviewStateMachine();
        state.Apply(new HoverEvent(HoverEventKind.Enter, "tile:a", new HoverRect(10, 10, 48, 40)));
        state.Apply(new HoverEvent(HoverEventKind.Leave, "tile:a", null));
        state.CurrentCommandId.Should().BeNull();
    }
}
