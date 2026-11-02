# LegacyHelpers

`LegacyHelpers` — набор helper-методов BigCityLegacy для проверок состояния игры, работы с путями, временем и ресурсами, безопасного вызова методов через reflection и выполнения типовых рутинных задач.

Класс публичный и находится в глобальном пространстве имён. Для внешнего плагина нужна ссылка на BigCityLegacy.dll и зависимость BepInEx от `com.bigcitylegacy.baseplugin`. Члены с доступом `internal` отмечены в своих разделах; их примеры относятся к коду самого BCL. Методы, обращающиеся к Unity API, вызывайте в главном потоке.

---

# `ModDataPath`

Возвращает путь к папке BigCityLegacy, располагающейся в корневой папке игры.

Сигнатура:

```csharp
public static string ModDataPath { get; }
```

При каждом обращении свойство проверяет каталог и пытается создать его, если он отсутствует. При ошибке создания пишет Error в Unity log, но всё равно возвращает путь.

## Пример

```csharp
string data = LegacyHelpers.ModDataPath;
string config = Path.Combine(data, "config");
```

## Когда использовать

Папка `BigCityLegacy` используется для хранения пользовательских данных мода: конфиги, JSON-файлы, вспомогательные директории, локальные ресурсы мода.

---

# `GetCurTimestamp`

Возвращает текущую дату и время в строковом формате, подходящем для имени файла.

Сигнатура:

```csharp
public static string GetCurTimestamp { get; }
```

При каждом обращении берёт `DateTime.Now` и форматирует его как `yyyy-MM-dd_HH-mm-ss-fff`, например `2026-10-31_14-25-36-123`. Используется локальное время операционной системы.

## Пример

```csharp
string stamp = LegacyHelpers.GetCurTimestamp;
string filename = "backup_" + stamp + ".json";
```

## Когда использовать

Подходит для имён экспортируемых файлов, резервных копий и диагностических снимков.

## Важно

Это свойство, а не метод и не Unix timestamp. Серверный логгер BCL использует UTC независимо от этого helper.

---

# `FormatSeconds(long seconds)`

Преобразует длительность в секундах в короткую строку.

Сигнатура:

```csharp
public static string FormatSeconds(long seconds)
```

Использует `TimeSpan.FromSeconds(seconds)` и выбирает до двух единиц времени:

- от суток — дни и часы;
- от часа — часы и минуты;
- от минуты — минуты и секунды;
- меньше минуты — секунды.

Сокращения фиксированы: `d.`, `h.`, `min.`, `sec.`; язык игры их не меняет.

## Пример

```csharp
string a = LegacyHelpers.FormatSeconds(45);     // 45 sec.
string b = LegacyHelpers.FormatSeconds(90);     // 1 min. 30 sec.
string c = LegacyHelpers.FormatSeconds(3660);   // 1 h. 1 min.
string d = LegacyHelpers.FormatSeconds(90061);  // 1 d. 1 h.
```

## Когда использовать

Для отображения длительности работы, ожидания, ограничений и других временных интервалов.

## Важно

Передавайте неотрицательную длительность в допустимом диапазоне TimeSpan. Метод сам не ограничивает значение и не перехватывает переполнение.

---

# `OpenFolder(string folderPath)`

Открывает папку в файловом менеджере ОС.

Сигнатура:

```csharp
public static void OpenFolder(string folderPath)
```

Поведение:

