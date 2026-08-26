namespace CmdPalDockPlus.Windows.Previews;

public readonly record struct PreviewSize(int Width, int Height);
public readonly record struct PreviewRect(int X, int Y, int Width, int Height);
public sealed record PreviewLayout(PreviewRect Bounds, IReadOnlyList<PreviewRect> Cells)
{
    private const int DefaultCellWidth = 320;
    private const int DefaultCellHeight = 220;

    public static PreviewLayout Calculate(int count, PreviewSize maximum)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (maximum.Width < 0 || maximum.Height < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (count == 0 || maximum.Width == 0 || maximum.Height == 0)
        {
            return new PreviewLayout(new PreviewRect(0, 0, 0, 0), []);
        }

        var columns = Math.Min(count, Math.Max(1, maximum.Width / Math.Min(DefaultCellWidth, Math.Max(1, maximum.Width))));
        columns = Math.Min(columns, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count))));
        var rows = (int)Math.Ceiling(count / (double)columns);

        while (rows * DefaultCellHeight > maximum.Height && columns < count)
        {
            columns++;
            rows = (int)Math.Ceiling(count / (double)columns);
        }

        var cellWidth = Math.Max(1, Math.Min(DefaultCellWidth, maximum.Width / columns));
        var cellHeight = Math.Max(1, Math.Min(DefaultCellHeight, maximum.Height / rows));
        var width = Math.Min(maximum.Width, columns * cellWidth);
        var height = Math.Min(maximum.Height, rows * cellHeight);
        var cells = new List<PreviewRect>(count);
        for (var index = 0; index < count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            cells.Add(new PreviewRect(column * cellWidth, row * cellHeight, cellWidth, cellHeight));
        }

        return new PreviewLayout(new PreviewRect(0, 0, width, height), cells);
    }
}
