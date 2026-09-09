using UnityEngine;

internal static class LegacyCustomSettings
{
    private const string F6SpawnModeKey = "BigCityLegacy.F6_SpawnMode";
    private const string PointToolToggle = "BigCityLegacy.PointToolToggle";

    internal static void Register()
    {
        LegacySettingsMenuHelper.RegisterToggle(
            id: "user.master_autoresolve",
            group: "User",
            position: new Vector2(0f, -312f),
            label: new LegacyLocalizedText("Resolve servers list on startup", "Получать список серверов при запуске"),
            binding: LegacyBoolSettingBinding.PlayerPrefs(
                key: "ModMaster_AutoResolve",
                defaultValue: true));

        if (!PlayerPrefs.HasKey(F6SpawnModeKey))
        {
            PlayerPrefs.SetInt(F6SpawnModeKey, 1);
            PlayerPrefs.Save();
        }

        LegacySettingsMenuHelper.RegisterTextChoice(
            id: "user.f6_spawnmode",
            group: "User",
            position: new Vector2(0f, -357f),
            label: new LegacyLocalizedText("F6 spawns last car selected:", "F6 создаёт последнюю машину:"),
            binding: LegacyTextChoiceSettingBinding.PlayerPrefs(
                key: F6SpawnModeKey,
                defaultIndex: 1,
                values: new[]
                {
                    new LegacyLocalizedText("In phone", "Из телефона"),
                    new LegacyLocalizedText("In garage", "Из гаража"),
                }));

        if (!PlayerPrefs.HasKey(PointToolToggle))
        {
            PlayerPrefs.SetInt(PointToolToggle, 0);
            PlayerPrefs.Save();
        }

        LegacySettingsMenuHelper.RegisterToggle(
            id: "user.PointToolToggle",
            group: "User",
            position: new Vector2(0f, -400f),
            label: new LegacyLocalizedText("Point Tool v2", "Point Tool v2"),
            binding: LegacyBoolSettingBinding.PlayerPrefs(
                key: PointToolToggle,
                defaultValue: false,
                onChanged: value => LegacyPointTool.InitIfNeeded()));

        LegacyShadowFix.EnsureDefault();
        LegacySettingsMenuHelper.RegisterToggle(
            id: "render.shadow_fix",
            group: "Render",
            position: new Vector2(0f, -612f),
            label: new LegacyLocalizedText("Character shadow", "Тень от персонажа"),
            binding: new LegacyBoolSettingBinding
            {
                Get = () => LegacyShadowFix.IsEnabled,
                Set = LegacyShadowFix.SetEnabled
            });

        LegacySettingsMenuHelper.RegisterButton(
            id: "user.about",
            group: "User",
            position: new Vector2(0f, -520f),
            label: new LegacyLocalizedText("About BigCityLegacy", "О BigCityLegacy"),
            action: LegacyAboutWindow.Show);

        // Use HideStockItem ONLY AFTER registering custom elements.
        LegacySettingsMenuHelper.HideStockItem("User", "Restore Purchases");
        LegacySettingsMenuHelper.HideStockItem("User", "Change Button Poses");
    }
}
