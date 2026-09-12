# LegacyLocalizer

`LegacyLocalizer` is a small localization helper for BigCityLegacy custom UI and stock game localization overrides.

The game currently has two relevant languages: English and Russian. Use `LegacyLocalizedText` anywhere a custom UI element needs a short localized string.

## Custom text

```csharp
string label = LegacyLocalizer.Text("Character shadows", "Тени персонажей");
```

Or store text for reuse:

```csharp
LegacyLocalizedText title = new LegacyLocalizedText("About BigCityLegacy", "О BigCityLegacy");
string current = title.Get();
```

If the string for the current language in `LegacyLocalizedText` is empty, the string for the other language will be used.


## Stock localization replacement

A stock localization key replacement can be registered like this:

```csharp
LegacyLocalizer.RegisterReplacement(
    "SomeLocalizationKey",
    new LegacyLocalizedText("English text", "Русский текст"));
```

Or like this, if the same string should be used for all languages:

```csharp
LegacyLocalizer.RegisterReplacement(
    "YetAnotherLocalizationKey",
    new LegacyLocalizedText.Same($"Version: {myVersion}"));
```

The replacement is applied to all loaded `LocalizationManager.Lang` instances. Already loaded `Localize` components are updated automatically.

Default BigCityLegacy replacements are registered by `LegacyLocalizer.RegisterDefaultReplacements()` during plugin startup.