- на Windows заменяет `/` на `\`;
- проверяет существование директории через `Directory.Exists(...)`;
- на Windows запускает `explorer.exe`;
- на macOS запускает `open`;
- на Linux запускает `xdg-open`;
- если нативный способ не сработал, пробует `Application.OpenURL("file://" + folderPath)`;
- при ошибке пишет сообщение в Unity log.

## Пример

```csharp
string folder = Path.Combine(BepInEx.Paths.GameRootPath, "BigCityLegacy");
LegacyHelpers.OpenFolder(folder);
```

## Когда использовать

Метод удобен для кнопок в UI, которые открывают папку с конфигами, логами или пользовательскими файлами.

## Важно

Метод ожидает путь к существующей директории. Если директория отсутствует, она не создаётся автоматически.

---

# `SafeInvoke(object instance, string methodName)`

Безопасно вызывает метод без параметров у объекта через reflection.

Сигнатура:

```csharp
public static void SafeInvoke(object instance, string methodName)
```

Метод поддерживает два сценария:

```csharp
LegacyHelpers.SafeInvoke(someObject, "SomeMethod");
LegacyHelpers.SafeInvoke(typeof(SomeType), "SomeStaticMethod");
```

Если `instance` сам является `Type`, вызов перенаправляется в overload:

```csharp
SafeInvoke((Type)instance, null, methodName);
```

Иначе тип берётся из `instance.GetType()`.

## Пример: вызов instance-метода

```csharp
if (GameUI.me)
{
    LegacyHelpers.SafeInvoke(GameUI.me, "UpResolution");
}
```

## Пример: вызов static-метода

```csharp
LegacyHelpers.SafeInvoke(typeof(PlayerPrefs), "Save");
```

## Важно

`instance` не должен быть `null`. В текущей реализации object-overload вызывает `instance.GetType()` до входа во второй overload, поэтому null нужно проверять заранее.

```csharp
if (target != null)
{
    LegacyHelpers.SafeInvoke(target, "MethodName");
}
```

---

# `SafeInvoke(Type type, object instance, string methodName)`

Безопасно вызывает метод без параметров через reflection, когда тип известен явно.

Сигнатура:

```csharp
public static void SafeInvoke(Type type, object instance, string methodName)
```

Метод ищет `methodName` с флагами:

```csharp
BindingFlags.Instance |
BindingFlags.Static |
BindingFlags.Public |
BindingFlags.NonPublic
```

Если метод найден, он вызывается так:

```csharp
method.Invoke(instance, null);
```

Ошибки вызова ловятся и логируются как warning:

```text
[BigCityLegacy] SafeInvoke failed: <methodName> -> <message>
```

## Пример: private instance-метод

```csharp
LegacyHelpers.SafeInvoke(
    typeof(GameUI),
    GameUI.me,
    "CheckActualDefferedRendeing");
```

## Пример: static-метод

```csharp
LegacyHelpers.SafeInvoke(
    typeof(PlayerPrefs),
    null,
    "Save");
```

## Когда использовать

Используйте `SafeInvoke`, когда нужно вызвать метод оригинальной игры без жёсткой compile-time зависимости от его доступности или уровня доступа.

Метод рассчитан только на методы **без параметров**. Для методов с аргументами нужно использовать отдельный reflection-код.

Если метод не найден, вызов пропускается. Ошибки поиска, включая неоднозначный набор перегрузок, также попадают в Warning. Метод не возвращает результат вызванного метода или признак успеха.

---

# `GetJsonPath(string arg, string json)`

Возвращает путь к JSON-файлу конфигурации.

Сигнатура:

```csharp
public static string GetJsonPath(string arg, string json)
```

Логика работы:

1. получает значение через `LegacyCommandLine.GetArgValue(arg)` из действующих аргументов BCL; для серверного процесса учитываются значения ServerConfig;
2. если аргумент не указан, строит путь по умолчанию:

```text
<GameRoot>/BigCityLegacy/config/<json>
```

3. снимает внешние кавычки через `LegacyCommandLine.StripQuotes(...)`;
4. если путь относительный, дополняет его через `Path.Combine(BepInEx.Paths.GameRootPath, text)`.

В стандартной ветке пытается создать `BigCityLegacy/config`. Явно заданный путь не переносится в эту папку, его родительский каталог этот helper не создаёт. Метод не читает и не создаёт сам файл. Имя метода не ограничивает расширение: например, для Alerts используется TOML.

`LegacyCommandLine.GetArgValue(...)` рассчитан на строгий формат:

```text
-key:value
-key:"value"
```

## Пример

```csharp
string serversListPath = LegacyHelpers.GetJsonPath(
    "-serversList",
    "ServersList.json");
```

CLI:

```text
-serversList:"C:\Big City\ServersList.json"
```

Если аргумент не указан, будет использован путь:

```text
<GameRoot>/BigCityLegacy/config/ServersList.json
```

## Примеры для существующих конфигов

```csharp
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string policy = LegacyHelpers.GetJsonPath("-pluginsPolicy", "PluginsPolicy.json");
string alerts = LegacyHelpers.GetJsonPath("-alerts", "Alerts.toml");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
```

Ивенты хранятся отдельными JSON в `BigCityLegacy/events/Race`, `CS` и `CS_Sur`; для выбора корневой папки служит `-eventsDir`. Пример с единым Events.json для этой схемы не используется.

---

# `CheckFileExistence(string path)`

Проверяет существование файла и пишет ошибку в лог, если файл не найден.

Сигнатура:

```csharp
public static bool CheckFileExistence(string path)
```

Поведение:

- возвращает `true`, если `File.Exists(path)`;
- возвращает `false`, если файла нет;
- при отсутствии файла пишет:

```text
Failed to load <path>: File not found
```

## Пример

```csharp
string path = LegacyHelpers.GetJsonPath("-exampleConfig", "ExamplePlugin.json");

