using System.Text.Json;
using CmdPalDockPlus.Core.Actions;
using CmdPalDockPlus.Core.Rules;

namespace CmdPalDockPlus.Core.Profiles;

public sealed class ProfileTextConfigurationException(string message) : FormatException(message);

public static class ProfileTextConfiguration
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static IReadOnlyList<DockRule> ParseRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ProfileTextConfigurationException("Rules must be a JSON array.");

            var result = new List<DockRule>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = RequiredString(element, "id");
                var conditions = new List<RuleCondition>();
                if (!element.TryGetProperty("when", out var when) || when.ValueKind != JsonValueKind.Array)
                    throw new ProfileTextConfigurationException($"Rule '{id}' requires a 'when' array.");
                foreach (var condition in when.EnumerateArray())
                {
                    var field = RequiredString(condition, "field");
                    var opText = RequiredString(condition, "op");
                    if (!Enum.TryParse<RuleOperator>(NormalizeEnum(opText), true, out var op))
                        throw new ProfileTextConfigurationException($"Rule '{id}' has unknown operator '{opText}'.");
                    var expected = condition.TryGetProperty("value", out var value)
                        ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
                        : null;
                    conditions.Add(new RuleCondition(field, op, expected));
                }

                var actions = new List<RuleAction>();
                if (!element.TryGetProperty("then", out var then) || then.ValueKind != JsonValueKind.Array)
                    throw new ProfileTextConfigurationException($"Rule '{id}' requires a 'then' array.");
                foreach (var action in then.EnumerateArray())
                {
                    var actionName = RequiredString(action, "action").Trim().ToLowerInvariant();
                    actions.Add(actionName switch
                    {
                        "group" => new GroupAction(RequiredString(action, "key")),
                        "separate" => new SeparateAction(),
                        "hide" => new HideAction(),
                        "title" => new SetTitleTemplateAction(RequiredString(action, "template")),
                        "subtitle" => new SetSubtitleTemplateAction(RequiredString(action, "template")),
                        "icon" => new SetIconTemplateAction(RequiredString(action, "template")),
                        _ => throw new ProfileTextConfigurationException($"Rule '{id}' has unknown action '{actionName}'."),
                    });
                }

                var rule = new DockRule(id, conditions, actions);
                var errors = RuleValidator.Validate(rule);
                if (errors.Count != 0)
                    throw new ProfileTextConfigurationException($"Rule '{id}' is invalid: {string.Join(", ", errors)}");
                result.Add(rule);
            }
            return result;
        }
        catch (ProfileTextConfigurationException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ProfileTextConfigurationException($"Rules JSON is invalid: {ex.Message}");
        }
    }

    public static IReadOnlyList<UserActionDefinition> ParseActions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ProfileTextConfigurationException("Actions must be a JSON array.");

            var result = new List<UserActionDefinition>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = RequiredString(element, "id");
                var name = RequiredString(element, "name");
                var kindText = RequiredString(element, "kind");
                if (!Enum.TryParse<UserActionKind>(kindText, true, out var kind))
                    throw new ProfileTextConfigurationException($"Action '{id}' has unknown kind '{kindText}'.");
                var action = new UserActionDefinition(
                    id,
                    name,
                    kind,
                    RequiredString(element, "target"),
                    OptionalString(element, "arguments"),
                    OptionalString(element, "workingDirectory"));
                var validation = UserActionValidator.Validate(action);
                if (validation.Errors.Count != 0)
                    throw new ProfileTextConfigurationException($"Action '{id}' is invalid: {string.Join(", ", validation.Errors)}");
                result.Add(action);
            }
            return result;
        }
        catch (ProfileTextConfigurationException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ProfileTextConfigurationException($"Actions JSON is invalid: {ex.Message}");
        }
    }

    public static string FormatRules(IEnumerable<DockRule> rules)
    {
        var model = rules.Select(rule => new
        {
            id = rule.Id,
            when = rule.Conditions.Select(condition => new
            {
                field = condition.FieldId,
                op = OperatorName(condition.Operator),
                value = condition.Expected,
            }),
            then = rule.Actions.Select(ActionModel),
        });
        return JsonSerializer.Serialize(model, SerializerOptions);
    }

    public static string FormatActions(IEnumerable<UserActionDefinition> actions)
        => JsonSerializer.Serialize(
            actions.Select(action => new
            {
                id = action.Id,
                name = action.DisplayName,
                kind = action.Kind.ToString().ToLowerInvariant(),
                target = action.Target,
                arguments = action.Arguments,
                workingDirectory = action.WorkingDirectory,
            }),
            SerializerOptions);

    private static object ActionModel(RuleAction action) => action switch
    {
        GroupAction group => new { action = "group", key = group.Key, template = (string?)null },
        SeparateAction => new { action = "separate", key = (string?)null, template = (string?)null },
        HideAction => new { action = "hide", key = (string?)null, template = (string?)null },
        SetTitleTemplateAction title => new { action = "title", key = (string?)null, template = title.Template },
        SetSubtitleTemplateAction subtitle => new { action = "subtitle", key = (string?)null, template = subtitle.Template },
        SetIconTemplateAction icon => new { action = "icon", key = (string?)null, template = icon.Template },
        _ => throw new ProfileTextConfigurationException($"Unsupported rule action type '{action.GetType().Name}'."),
    };

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            throw new ProfileTextConfigurationException($"Required string '{name}' is missing.");
        var value = property.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ProfileTextConfigurationException($"Required string '{name}' is empty.");
        return value;
    }

    private static string? OptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? string.IsNullOrWhiteSpace(property.GetString()) ? null : property.GetString()
            : null;

    private static string NormalizeEnum(string value)
        => value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);

    private static string OperatorName(RuleOperator value)
        => value.ToString().ToLowerInvariant();
}
