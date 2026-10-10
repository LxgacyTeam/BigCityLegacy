# LegacyLocalizer

`LegacyLocalizer` 是一个小型本地化辅助工具，用于 BigCityLegacy 自定义 UI 以及覆盖游戏原有的本地化内容。

游戏目前涉及的两种语言是英语和俄语。自定义 UI 元素需要简短的本地化字符串时，请使用 `LegacyLocalizedText`。

## 自定义文本

```csharp
string label = LegacyLocalizer.Text("Character shadows", "Тени персонажей");
```

也可以保存文本以便复用：

```csharp
LegacyLocalizedText title = new LegacyLocalizedText("About BigCityLegacy", "О BigCityLegacy");
string current = title.Get();
```

如果 `LegacyLocalizedText` 中当前语言的字符串为空，则使用另一种语言的字符串。

## 替换原有本地化内容

可以按以下方式为原有本地化键注册替换内容：

```csharp
LegacyLocalizer.RegisterReplacement(
    "SomeLocalizationKey",
    new LegacyLocalizedText("English text", "Русский текст"));
```

如果所有语言都应使用同一个字符串，也可以这样写：

```csharp
LegacyLocalizer.RegisterReplacement(
    "YetAnotherLocalizationKey",
    new LegacyLocalizedText.Same($"Version: {myVersion}"));
```

替换会应用于所有已加载的 `LocalizationManager.Lang` 实例。已加载的 `Localize` 组件会自动更新。

BigCityLegacy 的默认替换内容在插件启动期间由 `LegacyLocalizer.RegisterDefaultReplacements()` 注册。
