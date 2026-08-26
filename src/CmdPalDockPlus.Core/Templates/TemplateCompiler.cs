using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CmdPalDockPlus.Core.Templates;

public sealed class TemplateParseException(string message) : FormatException(message);

public static partial class TemplateCompiler
{
    private static readonly Regex ExpressionRegex = new(
        @"^\s*(?<primary>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+)(?:\s*\?\?\s*(?<fallback>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+))?(?::(?<format>[A-Za-z0-9._-]+))?\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static CompiledTemplate Compile(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var segments = new List<TemplateSegment>();
        var dependencies = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cursor = 0;

        while (cursor < template.Length)
        {
            var open = template.IndexOf('{', cursor);
            if (open < 0)
            {
                segments.Add(new LiteralSegment(template[cursor..]));
                break;
            }

            if (open > cursor)
            {
                segments.Add(new LiteralSegment(template[cursor..open]));
            }

            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                throw new TemplateParseException("Unclosed template expression.");
            }

            var inner = template[(open + 1)..close];
            var match = ExpressionRegex.Match(inner);
            if (!match.Success)
            {
                throw new TemplateParseException($"Invalid expression: {{{inner}}}");
            }

            var primary = match.Groups["primary"].Value;
            var fallback = match.Groups["fallback"].Success ? match.Groups["fallback"].Value : null;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
            segments.Add(new ExpressionSegment(primary, fallback, format));
            AddDependency(primary);
            if (fallback is not null)
            {
                AddDependency(fallback);
            }

            cursor = close + 1;
        }

        if (template.Length == 0)
        {
            segments.Add(new LiteralSegment(string.Empty));
        }

        return new CompiledTemplate(segments, dependencies);

        void AddDependency(string field)
        {
            if (seen.Add(field))
            {
                dependencies.Add(field);
            }
        }
    }
}

public sealed class CompiledTemplate
{
    private readonly IReadOnlyList<TemplateSegment> _segments;

    internal CompiledTemplate(IReadOnlyList<TemplateSegment> segments, IReadOnlyList<string> dependencies)
    {
        _segments = segments;
        Dependencies = dependencies;
    }

    public IReadOnlyList<string> Dependencies { get; }

    public string Evaluate(IReadOnlyDictionary<string, object?> values)
    {
        var builder = new StringBuilder();
        foreach (var segment in _segments)
        {
            switch (segment)
            {
                case LiteralSegment literal:
                    builder.Append(literal.Text);
                    break;
                case ExpressionSegment expression:
                    values.TryGetValue(expression.Primary, out var value);
                    if (value is null && expression.Fallback is not null)
                    {
                        values.TryGetValue(expression.Fallback, out value);
                    }

                    if (value is null)
                    {
                        break;
                    }

                    if (expression.Format is not null && value is IFormattable formattable)
                    {
                        builder.Append(formattable.ToString(expression.Format, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}

internal abstract record TemplateSegment;
internal sealed record LiteralSegment(string Text) : TemplateSegment;
internal sealed record ExpressionSegment(string Primary, string? Fallback, string? Format) : TemplateSegment;
