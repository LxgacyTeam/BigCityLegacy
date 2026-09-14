# LegacyHelpers

`LegacyHelpers` — набор небольших внутренних helper-методов BigCityLegacy для проверок состояния игры, работы с путями, безопасного вызова методов через reflection и выполнения типовых рутинных задач.

---

# `GameRoot`

Возвращает путь к корневой папке игры.

Сигнатура:

```csharp
public static string GameRoot { get; }
```

Логика работы:

1. базовый fallback — `Directory.GetCurrentDirectory()`;
2. если доступен `Application.dataPath`, берётся его родительская папка;
3. если получить путь через Unity не удалось, возвращается fallback.

Для обычной Unity-игры `Application.dataPath` указывает на папку вида:

```text
game_Data
```

Поэтому `GameRoot` вернёт родительскую директорию, то есть корень игры.

## Пример

```csharp
string root = LegacyHelpers.GameRoot;
string configFolder = Path.Combine(root, "BigCityLegacy");
```

## Когда использовать

Используйте `GameRoot`, когда нужно построить путь относительно папки игры: конфиги, JSON-файлы, вспомогательные директории, локальные ресурсы мода.

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
string folder = Path.Combine(LegacyHelpers.GameRoot, "BigCityLegacy");
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
    LegacyHelpers.SafeInvoke(GameUI.me, "Refresh");
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
    typeof(MenuEsc),
    MenuEsc.me,
    "UpdateButtons");
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

---

# `GetJsonPath(string arg, string json)`

Возвращает путь к JSON-файлу конфигурации.

Сигнатура:

```csharp
public static string GetJsonPath(string arg, string json)
```

Логика работы:

1. пробует получить значение command-line аргумента через `LegacyCommandLine.GetArgValue(arg)`;
2. если аргумент не указан, строит путь по умолчанию:

```text
<GameRoot>/BigCityLegacy/<json>
```

3. возвращает путь после `LegacyCommandLine.StripQuotes(...)`.

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
<GameRoot>/BigCityLegacy/ServersList.json
```

## Примеры для существующих конфигов

```csharp
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string events = LegacyHelpers.GetJsonPath("-eventsList", "Events.json");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
```

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
string path = LegacyHelpers.GetJsonPath("-eventsList", "Events.json");

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
public static bool IsGameplayRunning
```

Метод возвращает `false`, если:

- запущен серверный режим (`NetManager.isServer`);
- нет `Nuligine.me` или `Nuligine.RealGame`;
- нет активного `GameUI.me` или `GameUI.me.isUsed()` возвращает `false`;
- активна загрузка (`Loading.me`);
- сцены заняты (`Scenes.nowBusy()`);
- идёт fade-переход (`FadeUI.fadeState != FadeUI.FadeState.Unfaded`);
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
        LegacyPointTool.Toggle();
    }
}
```

## Когда использовать

Метод полезен для клиентских hotkey, overlay, debug tools и других функций, которые не должны срабатывать в меню, на загрузке, во время fade-переходов или в server mode.

---

# `IsCsOrSurvivalMatchRunning`

Проверяет, находится ли локальный игрок в активном матче CS или CS_Survival.

Сигнатура:

```csharp
public static bool IsCsOrSurvivalMatchRunning
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

## Пример: запрет функции во время CS/CS_Survival

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
public static int GetBuildVersion
```

## Пример

```csharp
int buildVersion = LegacyHelpers.GetBuildVersion;
Debug.Log("Game build version: " + buildVersion);
```

---

# `LegacyCommandLine.HasArg(string arg)`

Располагается в классе `LegacyCommandLine`.
Проверяет наличие command-line флага без учета регистра и наличия кавычек.

Сигнатура:

```csharp
internal static bool HasArg(string arg)
```

Метод проходит по `Environment.GetCommandLineArgs()` и возвращает `true`, если среди аргументов присутствует обработанный `arg`. Arg сравнивается без учета регистра и кавычек.

## Пример

```csharp
if (LegacyCommandLine.HasArg("-noServerCli"))
{
    return;
}
```

---

# NativeErrorDialog

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

---