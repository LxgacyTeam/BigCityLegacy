
using static SettingsValues.User;

/// <summary>
/// Use it for replace default localization strings
/// </summary>

public static class LegacyDefaultReplacements
{
    private static bool defaultsRegistered;

    private static bool isDisabled = false;

    public static void Register()
    {
        if (defaultsRegistered || isDisabled)
        {
            return;
        }

        defaultsRegistered = true;

        LegacyLocalizer.RegisterReplacement("TargetFPS", new LegacyLocalizedText("FPS Limit", "FPS Лимит"));

        LegacyLocalizer.ApplyRegisteredReplacements(relocalize: false);
    }
}
