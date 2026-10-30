# LegacyHelpers

Справочник по текущей реализации BCL 1.4.2: [LegacyHelpers.cs](../../../src/BigCityLegacy/Core/LegacyHelpers.cs). `LegacyHelpers` — публичный статический класс в глобальном пространстве имён. Для внешнего плагина нужна ссылка на BigCityLegacy.dll и зависимость BepInEx от `com.bigcitylegacy.baseplugin`. Методы с Unity API вызывайте в главном потоке.

## Публичные члены

```csharp
public static string ModDataPath { get; }
public static string GetCurTimestamp { get; }
public static string FormatSeconds(long seconds);
public static void OpenFolder(string folderPath);
public static void SafeInvoke(object instance, string methodName);
public static void SafeInvoke(Type type, object instance, string methodName);
public static string GetJsonPath(string arg, string json);
public static bool CheckFileExistence(string path);
public static bool IsGameplayRunning { get; }
public static bool IsCsOrSurvivalMatchRunning { get; }
public static int GetBuildVersion { get; }
public static Texture2D LoadEmbeddedTexture(string resourceName);
```

## Пути и файлы

`ModDataPath` возвращает `<GameRoot>/BigCityLegacy`. При каждом обращении проверяет каталог и пытается создать его, если он отсутствует. Ошибка создания записывается в Unity log; путь всё равно возвращается. Свойства `LegacyHelpers.GameRoot` нет: корень игры доступен через `BepInEx.Paths.GameRootPath`.

`GetJsonPath(arg, json)` получает значение `arg` из действующих аргументов BCL, включая настройки ServerConfig для серверного процесса. При отсутствии или пустом значении строит `<GameRoot>/BigCityLegacy/config/<json>` и пытается создать каталог `config`. Из выбранного пути снимаются внешние кавычки; относительный путь дополняется корнем игры. Явный путь не переносится в `config`, а его родительский каталог автоматически не создаётся этим методом. Файл не читается, не проверяется и не создаётся. Имя метода не ограничивает расширение: BCL использует его также для Alerts.toml.

```csharp
using BepInEx;
using System.IO;

string root = Paths.GameRootPath;
string data = LegacyHelpers.ModDataPath;
string ownConfig = LegacyHelpers.GetJsonPath("-exampleConfig", "ExamplePlugin.json");
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
```

```text
-exampleConfig:"C:\Big City\ExamplePlugin.json"
```

Стандартный путь ServersList — `<GameRoot>/BigCityLegacy/config/ServersList.json`. Для ивентов используются отдельные JSON в `BigCityLegacy/events/Race`, `CS`, `CS_Sur` и параметр `-eventsDir`; единого Events.json и актуальной настройки `-eventsList` нет.

`CheckFileExistence(path)` возвращает результат `File.Exists`. Если файла нет, пишет `Failed to load <path>: File not found` в Unity error log. Не загружает файл и не создаёт его.

`OpenFolder(folderPath)` требует существующий каталог; иначе пишет ошибку и возвращается. На WindowsPlayer нормализует `/` в `\` и запускает `explorer.exe`, на OSXPlayer — `open`, на LinuxPlayer — `xdg-open`. Если нативный запуск выбросил исключение, пробует `Application.OpenURL("file://" + folderPath)`. Метод не создаёт каталог. Проверки платформ относятся к Player, работа в Unity Editor не гарантируется.

```csharp
LegacyHelpers.OpenFolder(LegacyHelpers.ModDataPath);
```

## Время

`GetCurTimestamp` возвращает текущее **локальное время ОС** в формате `yyyy-MM-dd_HH-mm-ss-fff`. Это свойство, а не метод; оно не возвращает Unix timestamp. Серверные имена логов и время их записей используют UTC независимо от этого helper.

`FormatSeconds(seconds)` использует `TimeSpan.FromSeconds` и возвращает до двух единиц времени с английскими сокращениями. При сутках выводит дни и часы, при часах — часы и минуты, при минутах — минуты и секунды, иначе секунды. Для длительности передавайте неотрицательное значение в допустимом диапазоне TimeSpan; переполнение не перехватывается.

```csharp
string stamp = LegacyHelpers.GetCurTimestamp;
string shortTime = LegacyHelpers.FormatSeconds(90);     // 1 min. 30 sec.
string longTime = LegacyHelpers.FormatSeconds(90061);   // 1 d. 1 h.
```

## SafeInvoke

Обе перегрузки вызывают метод без параметров через reflection. Поиск охватывает `Instance | Static | Public | NonPublic`; отсутствующий метод пропускается. Вызов синхронный, значения результата и признака успеха нет. Ошибки поиска и вызова в перегрузке с `Type` перехватываются и пишутся как Warning.

В перегрузке с `object` переданный `Type` означает статический вызов. Обычный объект использует `instance.GetType()`; передача `null` в эту перегрузку выбрасывает `NullReferenceException` до защищённого вызова. Не передавайте методы с аргументами; несколько одноимённых перегрузок могут дать `AmbiguousMatchException`, которая будет записана в лог.

```csharp
LegacyHelpers.SafeInvoke(typeof(UnityEngine.PlayerPrefs), "Save");
if (target != null)
    LegacyHelpers.SafeInvoke(target.GetType(), target, "Refresh");