if (!LegacyHelpers.CheckFileExistence(path))
{
    return;
}

string json = File.ReadAllText(path);
```

## Когда использовать

Метод подходит для конфигов и внешних JSON-файлов, где отсутствие файла не должно приводить к необработанному исключению.

---

# `IsGameplayRunning`

Проверяет, что клиент находится в активном gameplay-состоянии.

Сигнатура:

```csharp
public static bool IsGameplayRunning { get; }
```

Метод возвращает `false`, если:

- запущен серверный режим (`NetManager.isServer`);
- нет `Nuligine.me` или `Nuligine.RealGame`;
- нет активного `GameUI.me` или `GameUI.me.isUsed()` возвращает `false`;
- активна загрузка (`Loading.me`);
- сцены заняты (`Scenes.nowBusy()`);
- идёт fade-переход (`FadeUI.me && FadeUI.fadeState != FadeUI.FadeState.Unfaded`);
- открыто escape-меню или нужен выбор меню (`MenuEsc.me || MenuEsc.needSelectWhere`);
- открыт телефон (`GamePhone.me.gameObject.activeSelf`);
- нет локального `InputControl`;
- у локального input нет `current` или `currentOrParent`.

## Пример: hotkey только в игре

```csharp
private void Update()
{
    if (!LegacyHelpers.IsGameplayRunning)
    {
        return;
    }

    if (Input.GetKeyDown(KeyCode.F8))
    {
        BigCityLegacy.Api.LegacyAlerts.Hint("Tool ready");
    }
}
```

## Когда использовать

Метод полезен для клиентских hotkey, overlay, debug tools и других функций, которые не должны срабатывать в меню, на загрузке, во время fade-переходов или в server mode.

---

# `IsCsOrSurvivalMatchRunning`

Проверяет, находится ли локальный игрок в активном матче CS или CS_Sur.

Сигнатура:

```csharp
public static bool IsCsOrSurvivalMatchRunning { get; }
```

Метод работает только для online client:

```csharp
if (!NetManager.isOnlineClient)
{
    return false;
}
```

Далее проверяются два источника текущего event-состояния:

```csharp
Net_BaseEvent.curUserInRaceInst
Net_BaseEvent.isCurUserAddedToPlayersListG()
```

Событие считается активным, если оно является `Net_CS` и находится в одном из состояний:

```csharp
Net_BaseEvent.CurState.Spawn_AfterLobby
Net_BaseEvent.CurState.BeginRace
Net_BaseEvent.CurState.Race
```

Состояния lobby/complete не считаются активным матчем.

## Пример: запрет функции во время CS/CS_Sur

```csharp
if (LegacyHelpers.IsCsOrSurvivalMatchRunning)
{
    return;
}

