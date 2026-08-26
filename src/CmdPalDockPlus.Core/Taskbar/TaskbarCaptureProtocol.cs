using System.Buffers.Binary;
using System.Text;

namespace CmdPalDockPlus.Core.Taskbar;

public sealed class TaskbarCaptureProtocolException(string message) : FormatException(message);

public static class TaskbarCaptureProtocol
{
    public const uint Magic = 0x31504443; // 'CDP1' little-endian
    public const ushort MajorVersion = 1;
    public const int HeaderSize = 24;
    public const int MaxMessageBytes = 1024 * 1024;
    public const int MaxDescriptionBytes = 4096;

    public static TaskbarCaptureMessage Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize) throw new TaskbarCaptureProtocolException("Taskbar message header is truncated.");
        if (data.Length > MaxMessageBytes) throw new TaskbarCaptureProtocolException("Taskbar message exceeds 1 MiB.");

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        var major = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        var type = (MessageType)BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        var sequence = BinaryPrimitives.ReadUInt64LittleEndian(data[12..]);
        var processId = BinaryPrimitives.ReadUInt32LittleEndian(data[20..]);
        if (magic != Magic) throw new TaskbarCaptureProtocolException("Taskbar message magic is invalid.");
        if (major != MajorVersion) throw new TaskbarCaptureProtocolException("Unsupported taskbar capture protocol version.");
        if (payloadLength > MaxMessageBytes - HeaderSize || data.Length != HeaderSize + payloadLength)
            throw new TaskbarCaptureProtocolException("Taskbar message payload length is invalid.");
        if (processId > int.MaxValue) throw new TaskbarCaptureProtocolException("Process id is out of range.");

        var payload = data[HeaderSize..];
        return type switch
        {
            MessageType.Hello => ParseHello(sequence, (int)processId, payload),
            MessageType.SetProgressState => ParseProgressState(sequence, (int)processId, payload),
            MessageType.SetProgressValue => ParseProgressValue(sequence, (int)processId, payload),
            MessageType.SetOverlayIcon => ParseOverlay(sequence, (int)processId, payload),
            MessageType.ClearOverlayIcon => ParseOverlayClear(sequence, (int)processId, payload),
            MessageType.ProcessExiting => ParseProcessExit(sequence, (int)processId, payload),
            _ => throw new TaskbarCaptureProtocolException("Unknown taskbar capture message type."),
        };
    }

    public static byte[] Serialize(TaskbarCaptureMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = message switch
        {
            TaskbarHello hello => SerializeHello(hello),
            ProgressStateChanged state => SerializeProgressState(state),
            ProgressValueChanged value => SerializeProgressValue(value),
            OverlayChanged overlay => SerializeOverlay(overlay),
            OverlayCleared clear => SerializeOverlayClear(clear),
            TaskbarProcessExited => Array.Empty<byte>(),
            _ => throw new TaskbarCaptureProtocolException("Unsupported taskbar capture message."),
        };
        if (payload.Length > MaxMessageBytes - HeaderSize) throw new TaskbarCaptureProtocolException("Taskbar message exceeds 1 MiB.");

        var result = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), MajorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(6), (ushort)TypeFor(message));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), checked((uint)payload.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(12), message.Sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20), checked((uint)message.ProcessId));
        payload.CopyTo(result, HeaderSize);
        return result;
    }

    private static TaskbarHello ParseHello(ulong sequence, int processId, ReadOnlySpan<byte> payload)
    {
        RequireLength(payload, 4);
        var architecture = (TaskbarArchitecture)BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (!Enum.IsDefined(architecture)) architecture = TaskbarArchitecture.Unknown;
        return new TaskbarHello(sequence, processId, architecture);
    }

    private static ProgressStateChanged ParseProgressState(ulong sequence, int processId, ReadOnlySpan<byte> payload)
    {
        RequireLength(payload, 12);
        var hwnd = ToHwnd(BinaryPrimitives.ReadUInt64LittleEndian(payload));
        var mode = (TaskbarProgressMode)BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]);
        if (mode is not (TaskbarProgressMode.None or TaskbarProgressMode.Indeterminate or TaskbarProgressMode.Normal or TaskbarProgressMode.Error or TaskbarProgressMode.Paused))
            throw new TaskbarCaptureProtocolException("Taskbar progress state is invalid.");
        return new ProgressStateChanged(sequence, processId, hwnd, mode);
    }

    private static ProgressValueChanged ParseProgressValue(ulong sequence, int processId, ReadOnlySpan<byte> payload)
    {
        RequireLength(payload, 24);
        return new ProgressValueChanged(
            sequence,
            processId,
            ToHwnd(BinaryPrimitives.ReadUInt64LittleEndian(payload)),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[16..]));
    }

    private static OverlayChanged ParseOverlay(ulong sequence, int processId, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 20) throw new TaskbarCaptureProtocolException("Overlay payload is truncated.");
        var hwnd = ToHwnd(BinaryPrimitives.ReadUInt64LittleEndian(payload));
        var width = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..]);
        var descriptionLength = BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]);
        var rgbaLength = BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]);
        if (width == 0 || height == 0 || width > TaskbarOverlay.MaxDimension || height > TaskbarOverlay.MaxDimension)
            throw new TaskbarCaptureProtocolException("Overlay dimensions are invalid.");
        var expectedPixels = checked((uint)width * height * 4);
        if (rgbaLength != expectedPixels) throw new TaskbarCaptureProtocolException("Overlay RGBA length is invalid.");
        if (descriptionLength > MaxDescriptionBytes) throw new TaskbarCaptureProtocolException("Overlay description is too large.");
        var required = checked(20u + descriptionLength + rgbaLength);
        if (required != payload.Length) throw new TaskbarCaptureProtocolException("Overlay payload length is invalid.");

        var description = Encoding.UTF8.GetString(payload.Slice(20, checked((int)descriptionLength)));
        var rgba = payload.Slice(checked(20 + (int)descriptionLength), checked((int)rgbaLength)).ToArray();
        return new OverlayChanged(sequence, processId, hwnd, new TaskbarOverlay(width, height, rgba, description));
    }

    private static OverlayCleared ParseOverlayClear(ulong sequence, int processId, ReadOnlySpan<byte> payload)
    {
        RequireLength(payload, 8);
        return new OverlayCleared(sequence, processId, ToHwnd(BinaryPrimitives.ReadUInt64LittleEndian(payload)));
    }

    private static TaskbarProcessExited ParseProcessExit(ulong sequence, int processId, ReadOnlySpan<byte> payload)
    {
        RequireLength(payload, 0);
        return new TaskbarProcessExited(sequence, processId);
    }

    private static byte[] SerializeHello(TaskbarHello hello)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)hello.Architecture);
        return payload;
    }

    private static byte[] SerializeProgressState(ProgressStateChanged state)
    {
        var payload = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, FromHwnd(state.Hwnd));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), (uint)state.Mode);
        return payload;
    }

    private static byte[] SerializeProgressValue(ProgressValueChanged value)
    {
        var payload = new byte[24];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, FromHwnd(value.Hwnd));
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(8), value.Completed);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(16), value.Total);
        return payload;
    }

    private static byte[] SerializeOverlay(OverlayChanged overlay)
    {
        ValidateOverlay(overlay.Overlay);
        var description = Encoding.UTF8.GetBytes(overlay.Overlay.Description ?? string.Empty);
        if (description.Length > MaxDescriptionBytes) throw new TaskbarCaptureProtocolException("Overlay description is too large.");
        var payload = new byte[checked(20 + description.Length + overlay.Overlay.Rgba.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, FromHwnd(overlay.Hwnd));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), checked((ushort)overlay.Overlay.Width));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), checked((ushort)overlay.Overlay.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), checked((uint)description.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), checked((uint)overlay.Overlay.Rgba.Length));
        description.CopyTo(payload, 20);
        overlay.Overlay.Rgba.CopyTo(payload, 20 + description.Length);
        return payload;
    }

    private static byte[] SerializeOverlayClear(OverlayCleared clear)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, FromHwnd(clear.Hwnd));
        return payload;
    }

    private static void ValidateOverlay(TaskbarOverlay overlay)
    {
        if (overlay.Width <= 0 || overlay.Height <= 0 || overlay.Width > TaskbarOverlay.MaxDimension || overlay.Height > TaskbarOverlay.MaxDimension)
            throw new TaskbarCaptureProtocolException("Overlay dimensions are invalid.");
        if (overlay.Rgba.Length != checked(overlay.Width * overlay.Height * 4))
            throw new TaskbarCaptureProtocolException("Overlay RGBA length is invalid.");
    }

    private static MessageType TypeFor(TaskbarCaptureMessage message) => message switch
    {
        TaskbarHello => MessageType.Hello,
        ProgressStateChanged => MessageType.SetProgressState,
        ProgressValueChanged => MessageType.SetProgressValue,
        OverlayChanged => MessageType.SetOverlayIcon,
        OverlayCleared => MessageType.ClearOverlayIcon,
        TaskbarProcessExited => MessageType.ProcessExiting,
        _ => throw new TaskbarCaptureProtocolException("Unsupported taskbar capture message."),
    };

    private static void RequireLength(ReadOnlySpan<byte> payload, int length)
    {
        if (payload.Length != length) throw new TaskbarCaptureProtocolException("Taskbar payload has an invalid length.");
    }

    private static nint ToHwnd(ulong value) => unchecked((nint)(long)value);
    private static ulong FromHwnd(nint value) => unchecked((ulong)(long)value);

    private enum MessageType : ushort
    {
        Hello = 1,
        SetProgressState = 2,
        SetProgressValue = 3,
        SetOverlayIcon = 4,
        ClearOverlayIcon = 5,
        ProcessExiting = 6,
    }
}
