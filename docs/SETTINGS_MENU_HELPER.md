# LegacySettingsMenuHelper

`LegacySettingsMenuHelper` — небольшой helper для добавления собственных пунктов в оригинальное меню настроек BigCityLegacy/MadOut2.

Оригинальная игра строит меню настроек из `MenuGroup`, внутри которых лежат элементы-наследники `MenuButton`: `MenuSettingBool`, `MenuSettingSlider`, `MenuSettingText` и похожие пункты. Их логика обычно привязана к настройкам из `SettingsValues.User`: настройка инициализирует UI-элемент, а затем получает новые значения через `SetValue`.

Helper использует тот же визуальный слой меню, но не требует создавать новые вложенные классы внутри `SettingsValues.User`. При появлении нужной `MenuGroup` он:

1. находит в ней существующий элемент подходящего типа;
2. клонирует его как визуальный шаблон;
3. оставляет оригинальный `MenuButton` / `MenuSetting*` behaviour включённым;
4. ставит `isSkipSaves = true`, чтобы оригинальная логика не искала пункт в `SettingsValues.User`;
5. подключает к клону BigCityLegacy binding или callback через небольшой bridge-компонент;
6. регистрирует клон в `MenuGroup`, чтобы сохранились штатные hover/pressed/selected visual states;
7. размещает элемент в указанной позиции или sibling index.

Клики обрабатываются оригинальными `MenuPress` / `MenuButton`-компонентами игры. Helper только синхронизирует значение между оригинальным UI-элементом и пользовательским binding. Благодаря этому кнопки, переключатели и пункты выбора выглядят и подсвечиваются как стандартные элементы меню.

Это позволяет быстро добавлять пункты меню без ручной сборки Unity prefab и без патча оригинальной `SettingsValues.User`.

## Файлы

```text
src/BigCityLegacy/Client/Settings/LegacySettingsMenuHelper.cs
src/BigCityLegacy/Patches/SettingsMenuPatches.cs
```

Патч `SettingsMenuPatches` вызывает helper при `Awake`, `Start` и `OnEnable` у `MenuGroup`, поэтому элементы можно регистрировать из `Awake()` плагина или из ранней инициализации модуля.

## Быстрый пример: toggle в PlayerPrefs

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

Вызов:

```csharp
private void Awake()
{
    MySettings.Register();
}
```

Значение хранится в:

```text
PlayerPrefs["MyMod.DebugOverlay"] = 0 / 1
```

## Быстрый пример: slider в PlayerPrefs

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

Значение хранится в:

```text
PlayerPrefs["MyMod.MusicVolume"] = float
```

## Button

Кнопка не хранит настройку, а просто выполняет действие.

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

`TextChoice` работает как пункт, который перебирает несколько строковых значений по клику. Это ближе всего к оригинальному `MenuSettingText`.

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

## Расширенная регистрация

Для большего контроля можно создать `LegacySettingsMenuItem` вручную:

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

`Group` сопоставляется с именем `MenuGroup` или полным путём объекта в сцене. По умолчанию используется `NameContains`: достаточно указать часть имени группы.

Стандартные группы оригинального меню:

```text
User
Render
Sound
Keyboard
JoyStick
TODO
```

Доступные режимы:

```csharp
LegacySettingsGroupMatchMode.NameContains
LegacySettingsGroupMatchMode.NameEquals
LegacySettingsGroupMatchMode.PathContains
LegacySettingsGroupMatchMode.PathEquals
```

Если не получается подобрать группу, временно включи логирование:

```csharp
LegacySettingsMenuHelper.DebugLogging = true;
```

После открытия меню настроек в логе появятся найденные `MenuGroup` пути.

### TemplateNameContains

Обычно helper сам выбирает шаблон:

```text
Toggle     -> MenuSettingBool
Slider     -> MenuSettingSlider
TextChoice -> MenuSettingText
Button     -> MenuButtonEvent
```

Если в конкретной группе есть несколько похожих элементов, можно указать `TemplateNameContains`, чтобы клонировать конкретный пункт.

`Button` использует только оригинальный `MenuButtonEvent`. Helper больше не использует fallback на `MenuSettingText` или `MenuSettingBool`, поэтому action-button не должен выглядеть как переключатель или текстовый выбор. Если в группе нет `MenuButtonEvent`, элемент будет пропущен с warning вместо создания неправильного пункта.

## Важные ограничения

Helper специально не пытается модифицировать `SettingsValues.User` напрямую. Вместо этого на клонах включается `isSkipSaves`, а значения зеркалируются в binding. Для модульного кода безопаснее иметь свой binding поверх `PlayerPrefs`, файла конфига или runtime-полей.

Слайдер использует оригинальный `MenuSettingSlider` и его `SliderRoot`: значение меняется только при клике/перетаскивании по самой полосе слайдера, а не по всей ширине пункта. Для float-значений helper мапит пользовательский диапазон на внутреннюю шкалу `0..SliderSteps`; по умолчанию `SliderSteps = 100`. При необходимости можно указать другое разрешение:

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

Также helper не создаёт UI с нуля. Он клонирует уже существующие пункты меню, поэтому внешний вид автоматически совпадает с оригинальным меню игры. Если в выбранной группе нет подходящего шаблона, элемент будет пропущен с warning в лог.

Для больших необязательных функций лучше регистрировать пункты из отдельного модуля/плагина, а не раздувать основной BigCityLegacy plugin.
