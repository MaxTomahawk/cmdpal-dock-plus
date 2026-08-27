using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CmdPalDockPlus.Extension.Tray;

internal static class TrayIconCapture
{
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const uint SrcCopyCaptureBlt = 0x00CC0020 | 0x40000000;
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(3);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.Ordinal);
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CmdPalDockPlus",
        "tray-icons");

    public static string? TryCaptureToFile(AutomationElement element, string key)
    {
        if (Cache.TryGetValue(key, out var cached)
            && File.Exists(cached.Path)
            && DateTime.UtcNow - cached.CapturedAt < Freshness)
        {
            return cached.Path;
        }

        try
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width < 1 || rect.Height < 1)
            {
                return null;
            }

            var side = (int)Math.Floor(Math.Min(rect.Width, rect.Height));
            if (side < 1)
            {
                return null;
            }

            var x = (int)Math.Round(rect.X + ((rect.Width - side) / 2d));
            var y = (int)Math.Round(rect.Y + ((rect.Height - side) / 2d));
            var bgra = CaptureBgra(x, y, side);
            if (bgra is null)
            {
                return null;
            }

            for (var index = 3; index < bgra.Length; index += 4)
            {
                bgra[index] = byte.MaxValue;
            }

            var hash = Convert.ToHexString(SHA256.HashData(bgra).AsSpan(0, 8)).ToLowerInvariant();
            Directory.CreateDirectory(CacheDirectory);
            var path = Path.Combine(CacheDirectory, $"{Sanitize(key)}-{hash}.png");
            if (!File.Exists(path))
            {
                WritePng(path, bgra, side);
            }

            Cache[key] = new CacheEntry(path, DateTime.UtcNow);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? CaptureBgra(int sourceX, int sourceY, int side)
    {
        var screen = GetDC(0);
        if (screen == 0)
        {
            return null;
        }

        nint memory = 0;
        nint bitmap = 0;
        try
        {
            memory = CreateCompatibleDC(screen);
            if (memory == 0)
            {
                return null;
            }

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = side,
                    Height = -side,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                },
            };

            bitmap = CreateDIBSection(memory, ref info, DibRgbColors, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0)
            {
                return null;
            }

            var old = SelectObject(memory, bitmap);
            try
            {
                if (!BitBlt(memory, 0, 0, side, side, screen, sourceX, sourceY, SrcCopyCaptureBlt))
                {
                    return null;
                }

                var bytes = new byte[checked(side * side * 4)];
                Marshal.Copy(bits, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                if (old != 0)
                {
                    _ = SelectObject(memory, old);
                }
            }
        }
        finally
        {
            if (bitmap != 0)
            {
                _ = DeleteObject(bitmap);
            }

            if (memory != 0)
            {
                _ = DeleteDC(memory);
            }

            _ = ReleaseDC(0, screen);
        }
    }

    private static void WritePng(string path, byte[] bgra, int side)
    {
        var source = BitmapSource.Create(
            side,
            side,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            bgra,
            checked(side * 4));
        source.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        encoder.Save(stream);
    }

    private static string Sanitize(string key)
    {
        var chars = key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var value = new string(chars);
        return value.Length <= 96 ? value : value[..96];
    }

    private sealed record CacheEntry(string Path, DateTime CapturedAt);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(
        nint dc,
        ref BitmapInfo info,
        uint usage,
        out nint bits,
        nint section,
        uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint dc, nint obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        nint destination,
        int xDestination,
        int yDestination,
        int width,
        int height,
        nint source,
        int xSource,
        int ySource,
        uint operation);
}
