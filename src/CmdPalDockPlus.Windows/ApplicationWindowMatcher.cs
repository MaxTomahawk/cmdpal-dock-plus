using CmdPalDockPlus.Core.Profiles;

namespace CmdPalDockPlus.Windows;

public static class ApplicationWindowMatcher
{
    public static bool Matches(ApplicationMatch application, WindowSnapshot window)
    {
        if (!string.IsNullOrWhiteSpace(application.ExecutablePath)
            && string.Equals(
                Path.GetFileName(application.ExecutablePath),
                window.ExecutableName,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(application.Aumid)
            && !string.IsNullOrWhiteSpace(window.AppUserModelId)
            && string.Equals(application.Aumid, window.AppUserModelId, StringComparison.OrdinalIgnoreCase);
    }
}
