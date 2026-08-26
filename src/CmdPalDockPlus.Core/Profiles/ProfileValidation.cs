using CmdPalDockPlus.Core.Actions;
using CmdPalDockPlus.Core.Rules;
using CmdPalDockPlus.Core.Templates;

namespace CmdPalDockPlus.Core.Profiles;

public sealed record ProfileValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public static class ProfileValidator
{
    public static ProfileValidationResult Validate(AppProfile profile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            errors.Add("profile.id.required");
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            errors.Add("profile.displayName.required");
        }

        if (string.IsNullOrWhiteSpace(profile.Application.ExecutablePath) && string.IsNullOrWhiteSpace(profile.Application.Aumid))
        {
            errors.Add("application.target.required");
        }

        TryTemplate(profile.Display.Title, "display.title.invalid", errors);
        TryTemplate(profile.Display.Subtitle, "display.subtitle.invalid", errors);
        if (!string.IsNullOrWhiteSpace(profile.Display.Icon))
        {
            TryTemplate(profile.Display.Icon!, "display.icon.invalid", errors);
        }

        foreach (var rule in profile.Rules)
        {
            foreach (var error in RuleValidator.Validate(rule))
            {
                errors.Add(error);
            }
        }

        foreach (var action in profile.UserActions)
        {
            var validation = UserActionValidator.Validate(action);
            errors.AddRange(validation.Errors);
            warnings.AddRange(validation.Warnings);
        }

        return new(errors, warnings);
    }

    public static IReadOnlyList<string> ValidateDocument(ProfileDocument document)
    {
        var errors = new List<string>();
        if (document.SchemaVersion != ProfileDocument.CurrentSchemaVersion)
        {
            errors.Add($"schema.unsupported:{document.SchemaVersion}");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in document.Profiles)
        {
            if (!ids.Add(profile.Id))
            {
                errors.Add($"profile.id.duplicate:{profile.Id}");
            }

            errors.AddRange(Validate(profile).Errors.Select(e => $"{profile.Id}:{e}"));
        }

        return errors;
    }

    private static void TryTemplate(string template, string code, ICollection<string> errors)
    {
        try
        {
            _ = TemplateCompiler.Compile(template);
        }
        catch (TemplateParseException)
        {
            errors.Add(code);
        }
    }
}
