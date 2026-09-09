/// <summary>
/// Use it for replace default localization strings
/// </summary>

public static class LegacyDefaultReplacements
{
    private static bool defaultsRegistered;

    private static bool isDisabled = true;

    public static void Register()
    {
        if (defaultsRegistered || isDisabled)
        {
            return;
        }

        defaultsRegistered = true;
        
        // Replacement examples
        // !!! set 'isDisabled = false' to use this section
        LegacyLocalizer.RegisterReplacement("PLAY", new LegacyLocalizedText("Yo!", "Че каво?"));
        LegacyLocalizer.RegisterReplacement("Exit Game", LegacyLocalizedText.Same("Do u know da way?"));

        LegacyLocalizer.ApplyRegisteredReplacements(relocalize: false);
    }
}
