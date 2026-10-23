using UnityEngine;

internal static class LegacyCustomSettings
{
    private const string F6SpawnModeKey = "BigCityLegacy.F6_SpawnMode";
    private const string PointToolToggle = "BigCityLegacy.PointToolToggle";

    internal static void Register()
    {
        LegacyMasterClient.EnsureStartupMode();
        LegacySettingsMenuHelper.RegisterTextChoice(
            id: "user.master_autoresolve",
            group: "User",
            position: new Vector2(0f, -312f),
            label: new LegacyLocalizedText("Resolve servers list on startup", "Получать список серверов при запуске"),
            binding: LegacyTextChoiceSettingBinding.PlayerPrefs(
                key: LegacyMasterClient.StartupModeKey,
                defaultIndex: 1,
                values: new[]
                {
                    new LegacyLocalizedText("No", "Нет"),
                    new LegacyLocalizedText("Official", "Официальный"),
                    new LegacyLocalizedText("Last", "Последний")
                }));

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

        LegacySettingsMenuHelper.RegisterToggle(
            id: "user.remote_head_look",
            group: "User",
            position: new Vector2(0f, -445f),
            label: new LegacyLocalizedText("Character head movement", "Поворот головы персонажей"),
            binding: new LegacyBoolSettingBinding
            {
                Get = () => LegacyRemoteLook.Enabled,
                Set = value => LegacyRemoteLook.Enabled = value
            });

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
        LegacyWeatherLock.EnsureDefault();

        LegacySettingsMenuItem weatherLock = LegacySettingsMenuHelper.RegisterToggle(
            id: "todo.weather_lock",
            group: "TODO",
            position: Vector2.zero,
            label: new LegacyLocalizedText("Lock weather change", "Отключить смену погоды"),
            binding: new LegacyBoolSettingBinding
            {
                Get = () => LegacyWeatherLock.IsEnabled,
                Set = LegacyWeatherLock.SetEnabled,
                OnLocalizedText = new LegacyLocalizedText("On", "Да"),
                OffLocalizedText = new LegacyLocalizedText("Off", "Выкл")
            });
        weatherLock.UseAnchoredPosition = false;
        weatherLock.OnCreated = PlaceWeatherLockUnderWeatherRow;

        LegacySettingsMenuItem profile = LegacySettingsMenuHelper.RegisterButton(
            id: "todo.player_profile",
            group: "TODO",
            position: Vector2.zero,
            label: new LegacyLocalizedText("Player profile", "Профиль игрока"),
            action: LegacyProfileWindow.Show);
        profile.UseAnchoredPosition = false;
        profile.OnCreated = PlaceProfileUnderWeatherLock;

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

    private static void PlaceProfileUnderWeatherLock(GameObject clone)
    {
        if (!clone || !clone.transform.parent) return;
        Transform anchor = clone.transform.parent.Find("BigCityLegacySetting_todo.weather_lock") ?? clone.transform.parent.Find("TimeWeather");
        RectTransform row = anchor as RectTransform, target = clone.transform as RectTransform;
        if (!row || !target) return;
        float shift = row.sizeDelta.y + 8f;
        target.anchoredPosition = row.anchoredPosition - new Vector2(0f, shift);
        foreach (Transform sibling in clone.transform.parent)
        {
            if (sibling == anchor || sibling == clone.transform) continue;
            RectTransform rect = sibling as RectTransform;
            if (rect && rect.anchoredPosition.y < row.anchoredPosition.y - 1f) rect.anchoredPosition -= new Vector2(0f, shift);
        }
    }

    private static void PlaceWeatherLockUnderWeatherRow(GameObject clone)
    {
        if (!clone) return;
        Transform parent = clone.transform.parent;
        if (!parent) return;
        Transform anchor = parent.Find("TimeWeather");
        if (!anchor)
        {
            anchor = parent.Find("BonusBoxesInRace");
        }
        if (!anchor) return;
        RectTransform anchorRect = anchor as RectTransform;
        RectTransform cloneRect = clone.transform as RectTransform;
        if (!anchorRect || !cloneRect) return;
        float shift = anchorRect.sizeDelta.y + 8f;
        cloneRect.anchoredPosition = new Vector2(anchorRect.anchoredPosition.x, anchorRect.anchoredPosition.y - shift);
        foreach (Transform sib in parent)
        {
            if (sib == anchor || sib == clone.transform) continue;
            RectTransform r = sib as RectTransform;
            if (r && r.anchoredPosition.y < anchorRect.anchoredPosition.y - 1f)
            {
                r.anchoredPosition -= new Vector2(0f, shift);
            }
        }
    }

}
