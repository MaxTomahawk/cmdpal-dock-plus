using System.Globalization;
using System.Text.RegularExpressions;
using CmdPalDockPlus.Core.Templates;

namespace CmdPalDockPlus.Core.Rules;

public enum RuleOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    Regex,
    Exists,
    Missing,
    GreaterThan,
    LessThan,
    True,
    False,
}

public sealed record RuleCondition(string FieldId, RuleOperator Operator, string? Expected = null);

public abstract record RuleAction;
public sealed record GroupAction(string Key) : RuleAction;
public sealed record SeparateAction : RuleAction;
public sealed record HideAction : RuleAction;
public sealed record SetTitleTemplateAction(string Template) : RuleAction;
public sealed record SetSubtitleTemplateAction(string Template) : RuleAction;
public sealed record SetIconTemplateAction(string Template) : RuleAction;

public sealed record DockRule(string Id, IReadOnlyList<RuleCondition> Conditions, IReadOnlyList<RuleAction> Actions);

public enum RuleGrouping
{
    None,
    Group,
    Separate,
    Hidden,
}

public sealed record RuleEvaluationResult(
    RuleGrouping Grouping,
    string? GroupKey,
    string? TitleTemplate,
    string? SubtitleTemplate,
    string? IconTemplate);

public static class RuleEvaluator
{
    public static RuleEvaluationResult Evaluate(IEnumerable<DockRule> rules, IReadOnlyDictionary<string, object?> values)
    {
        var grouping = RuleGrouping.None;
        string? groupKey = null;
        string? title = null;
        string? subtitle = null;
        string? icon = null;

        foreach (var rule in rules)
        {
            if (!rule.Conditions.All(condition => Matches(condition, values)))
            {
                continue;
            }

            foreach (var action in rule.Actions)
            {
                switch (action)
                {
                    case SetTitleTemplateAction setTitle:
                        title = setTitle.Template;
                        break;
                    case SetSubtitleTemplateAction setSubtitle:
                        subtitle = setSubtitle.Template;
                        break;
                    case SetIconTemplateAction setIcon:
                        icon = setIcon.Template;
                        break;
                    case GroupAction group:
                        grouping = RuleGrouping.Group;
                        groupKey = group.Key;
                        return new(grouping, groupKey, title, subtitle, icon);
                    case SeparateAction:
                        grouping = RuleGrouping.Separate;
                        return new(grouping, groupKey, title, subtitle, icon);
                    case HideAction:
                        grouping = RuleGrouping.Hidden;
                        return new(grouping, groupKey, title, subtitle, icon);
                }
            }
        }

        return new(grouping, groupKey, title, subtitle, icon);
    }

    public static bool Matches(RuleCondition condition, IReadOnlyDictionary<string, object?> values)
    {
        values.TryGetValue(condition.FieldId, out var actual);
        return condition.Operator switch
        {
            RuleOperator.Exists => actual is not null,
            RuleOperator.Missing => actual is null,
            RuleOperator.True => actual is true || string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), "true", StringComparison.OrdinalIgnoreCase),
            RuleOperator.False => actual is false || string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), "false", StringComparison.OrdinalIgnoreCase),
            RuleOperator.GreaterThan => CompareNumber(actual, condition.Expected, static (a, b) => a > b),
            RuleOperator.LessThan => CompareNumber(actual, condition.Expected, static (a, b) => a < b),
            RuleOperator.Regex => RegexMatches(actual, condition.Expected),
            _ => CompareString(actual, condition.Expected, condition.Operator),
        };
    }

    private static bool CompareString(object? actual, string? expected, RuleOperator op)
    {
        var actualText = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? string.Empty;
        expected ??= string.Empty;
        return op switch
        {
            RuleOperator.Equals => string.Equals(actualText, expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotEquals => !string.Equals(actualText, expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.Contains => actualText.Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotContains => !actualText.Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.StartsWith => actualText.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.EndsWith => actualText.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool CompareNumber(object? actual, string? expected, Func<double, double, bool> comparison)
    {
        return double.TryParse(Convert.ToString(actual, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var left)
            && double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var right)
            && comparison(left, right);
    }

    private static bool RegexMatches(object? actual, string? pattern)
    {
        if (pattern is null)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(
                Convert.ToString(actual, CultureInfo.InvariantCulture) ?? string.Empty,
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

public static class RuleValidator
{
    public static IReadOnlyList<string> Validate(DockRule rule)
    {
        var errors = new List<string>();
        foreach (var condition in rule.Conditions.Where(c => c.Operator == RuleOperator.Regex))
        {
            try
            {
                _ = new Regex(condition.Expected ?? string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException)
            {
                errors.Add($"rule.regex.invalid:{rule.Id}:{condition.FieldId}");
            }
        }

        foreach (var group in rule.Actions.OfType<GroupAction>())
        {
            try
            {
                _ = TemplateCompiler.Compile(group.Key);
            }
            catch (TemplateParseException)
            {
                errors.Add($"rule.template.invalid:{rule.Id}:group");
            }
        }

        return errors;
    }
}
