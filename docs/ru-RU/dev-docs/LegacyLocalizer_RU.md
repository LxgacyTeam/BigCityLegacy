# LegacyLocalizer

`LegacyLocalizer` - это небольшой вспомогательный инструмент локализации для пользовательского интерфейса BigCityLegacy и переопределения стандартной локализации игры.

В настоящее время в игре используются два основных языка: английский и русский. Используйте `LegacyLocalizedText` везде, где пользовательскому интерфейсу требуется небольшая локализованная строка.

## Пользовательский текст

```csharp
string label = LegacyLocalizer.Text("Character shadows", "Тени персонажей");
```

Либо сохраните текст для повторного использования:

```csharp
LegacyLocalizedText title = new LegacyLocalizedText("About BigCityLegacy", "О BigCityLegacy");
string current = title.Get();
```

Если строка для текущего языка в `LegacyLocalizedText` пуста, будет использована строка на другом языке.


## Замена стандартной локализации

Зарегистрировать замену стандартного ключа локализации можно следующим образом:

```csharp
LegacyLocalizer.RegisterReplacement(
    "SomeLocalizationKey",
    new LegacyLocalizedText("English text", "Русский текст"));
```

Либо так, если для всех языков нужно использовать одну и ту же строку:

```csharp
LegacyLocalizer.RegisterReplacement(
    "YetAnotherLocalizationKey",
    new LegacyLocalizedText.Same($"Version: {myVersion}"));
```

Замена применяется ко всем загруженным экземплярам `LocalizationManager.Lang`. Уже загруженные компоненты `Localize` обновляются автоматически.

Стандартные замены BigCityLegacy регистрируются методом `LegacyLocalizer.RegisterDefaultReplacements()` во время запуска плагина.
