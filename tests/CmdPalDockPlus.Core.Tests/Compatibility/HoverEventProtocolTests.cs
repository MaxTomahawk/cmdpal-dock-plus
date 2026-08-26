using CmdPalDockPlus.Core.Compatibility;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Compatibility;

public sealed class HoverEventProtocolTests
{
    [Fact]
    public void ParsesEnterVector()
    {
        const string json = "{\"version\":1,\"kind\":\"enter\",\"commandId\":\"tile:vscode:hwnd:2A\",\"x\":100,\"y\":1040,\"width\":48,\"height\":40}";
        var evt = HoverEventProtocol.Parse(json);
        evt.Kind.Should().Be(HoverEventKind.Enter);
        evt.CommandId.Should().Be("tile:vscode:hwnd:2A");
        evt.Anchor.Should().Be(new HoverRect(100, 1040, 48, 40));
    }

    [Fact]
    public void ParsesLeaveVector()
    {
        const string json = "{\"version\":1,\"kind\":\"leave\",\"commandId\":\"tile:vscode:hwnd:2A\"}";
        HoverEventProtocol.Parse(json).Kind.Should().Be(HoverEventKind.Leave);
    }

    [Theory]
    [InlineData("{\"version\":2,\"kind\":\"leave\",\"commandId\":\"tile:a\"}")]
    [InlineData("{\"version\":1,\"kind\":\"leave\",\"commandId\":\"\"}")]
    [InlineData("{\"version\":1,\"kind\":\"enter\",\"commandId\":\"tile:a\",\"x\":0,\"y\":0,\"width\":-1,\"height\":40}")]
    public void RejectsInvalidVectors(string json)
    {
        var act = () => HoverEventProtocol.Parse(json);
        act.Should().Throw<HoverProtocolException>();
    }

    [Fact]
    public void RejectsPayloadOverEightKib()
    {
        var payload = new string('x', HoverEventProtocol.MaxMessageBytes + 1);
        var act = () => HoverEventProtocol.Parse(payload);
        act.Should().Throw<HoverProtocolException>();
    }
}
