using CmdPalDockPlus.Windows.Previews;
using FluentAssertions;

namespace CmdPalDockPlus.Windows.Tests.Previews;

public sealed class PreviewLayoutTests
{
    [Theory]
    [InlineData(1, 320, 220)]
    [InlineData(2, 640, 220)]
    [InlineData(4, 640, 440)]
    [InlineData(7, 960, 660)]
    public void LayoutUsesBoundedGrid(int count, int maxWidth, int maxHeight)
    {
        var layout = PreviewLayout.Calculate(count, new PreviewSize(maxWidth, maxHeight));
        layout.Cells.Should().HaveCount(count);
        layout.Bounds.Width.Should().BeLessThanOrEqualTo(maxWidth);
        layout.Bounds.Height.Should().BeLessThanOrEqualTo(maxHeight);
        layout.Cells.Should().OnlyContain(cell => cell.Width > 0 && cell.Height > 0);
    }

    [Fact]
    public void EmptyLayoutHasNoCells()
    {
        var layout = PreviewLayout.Calculate(0, new PreviewSize(640, 440));
        layout.Cells.Should().BeEmpty();
        layout.Bounds.Should().Be(new PreviewRect(0, 0, 0, 0));
    }
}