FlyCamera.Create();
```

## Когда использовать

Метод подходит для клиентских функций, которые не должны мешать соревновательным режимам: free camera, debug tools, UI-инструменты, экспериментальные hotkey.

---

# `GetBuildVersion`

Возвращает build version игры из XML-asset'а `AlwaysOnline`.
Пишет в лог ошибку если AlwaysOnline ещё не инициализирован.

Сигнатура:

```csharp
public static int GetBuildVersion { get; }
```

Читает `AlwaysOnline.me.buildVersionAsset.text` и получает `curVersion` через `AlwaysOnline.GetBuildVersionFromXML(text, false)`. При любой ошибке возвращает `-1`. Это build игры, а не версия BCL и не online protocol version.

## Пример

```csharp
int buildVersion = LegacyHelpers.GetBuildVersion;
Debug.Log("Game build version: " + buildVersion);
```

---

# `LoadEmbeddedTexture(string resourceName)`

Загружает Texture2D из встроенного ресурса сборки BigCityLegacy.

Сигнатура:

```csharp
public static Texture2D LoadEmbeddedTexture(string resourceName)
```

Логика работы:

1. получает `Assembly.GetExecutingAssembly()`;
2. открывает `GetManifestResourceStream(resourceName)` и читает байты ресурса;
3. создаёт `Texture2D(2, 2, TextureFormat.RGBA32, false)` без mipmaps;
4. загружает изображение через `LoadImage(data)`;
5. присваивает текстуре имя `resourceName` и возвращает её.

Если ресурса нет, пишет Error и возвращает `null`. Если `LoadImage` вернул `false`, удаляет созданную текстуру, пишет Error и возвращает `null`.

## Пример

```csharp
Texture2D logo = LegacyHelpers.LoadEmbeddedTexture("bcl-logo.png");
if (logo != null)
{
    Debug.Log(logo.name);
    // Release when the UI no longer needs the texture.
    UnityEngine.Object.Destroy(logo);
}
```

## Когда использовать

Для загрузки изображений, встроенных именно в BigCityLegacy, например логотипа для собственного UI.

## Важно

Метод ищет ресурс в BigCityLegacy.dll, а не в вызывающем плагине. Для ресурсов своего плагина используйте `typeof(MyPlugin).Assembly.GetManifestResourceStream(...)` и собственную загрузку Texture2D. Вызывайте метод в главном потоке Unity. Ненужную текстуру удаляйте через `UnityEngine.Object.Destroy`; общего обработчика исключений чтения и декодирования в helper нет.

---

# `isDebugBuild`

Показывает, собран ли BCL с символом DEBUG.

Внутренний член BCL; не является публичным API для внешних плагинов.

Сигнатура:

```csharp
internal static bool isDebugBuild { get; }
```

Возвращает `true` при компиляции с `#if DEBUG`, иначе `false`. Значение определяется сборкой BCL, а не `Debug.isDebugBuild` игры и не аргументом запуска.

## Пример

```csharp
if (LegacyHelpers.isDebugBuild)
{
    Debug.Log("BCL debug diagnostics enabled");
}
```

## Когда использовать

Для включения внутренней диагностики и отладочных веток BCL.

---

# `VersionLine`

Возвращает строку с версией мода и игры.

Внутренний член BCL; не является публичным API для внешних плагинов.

Сигнатура:

```csharp
internal static string VersionLine { get; }
```

Формат: `BigCityLegacy v<VersionInfo.ModVersionName> | Game ver: <Application.version>`. Значения читаются при обращении к свойству.

## Пример

```csharp
Debug.Log(LegacyHelpers.VersionLine);
```

## Когда использовать

Для внутренних сообщений запуска, статуса и диагностики BCL.

---

# `EnsureServerNetChat(NetManager manager)`

Создаёт серверный NetChat, если он ещё не существует.

Внутренний член BCL; не является публичным API для внешних плагинов.

Сигнатура:

```csharp
internal static void EnsureServerNetChat(NetManager manager)
```

Работает только при наличии `manager`, серверном режиме и активном `NetworkServer`. Если `NetChat.me` уже есть, ничего не делает. Создаёт объект из `manager.chat`, проверяет NetChat и NetworkIdentity, задаёт имя `NetChat_Server` и регистрирует объект через `NetworkServer.Spawn`. Ошибки prefab/создания/регистрации пишет в лог; некорректный или не зарегистрированный экземпляр удаляет.

## Пример

```csharp
if (NetManager.isServer && NetworkServer.active)
{
    LegacyHelpers.EnsureServerNetChat(NetManager.me);
}
```

## Когда использовать

Для подготовки чата в серверном процессе, включая headless-запуск, перед отправкой серверных сообщений.

---

# `EnsureServerUnetSettings(NetManager manager)`

Создаёт UNet_Settings для работающего сервера при отсутствии экземпляра.

Внутренний член BCL; не является публичным API для внешних плагинов.

Сигнатура:

```csharp
internal static void EnsureServerUnetSettings(NetManager manager)
```

Проверяет `manager`, серверный режим, активный NetworkServer и `manager.unet_settings`. Если `FindObjectOfType<UNet_Settings>()` нашёл экземпляр, возвращается. Иначе создаёт объект из prefab, задаёт имя `UNet_Settings_Server` и вызывает `NetworkServer.Spawn`. Общего try/catch вокруг создания и регистрации нет.

## Пример

```csharp
if (NetManager.isServer && NetworkServer.active)
{
    LegacyHelpers.EnsureServerUnetSettings(NetManager.me);
}
```

## Когда использовать

Для инициализации штатных сетевых настроек в серверном процессе.

---

# `ResetSubsStatus()`

Однократно сбрасывает сохранённые статусы подписок на социальные сети.

Внутренний член BCL; не является публичным API для внешних плагинов.

Сигнатура:

