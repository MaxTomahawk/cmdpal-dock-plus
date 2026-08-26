using CmdPalDockPlus.Core.Profiles;

namespace CmdPalDockPlus.Windows.Destinations;

public enum DestinationKind
{
    Recent,
    Frequent,
}

public sealed record AppDestination(
    string Id,
    string DisplayName,
    string Path,
    string? Arguments,
    DestinationKind Kind);

public static class DestinationDeduplicator
{
    public static IReadOnlyList<AppDestination> Deduplicate(IEnumerable<AppDestination> destinations, int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AppDestination>();
        foreach (var destination in destinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Path))
            {
                continue;
            }

            var key = CanonicalKey(destination.Path, destination.Arguments);
            if (!seen.Add(key))
            {
                continue;
            }

            result.Add(destination);
            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    private static string CanonicalKey(string target, string? arguments)
    {
        var canonical = target.Trim();
        if (Path.IsPathFullyQualified(canonical))
        {
            try
            {
                canonical = Path.GetFullPath(canonical).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return canonical + "\u001f" + (arguments ?? string.Empty).Trim();
    }
}