```

## Состояние игры

`IsGameplayRunning` — проверка клиентского игрового состояния. Возвращает false на сервере; при отсутствии `Nuligine.me`/`Nuligine.RealGame`, активного GameUI, локального InputControl или его `current`/`currentOrParent`; во время Loading, занятых сцен, незавершённого FadeUI; при открытом MenuEsc/выборе меню или активном телефоне. Если FadeUI отсутствует, это само по себе не блокирует проверку. Это не проверка соединения или состояния конкретного ивента.

```csharp
private void Update()
{
    if (!LegacyHelpers.IsGameplayRunning) return;
    if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
        BigCityLegacy.Api.LegacyAlerts.Hint("Tool ready");
}
```

`IsCsOrSurvivalMatchRunning` работает только для `NetManager.isOnlineClient`. Проверяет `Net_BaseEvent.curUserInRaceInst`, затем `Net_BaseEvent.isCurUserAddedToPlayersListG()`. Возвращает true только для `Net_CS` в `Spawn_AfterLobby`, `BeginRace` или `Race`. Лобби и завершённый матч не считаются активными; это проверка локального игрока, а не всех матчей сервера.

`GetBuildVersion` читает `AlwaysOnline.me.buildVersionAsset.text`, разбирает XML через `AlwaysOnline.GetBuildVersionFromXML(text, false)` и возвращает `curVersion`. При ошибке, включая ранний вызов до инициализации AlwaysOnline, пишет Error и возвращает `-1`. Это числовой build игры, а не версия BCL и не `OnlineProtocolVersion`.

## Встроенные текстуры

`LoadEmbeddedTexture(resourceName)` ищет manifest resource **в сборке BigCityLegacy**, а не в вызывающем плагине. Читает данные и вызывает `Texture2D.LoadImage` для новой RGBA32-текстуры без mipmaps; имя текстуры равно имени ресурса. Если ресурса нет, пишет Error и возвращает null. При возврате false из LoadImage удаляет созданную текстуру, пишет Error и возвращает null. Исключения чтения/декодирования общего обработчика не имеют. Возвращённой текстурой владеет вызывающий код; ненужную текстуру удаляйте через `UnityEngine.Object.Destroy`.

Для embedded resource собственного плагина используйте его `typeof(MyPlugin).Assembly.GetManifestResourceStream(...)` и собственную загрузку Texture2D.

## Внутренние вспомогательные классы BCL

Следующие члены не являются публичным API для внешних плагинов:

| Член | Назначение |
| --- | --- |
| `LegacyHelpers.isDebugBuild` | Значение определяется символом компиляции DEBUG. |
| `LegacyHelpers.VersionLine` | Строка версии BCL и `Application.version`. |
| `EnsureServerNetChat(NetManager)` | Создаёт и регистрирует NetChat при активном UNet-сервере, если чата ещё нет; проверяет prefab/NetworkIdentity и логирует ошибки. |
| `EnsureServerUnetSettings(NetManager)` | При активном сервере создаёт UNet_Settings из prefab, только если экземпляр отсутствует. |
| `ResetSubsStatus()` | Однократно сбрасывает Subs_Vk/Subs_Fb/Subs_Instagram в PlayerPrefs с меткой BigCityLegacy.isSubsReseted и сохраняет PlayerPrefs. |
| `LegacyCommandLine.HasArg(string)` | Внутренний класс: проверяет точный флаг без учёта регистра и внешних кавычек среди действующих аргументов, включая ServerConfig. Не читает значение `-key:value`. |
| `LegacyServerShutdown` | Внутренний запрос остановки, сигнал консольному потоку и Application.Quit из главного потока. |

`NativeErrorDialog` также является **internal-классом**, несмотря на public-методы `Show(title, message)` и `ShowAndExit(title, message, exitCode = 1)`. `Show` синхронно показывает блокирующий диалог: MessageBoxW на Windows, kdialog/zenity на Linux, osascript на macOS; при неудаче пишет fallback в Console.Error и возвращает false. `ShowAndExit` затем вызывает `Application.Quit(exitCode)`. Это внутренний механизм критических ошибок запуска, не публичный диалоговый API. Для уведомлений плагина используйте [LegacyAlerts](LegacyAlerts_RU.md).

[Документация разработчика](README.md).