```csharp
internal static void ResetSubsStatus()
```

Если `BigCityLegacy.isSubsReseted` в PlayerPrefs уже равен `1`, возвращается. Иначе ставит эту метку и сбрасывает существующие ключи `Subs_Vk`, `Subs_Fb`, `Subs_Instagram` в `0`, затем вызывает `PlayerPrefs.Save()`. Отсутствующие ключи подписок не создаёт.

## Пример

```csharp
LegacyHelpers.ResetSubsStatus();
```

## Когда использовать

Для одноразовой подготовки старого сохранения в процессе инициализации BCL.

---

# `LegacyCommandLine.HasArg(string arg)`

Внутренний член BCL; не является публичным API для внешних плагинов.

Располагается в классе `LegacyCommandLine`.
Проверяет наличие command-line флага без учета регистра и наличия кавычек.

Сигнатура:

```csharp
internal static bool HasArg(string arg)
```

Метод проверяет действующие аргументы BCL, включая значения из ServerConfig в серверном процессе. Флаг сравнивается целиком, без учёта регистра и внешних кавычек. Значения формата `-key:value` нужно читать через `GetArgValue`, а не HasArg.

## Пример

```csharp
if (LegacyCommandLine.HasArg("-noServerCli"))
{
    return;
}
```

---

# NativeErrorDialog

Внутренний член BCL; не является публичным API для внешних плагинов.

`NativeErrorDialog` — отдельный helper-класс для показа блокирующего нативного окна ошибки с кнопкой OK.

Класс находится отдельно от `LegacyHelpers` и используется для критических ошибок запуска, когда обычный Unity UI может быть недоступен или ещё не инициализирован.

## `Show(string title, string message)`

Показывает окно ошибки и возвращает `true`, если удалось показать графический диалог.

Сигнатура:

```csharp
public static bool Show(string title, string message)
```

Поведение по платформам:

- Windows: `MessageBoxW` из `user32.dll`;
- Linux: сначала `kdialog`, затем `zenity`;
- macOS: `/usr/bin/osascript` с AppleScript `display alert`;
- если графический диалог показать не удалось, пишет fallback-сообщение в `Console.Error`.

## Пример

```csharp
NativeErrorDialog.Show(
    "BigCityLegacy Startup Error",
    "Failed to load required configuration file.");
```

## `ShowAndExit(string title, string message, int exitCode = 1)`

Показывает окно ошибки и завершает приложение через `Application.Quit(exitCode)`.

Сигнатура:

```csharp
public static void ShowAndExit(
    string title,
    string message,
    int exitCode = 1)
```

## Пример

```csharp
NativeErrorDialog.ShowAndExit(
    "BigCityLegacy Startup Error",
    "Outdated plugin version detected.",
    1);
```

## Когда использовать

`NativeErrorDialog` стоит использовать для критических startup-ошибок, когда продолжение запуска опасно: конфликт старого плагина, несовместимая версия, отсутствующие обязательные файлы.

Для обычных runtime-ошибок лучше использовать логгер BepInEx или Unity `Debug.LogError`, чтобы не блокировать игру нативным диалогом.

В headless/server mode диалог может быть нежелателен: на сервере обычно лучше писать ошибку в консоль/лог и завершать процесс штатным серверным механизмом.

---# `LegacyServerShutdown`

Управляет отложенным завершением серверного процесса.

Внутренний член BCL; не является публичным API для внешних плагинов.

Сигнатура:

```csharp
internal static bool IsRequested { get; }
internal static void Initialize();
internal static void Request(string requestReason);
internal static void Tick();
```

`Initialize()` один раз подписывает обработчик `Application.quitting`. `Request(reason)` атомарно отмечает запрос остановки и подаёт сигнал консольному потоку; повторный запрос игнорируется. `IsRequested` показывает наличие запроса. `Tick()` в главном потоке один раз пишет серверное сообщение и вызывает `Application.Quit()`. Обработчик quitting также уведомляет консольный поток.

## Пример

```csharp
// Once during BCL startup, on the main thread:
LegacyServerShutdown.Initialize();

// For example, from the server CLI:
LegacyServerShutdown.Request("console stop");

// In the plugin's main-thread Update:
LegacyServerShutdown.Tick();
```

## Когда использовать

Для обработки команды stop и других внутренних запросов завершения BCL. `Request` допускает вызов из консольного потока; `Initialize` и `Tick` вызываются в главном потоке Unity.

---

[Документация разработчика](README.md).
