using System.Diagnostics;
using CmdPalDockPlus.Core.Actions;
using CmdPalDockPlus.Core.Profiles;

namespace CmdPalDockPlus.Windows;

public sealed class AppLauncher
{
    public ValueTask LaunchAsync(ApplicationMatch application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            Process.Start(new ProcessStartInfo(application.ExecutablePath) { UseShellExecute = true });
            return ValueTask.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(application.Aumid))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{application.Aumid}") { UseShellExecute = true });
            return ValueTask.CompletedTask;
        }

        throw new InvalidOperationException("Application profile has no launch target.");
    }

    public ValueTask OpenFileLocationAsync(string executablePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{executablePath}\"") { UseShellExecute = true });
        return ValueTask.CompletedTask;
    }

    public ValueTask RunUserActionAsync(UserActionDefinition action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = UserActionValidator.Validate(action);
        if (validation.Errors.Count != 0)
        {
            throw new InvalidOperationException(string.Join(", ", validation.Errors));
        }

        ProcessStartInfo startInfo = action.Kind switch
        {
            UserActionKind.Uri => new ProcessStartInfo(action.Target) { UseShellExecute = true },
            UserActionKind.Shell => new ProcessStartInfo(action.Target, action.Arguments ?? string.Empty) { UseShellExecute = true },
            _ => new ProcessStartInfo(action.Target, action.Arguments ?? string.Empty) { UseShellExecute = false },
        };

        if (!string.IsNullOrWhiteSpace(action.WorkingDirectory))
        {
            startInfo.WorkingDirectory = action.WorkingDirectory;
        }

        Process.Start(startInfo);
        return ValueTask.CompletedTask;
    }
}
