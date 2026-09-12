# LegacySettingsMenuHelper

`LegacySettingsMenuHelper` is a small helper for adding custom items to the original MadOut2 settings menu.

The original game builds the settings menu from `MenuGroup` objects containing `MenuButton` descendants: `MenuSettingBool`, `MenuSettingSlider`, `MenuSettingText`, and similar entries. Their logic is usually tied to settings from `SettingsValues.User`: a setting initializes the UI element, and then receives new values through `SetValue`.

The helper uses the same visual menu layer but does not require creating new nested classes inside `SettingsValues.User`. When the target `MenuGroup` appears, it:

1. finds an existing element of a suitable type inside it;
2. clones it as a visual template;
3. keeps the original `MenuButton` / `MenuSetting*` behaviour enabled;
4. sets `isSkipSaves = true` so the original logic does not search for the item in `SettingsValues.User`;
5. connects a BigCityLegacy binding or callback to the clone through a small bridge component;
6. registers the clone in `MenuGroup` so stock hover/pressed/selected visual states are preserved;
7. places the element at the specified position or sibling index.

Clicks are handled by the game's original `MenuPress` / `MenuButton` components. The helper only synchronizes the value between the original UI element and the custom binding. Because of that, buttons, toggles, and selection items look and highlight like standard menu elements.

This makes it possible to quickly add menu items without manually building a Unity prefab and without patching the original `SettingsValues.User`.

The `SettingsMenuPatches` patch calls the helper during `Awake`, `Start`, and `OnEnable` on `MenuGroup`, so items can be registered from a plugin's `Awake()` or from early module initialization.

## Quick example: PlayerPrefs toggle

```csharp
using UnityEngine;

internal static class MySettings
{
    internal static void Register()
    {
        LegacySettingsMenuHelper.RegisterToggle(
            id: "my_mod.debug_overlay",
            group: "Render",
            position: new Vector2(0f, -420f),
            label: "Debug overlay",
            binding: LegacyBoolSettingBinding.PlayerPrefs(
                key: "MyMod.DebugOverlay",
                defaultValue: false,
                onChanged: value =>
                {
                    Debug.Log("Debug overlay: " + value);
                }));
    }
}
```

Call:

```csharp
private void Awake()
{
    MySettings.Register();
}
```

The value is stored in:

```text
PlayerPrefs["MyMod.DebugOverlay"] = 0 / 1
```

## Quick example: PlayerPrefs slider

```csharp
using UnityEngine;

internal static class MySettings
{
    internal static void RegisterVolumeSlider()
    {
        LegacySettingsMenuHelper.RegisterSlider(
            id: "my_mod.music_volume",
            group: "Sound",
            position: new Vector2(0f, -460f),
            label: "Custom music volume",
            binding: LegacyFloatSettingBinding.PlayerPrefs(
                key: "MyMod.MusicVolume",
                defaultValue: 0.75f,
                min: 0f,
                max: 1f,
                wholeNumbers: false,
                format: "0.00",
                onChanged: value =>
                {
                    AudioListener.volume = value;
                }));
    }
}
```

The value is stored in:

```text
PlayerPrefs["MyMod.MusicVolume"] = float
```

## Button

A button does not store a setting. It simply executes an action.

```csharp
LegacySettingsMenuHelper.RegisterButton(
    id: "my_mod.open_folder",
    group: "User",
    position: new Vector2(0f, -500f),
    label: "Open BigCityLegacy folder",
    action: () =>
    {
        Application.OpenURL(Application.dataPath + "/../BepInEx/plugins/BigCityLegacy");
    });
```

## TextChoice

`TextChoice` works as an item that cycles through several string values when clicked. It is closest to the original `MenuSettingText`.

```csharp
LegacySettingsMenuHelper.RegisterTextChoice(
    id: "my_mod.log_level",
    group: "User",
    position: new Vector2(0f, -540f),
    label: "Log level",
    binding: LegacyTextChoiceSettingBinding.PlayerPrefs(
        key: "MyMod.LogLevel",
        defaultValue: "Normal",
        values: new[] { "Quiet", "Normal", "Verbose" },
        onChanged: value =>
        {
            Debug.Log("Log level changed: " + value);
        }));
```

## Advanced registration

For more control, you can create a `LegacySettingsMenuItem` manually:

```csharp
LegacySettingsMenuHelper.Register(new LegacySettingsMenuItem
{
    Id = "my_mod.precise_item",
    Group = "Render",
    GroupMatchMode = LegacySettingsGroupMatchMode.NameContains,
    Type = LegacySettingsMenuItemType.Toggle,
    Label = "Precise item",
    Tooltip = "Optional hint",
    UseAnchoredPosition = true,
    AnchoredPosition = new Vector2(0f, -600f),
    Size = new Vector2(480f, 48f),
    SiblingIndex = -1,
    IgnoreUnityLayout = true,
    TemplateNameContains = "TargetFPS",
    BoolBinding = LegacyBoolSettingBinding.PlayerPrefs("MyMod.PreciseItem", false),
    OnCreated = go =>
    {
        Debug.Log("Created settings item: " + go.name);
    }
});
```

### Group

`Group` is matched against the `MenuGroup` name or the full object path in the scene. By default, `NameContains` is used: specifying a part of the group name is enough.

Standard groups in the original settings menu:

```text
User
Render
Sound
Keyboard
JoyStick
TODO
```

### TemplateNameContains

Normally, the helper chooses a template automatically:

```text
Toggle     -> MenuSettingBool
Slider     -> MenuSettingSlider
TextChoice -> MenuSettingText
Button     -> MenuButtonEvent
```

If a specific group has several similar elements, you can specify `TemplateNameContains` to clone a specific item.

`Button` uses only the original `MenuButtonEvent`. If the group has no `MenuButtonEvent`, the item is skipped with a warning instead of creating an incorrect entry.

## Hiding existing items

Existing original settings menu items can be hidden with:

```csharp
LegacySettingsMenuHelper.HideStockItem(group, objectName);
```

`objectName` is the name of the object in the Unity scene. Settings menu items can be found under `MenuEsc/Root/Setting`.

Example: hiding the total volume slider:

```csharp
LegacySettingsMenuHelper.HideStockItem("Sound", "Sound_Total");
```

> [!WARNING]
> Items should be hidden **strictly after** all custom items are registered, because a hidden item effectively disappears from the scene and can no longer be cloned. Otherwise, registration can fail.

## Important limitations

The helper deliberately does not try to modify `SettingsValues.User` directly. Instead, clones use `isSkipSaves`, and values are mirrored into a binding. For modular code, it is safer to have your own binding over `PlayerPrefs`, a config file, or runtime fields.

The slider uses the original `MenuSettingSlider` and its `SliderRoot`. For float values, the helper maps the custom range to the internal `0..SliderSteps` scale; by default, `SliderSteps = 100`. If needed, you can specify a different resolution:

```csharp
LegacyFloatSettingBinding binding = LegacyFloatSettingBinding.PlayerPrefs(
    key: "MyMod.Value",
    defaultValue: 0.5f,
    min: 0f,
    max: 1f);
binding.SliderSteps = 200;

LegacySettingsMenuHelper.RegisterSlider(
    id: "my_mod.value",
    group: "Render",
    position: new Vector2(0f, -460f),
    label: "My value",
    binding: binding);
```

The helper also does not create UI from scratch. It clones existing menu items, so the appearance automatically matches the original game menu. If the selected group has no suitable template, the item is skipped with a warning in the log.

For large optional features, it is better to register items from a separate module/plugin.
---
