using UnityEngine;

// Usage example only. Copy the needed parts into a BigCityLegacy module/plugin and call Register() from Awake().
internal static class SettingsMenuExamples
{
    internal static void Register()
    {
        LegacySettingsMenuHelper.RegisterToggle(
            id: "example.show_coordinates",
            group: "Render",
            position: new Vector2(0f, -420f),
            label: "Show coordinates",
            binding: LegacyBoolSettingBinding.PlayerPrefs(
                key: "Example.ShowCoordinates",
                defaultValue: false,
                onChanged: value => Debug.Log("Show coordinates: " + value)));

        LegacySettingsMenuHelper.RegisterSlider(
            id: "example.console_alpha",
            group: "Render",
            position: new Vector2(0f, -470f),
            label: "Console alpha",
            binding: LegacyFloatSettingBinding.PlayerPrefs(
                key: "Example.ConsoleAlpha",
                defaultValue: 0.85f,
                min: 0.25f,
                max: 1f,
                wholeNumbers: false,
                format: "0.00",
                onChanged: value => Debug.Log("Console alpha: " + value)));

        LegacySettingsMenuHelper.RegisterButton(
            id: "example.reset_ui",
            group: "User",
            position: new Vector2(0f, -520f),
            label: "Reset example UI",
            action: () =>
            {
                PlayerPrefs.DeleteKey("Example.ShowCoordinates");
                PlayerPrefs.DeleteKey("Example.ConsoleAlpha");
                PlayerPrefs.Save();
                Debug.Log("Example settings reset.");
            });
    }
}
