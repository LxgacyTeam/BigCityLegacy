# LegacySettingsMenuHelper

`LegacySettingsMenuHelper` 是一个小型辅助工具，用于向 MadOut2 原有设置菜单添加自定义项目。

原版游戏通过 `MenuGroup` 对象构建设置菜单，这些对象包含派生自 `MenuButton` 的项目，例如 `MenuSettingBool`、`MenuSettingSlider`、`MenuSettingText` 等。它们的逻辑通常与 `SettingsValues.User` 中的设置关联：设置先初始化 UI 元素，然后通过 `SetValue` 接收新值。

该辅助工具使用相同的菜单视觉层，但无需在 `SettingsValues.User` 内创建新的嵌套类。目标 `MenuGroup` 出现时，它会：

1. 在该组中查找类型合适的现有元素；
2. 克隆该元素作为视觉模板；
3. 保持原有 `MenuButton` / `MenuSetting*` 行为启用；
4. 设置 `isSkipSaves = true`，使原有逻辑不再到 `SettingsValues.User` 中查找该项目；
5. 通过一个小型桥接组件，将 BigCityLegacy 绑定或回调连接到克隆对象；
6. 在 `MenuGroup` 中注册克隆对象，保留原有的悬停、按下和选中视觉状态；
7. 将元素放置在指定位置或同级索引处。

点击由游戏原有的 `MenuPress` / `MenuButton` 组件处理。辅助工具仅同步原有 UI 元素与自定义绑定之间的值。因此，按钮、开关和选择项的外观及高亮效果都与标准菜单元素一致。

这样即可快速添加菜单项目，无需手动制作 Unity 预制件，也无需修补原有的 `SettingsValues.User`。

`SettingsMenuPatches` 补丁在 `MenuGroup` 的 `Awake`、`Start` 和 `OnEnable` 中调用辅助工具，因此可以在插件的 `Awake()` 或模块早期初始化阶段注册项目。

## 快速示例：PlayerPrefs 开关

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

调用方式：

```csharp
private void Awake()
{
    MySettings.Register();
}
```

该值存储为：

```text
PlayerPrefs["MyMod.DebugOverlay"] = 0 / 1
```

## 快速示例：PlayerPrefs 滑块

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

该值存储为：

```text
PlayerPrefs["MyMod.MusicVolume"] = float
```

## 按钮（Button）

按钮不存储设置，只执行一个操作。

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

`TextChoice` 是一个点击后在多个字符串值之间循环切换的项目。它最接近原有的 `MenuSettingText`。

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

## 高级注册

如需更精细的控制，可以手动创建 `LegacySettingsMenuItem`：

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

`Group` 用于匹配 `MenuGroup` 名称或场景中的完整对象路径。默认使用 `NameContains`，因此只需指定组名称的一部分。

原版设置菜单的标准分组：

```text
User
Render
Sound
Keyboard
JoyStick
TODO
```

### TemplateNameContains

通常，辅助工具会自动选择模板：

```text
Toggle     -> MenuSettingBool
Slider     -> MenuSettingSlider
TextChoice -> MenuSettingText
Button     -> MenuButtonEvent
```

如果某个分组中存在多个相似元素，可以指定 `TemplateNameContains` 来克隆特定项目。

`Button` 仅使用原有的 `MenuButtonEvent`。如果分组中没有 `MenuButtonEvent`，则跳过该项目并输出警告，而不会创建不正确的项目。

## 隐藏现有项目

可以通过以下方式隐藏原有设置菜单中的项目：

```csharp
LegacySettingsMenuHelper.HideStockItem(group, objectName);
```

`objectName` 是 Unity 场景中的对象名称。设置菜单项目位于 `MenuEsc/Root/Setting` 下。

示例：隐藏总音量滑块：

```csharp
LegacySettingsMenuHelper.HideStockItem("Sound", "Sound_Total");
```

> [!WARNING]
> 必须**在所有自定义项目注册完成之后**再隐藏项目，因为被隐藏的项目实际上会从场景中消失，无法再被克隆。否则，注册可能失败。

## 重要限制

该辅助工具刻意不直接修改 `SettingsValues.User`。克隆对象使用 `isSkipSaves`，并将值同步到绑定中。对于模块化代码，基于 `PlayerPrefs`、配置文件或运行时字段建立自己的绑定更安全。

滑块使用原有的 `MenuSettingSlider` 及其 `SliderRoot`。对于浮点值，辅助工具将自定义范围映射到内部的 `0..SliderSteps` 刻度；默认 `SliderSteps = 100`。如有需要，可以指定其他精度：

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

该辅助工具也不会从零创建 UI，而是克隆现有菜单项目，因此外观会自动匹配原版游戏菜单。如果所选分组中没有合适的模板，会跳过该项目并在日志中输出警告。

对于较大的可选功能，最好从独立模块或插件中注册项目。

---
