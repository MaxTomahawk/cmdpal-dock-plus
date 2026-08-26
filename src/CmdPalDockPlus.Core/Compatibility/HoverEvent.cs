using System.Text;
using System.Text.Json;

namespace CmdPalDockPlus.Core.Compatibility;

public enum HoverEventKind
{
    Enter,
    Leave,
}

public readonly record struct HoverRect(int X, int Y, int Width, int Height);
public sealed record HoverEvent(HoverEventKind Kind, string CommandId, HoverRect? Anchor);

public sealed class HoverProtocolException : FormatException
{
    public HoverProtocolException(string message) : base(message) { }
}

public static class HoverEventProtocol
{
    public const int MaxMessageBytes = 8 * 1024;
    public const int Version = 1;

    public static HoverEvent Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxMessageBytes)
        {
            throw new HoverProtocolException("Hover message exceeds 8 KiB.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new HoverProtocolException("Hover message must be an object.");
            if (!root.TryGetProperty("version", out var version) || version.GetInt32() != Version) throw new HoverProtocolException("Unsupported hover protocol version.");
            if (!root.TryGetProperty("kind", out var kindProperty)) throw new HoverProtocolException("Hover message kind is required.");
            if (!root.TryGetProperty("commandId", out var commandProperty)) throw new HoverProtocolException("Hover command id is required.");
            var commandId = commandProperty.GetString();
            if (string.IsNullOrWhiteSpace(commandId)) throw new HoverProtocolException("Hover command id is required.");

            var kindText = kindProperty.GetString();
            if (string.Equals(kindText, "leave", StringComparison.OrdinalIgnoreCase))
            {
                return new HoverEvent(HoverEventKind.Leave, commandId, null);
            }

            if (!string.Equals(kindText, "enter", StringComparison.OrdinalIgnoreCase))
            {
                throw new HoverProtocolException("Unknown hover event kind.");
            }

            var x = RequiredInt(root, "x");
            var y = RequiredInt(root, "y");
            var width = RequiredInt(root, "width");
            var height = RequiredInt(root, "height");
            if (width < 0 || height < 0) throw new HoverProtocolException("Hover rectangle size cannot be negative.");
            return new HoverEvent(HoverEventKind.Enter, commandId, new HoverRect(x, y, width, height));
        }
        catch (HoverProtocolException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new HoverProtocolException($"Invalid hover message: {ex.Message}");
        }
    }

    public static string Serialize(HoverEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        object payload = evt.Kind == HoverEventKind.Enter && evt.Anchor is { } rect
            ? new { version = Version, kind = "enter", commandId = evt.CommandId, x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height }
            : new { version = Version, kind = "leave", commandId = evt.CommandId };
        var json = JsonSerializer.Serialize(payload);
        if (Encoding.UTF8.GetByteCount(json) > MaxMessageBytes) throw new HoverProtocolException("Hover message exceeds 8 KiB.");
        return json;
    }

    private static int RequiredInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property) || !property.TryGetInt32(out var value))
            throw new HoverProtocolException($"Hover field '{name}' is required and must be an integer.");
        return value;
    }
}
