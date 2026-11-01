# API активации плагинов

Клиентский API BigCityLegacy 1.4.2 для MadOut2 9.4 находится в `BigCityLegacy.Api`. Добавьте ссылку на `BigCityLegacy.dll` и BepInEx-зависимость от `com.bigcitylegacy.baseplugin` версии 1.4.2 или выше. API управляет логическим состоянием уже загруженного BepInEx-плагина; загрузку и выгрузку DLL он не выполняет.

## Контракт и явная поддержка

Поддержка определяется на основном классе плагина одновременно атрибутом `[LegacyPluginActivation]` и интерфейсом `ILegacyPluginActivation`. Обязательны `bool IsEnabled { get; }`, `void Enable()` и `void Disable()`. Атрибут не наследуется. `[LegacyPluginActivation(false)]`, отсутствие атрибута или интерфейса означают отсутствие поддержки: BCL не читает состояние и не вызывает методы.

`RequiresRestart` по умолчанию равен `true`. `Enable` и `Disable` сохраняют нужную настройку средствами самого плагина. При перезапуске плагин самостоятельно читает её и выполняет инициализацию. `IsEnabled` сообщает фактическое состояние текущего процесса, а не сохранённое пожелание для следующего запуска. Плагин должен оставаться загруженным в Chainloader и при выключенной функции, иначе BCL не сможет её включить.

Для настоящего переключения без перезапуска укажите `RequiresRestart = false`: методы должны сразу применить состояние, а `IsEnabled` — сразу его отразить. Методы и getter вызываются в главном потоке Unity. Реализации должны быть повторяемыми и завершать работу до возврата; BCL не ожидает внутренних фоновых операций плагина. Не запускайте перезагрузку из `Enable`/`Disable`: BCL применяет всю группу изменений и организует один перезапуск.

## Пример с сохранением настройки

```csharp
using BepInEx;
using BepInEx.Configuration;
using BigCityLegacy.Api;

[BepInPlugin("com.example.map", "Example Map", "1.0.0")]
[BepInDependency("com.bigcitylegacy.baseplugin", "1.4.2")]
[LegacyPluginActivation(RequiresRestart = true)]
public sealed class ExampleMapPlugin : BaseUnityPlugin, ILegacyPluginActivation
{
    private ConfigEntry<bool> enabledSetting;
    private bool activeAtStartup;
    public bool IsEnabled => activeAtStartup;

    private void Awake()
    {
        enabledSetting = Config.Bind("General", "Enabled", true);
        activeAtStartup = enabledSetting.Value;
        if (activeAtStartup)
        {
            // Initialize the plugin's own map/resources here.
        }
    }

    public void Enable() { enabledSetting.Value = true; }
    public void Disable() { enabledSetting.Value = false; }
}
```

Пример показывает контракт, а не реализацию конкретной карты. Тело инициализации и место хранения настройки выбирает автор плагина. При `RequiresRestart = true` значение `activeAtStartup` остаётся прежним до нового запуска.

## Чтение и изменение состояния

`LegacyPlugins.GetStatus(string pluginGuid)` вызывается в главном потоке Unity. Возвращаемый `LegacyPluginStatus` содержит `Guid`, `Name`, `Version`, `Installed`, `SupportsActivation`, `IsEnabled`, `RequiresRestart`. Сначала проверяйте `Installed` и `SupportsActivation`; значение `IsEnabled` достоверно только для поддерживающего API плагина. GUID сравнивается без учёта регистра.

`Task<bool> LegacyPlugins.SetEnabledAsync(string pluginGuid, bool enabled, bool confirm = true)` показывает подтверждение, вызывает нужный метод в главном потоке и при необходимости перезапускает игру через `LegacyGame`. `false` означает отказ пользователя; `true` — завершённое переключение без перезапуска либо принятую попытку перезапуска. Отсутствующий/неподдерживаемый плагин, изменение версии во время подтверждения и ошибки методов завершают Task исключением. Для hot-плагина дополнительно проверяется новое фактическое состояние.

```csharp
LegacyPluginStatus status = LegacyPlugins.GetStatus("com.example.map");
if (status.Installed && status.SupportsActivation && !status.IsEnabled)
{
    bool applied = await LegacyPlugins.SetEnabledAsync(status.Guid, true);
}
```

Не блокируйте главный поток через `.Result` или `.Wait()`; используйте `await`. После `await` из фонового потока не обращайтесь к Unity-объектам без перехода в главный поток. Произвольные изменения, уже выполненные телами методов до исключения, автоматически не откатываются.

## Проверка сервером

Клиент сообщает поддержку API, фактический статус и необходимость перезапуска вместе со списком плагинов. Настройка `controlByStatus` в `PluginsPolicy.json` разрешает учитывать статус для конкретного правила. Без неё сохраняется проверка установки. Если API не поддерживается или статус не передан старым клиентом, также используется обычная проверка установки.

В hardlist совместимый установленный, но выключенный плагин вызывает предложение включить его. В blacklist включённый совместимый плагин вызывает предложение выключить, а выключенный допускается. Ограничения версии сохраняются. Отсутствующий плагин и неподходящая версия требуют обычного исправления установки.

```json
{
  "version": 1,
  "hardlist": [
    { "guid": "com.example.map", "minVersion": "1.0.0", "controlByStatus": true }
  ],
  "blacklist": [
    { "guid": "com.example.othermap", "controlByStatus": true }
  ]
}
```

Сервер завершает попытку подключения до спавна и отправляет ограниченный запрос на изменение. Клиент показывает адрес сервера, названия/GUID плагинов и предупреждение о перезапуске. Только после согласия вызываются локальные методы. Отказ оставляет настройки прежними. После применения нужно подключиться повторно; автоматического реконнекта нет. Подтверждение доставки пакета не означает согласия пользователя.

Все несовместимости должны устраняться переключением статуса; при смешанной ошибке (например, одна карта выключена, другой плагин отсутствует) выводится обычное сообщение без частичного применения. Один запрос содержит до 32 плагинов. Попытка подключения к другому серверу делает старый неподтверждённый запрос недействительным. Сам BCL нельзя управлять через `controlByStatus`; один GUID нельзя одновременно включать в hardlist и blacklist с этой настройкой.

Отчёт является данными клиента и не доказывает отсутствие модификаций. Базовый формат списка остаётся совместимым с прежними серверами, версия online protocol остаётся 94.

[Диалоги и перезапуск](LegacyDialogs_RU.md) · [Сервер](../user-docs/server_RU.md) · [Разработчикам](README.md)
