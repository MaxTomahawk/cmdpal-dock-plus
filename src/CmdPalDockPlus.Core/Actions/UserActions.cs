namespace CmdPalDockPlus.Core.Actions;

public enum UserActionKind
{
    Process,
    Uri,
    Shell,
}

public sealed record UserActionDefinition(
    string Id,
    string DisplayName,
    UserActionKind Kind,
    string Target,
    string? Arguments,
    string? WorkingDirectory);

public sealed record UserActionValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public static class UserActionValidator
{
    public static UserActionValidationResult Validate(UserActionDefinition action)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(action.Id))
        {
            errors.Add("action.id.required");
        }

        if (string.IsNullOrWhiteSpace(action.DisplayName))
        {
            errors.Add("action.displayName.required");
        }

        if (string.IsNullOrWhiteSpace(action.Target))
        {
            errors.Add("action.target.required");
        }

        if (action.Kind == UserActionKind.Shell)
        {
            warnings.Add("action.shell.explicit-risk");
        }

        if (action.Kind == UserActionKind.Uri && !Uri.TryCreate(action.Target, UriKind.Absolute, out _))
        {
            errors.Add("action.uri.invalid");
        }

        return new(errors, warnings);
    }
}
