# LegacyHelpers

`LegacyHelpers` 是 BigCityLegacy 内部的一组小型辅助方法，用于检查游戏状态、处理路径、通过反射安全调用方法，以及执行常见的日常任务。

---

# `ModDataPath`

返回位于游戏根目录中的 BigCityLegacy 文件夹路径。

签名：

```csharp
public static string ModDataPath { get; }
```

## 示例

```csharp
string data = LegacyHelpers.ModDataPath;
string config = Path.Combine(dataFolder, "config");
```

## 适用场景

`BigCityLegacy` 文件夹用于存储模组的用户数据：配置、JSON 文件、辅助目录及模组本地资源。

---

# `OpenFolder(string folderPath)`

在操作系统的文件管理器中打开文件夹。

签名：

```csharp
public static void OpenFolder(string folderPath)
```

行为：

- 在 Windows 上，将 `/` 替换为 `\`；
- 通过 `Directory.Exists(...)` 检查目录是否存在；
- 在 Windows 上启动 `explorer.exe`；
- 在 macOS 上启动 `open`；
- 在 Linux 上启动 `xdg-open`；
- 如果原生方式不起作用，尝试使用 `Application.OpenURL("file://" + folderPath)`；
- 失败时向 Unity 日志写入错误消息。

## 示例

```csharp
string folder = Path.Combine(LegacyHelpers.GameRoot, "BigCityLegacy");
LegacyHelpers.OpenFolder(folder);
```

## 适用场景

该方法适合用于打开配置、日志或用户文件所在文件夹的 UI 按钮。

## 注意事项

该方法要求传入已存在目录的路径。如果目录不存在，不会自动创建。

---

# `SafeInvoke(object instance, string methodName)`

通过反射安全调用对象上的无参数方法。

签名：

```csharp
public static void SafeInvoke(object instance, string methodName)
```

该方法支持两种使用方式：

```csharp
LegacyHelpers.SafeInvoke(someObject, "SomeMethod");
LegacyHelpers.SafeInvoke(typeof(SomeType), "SomeStaticMethod");
```

如果 `instance` 本身是一个 `Type`，则将调用转交给以下重载：

```csharp
SafeInvoke((Type)instance, null, methodName);
```

否则，通过 `instance.GetType()` 获取类型。

## 示例：调用实例方法

```csharp
if (GameUI.me)
{
    LegacyHelpers.SafeInvoke(GameUI.me, "Refresh");
}
```

## 示例：调用静态方法

```csharp
LegacyHelpers.SafeInvoke(typeof(PlayerPrefs), "Save");
```

## 注意事项

`instance` 不得为 `null`。在当前实现中，接收对象的重载在进入第二个重载前会调用 `instance.GetType()`，因此必须事先检查是否为空。

```csharp
if (target != null)
{
    LegacyHelpers.SafeInvoke(target, "MethodName");
}
```

---

# `SafeInvoke(Type type, object instance, string methodName)`

在明确知道类型时，通过反射安全调用无参数方法。

签名：

```csharp
public static void SafeInvoke(Type type, object instance, string methodName)
```

该方法使用以下标志查找 `methodName`：

```csharp
BindingFlags.Instance |
BindingFlags.Static |
BindingFlags.Public |
BindingFlags.NonPublic
```

如果找到方法，则按以下方式调用：

```csharp
method.Invoke(instance, null);
```

调用错误会被捕获，并作为警告记录：

```text
[BigCityLegacy] SafeInvoke failed: <methodName> -> <message>
```

## 示例：私有实例方法

```csharp
LegacyHelpers.SafeInvoke(
    typeof(MenuEsc),
    MenuEsc.me,
    "UpdateButtons");
```

## 示例：静态方法

```csharp
LegacyHelpers.SafeInvoke(
    typeof(PlayerPrefs),
    null,
    "Save");
```

## 适用场景

需要调用原版游戏中的方法，但不希望在编译时硬性依赖该方法的存在或访问级别时，可使用 `SafeInvoke`。

该方法仅适用于**无参数**方法。对于带参数的方法，请使用单独的反射代码。

---

# `GetJsonPath(string arg, string json)`

返回 JSON 配置文件的路径。

签名：

```csharp
public static string GetJsonPath(string arg, string json)
```

工作流程：

1. 尝试通过 `LegacyCommandLine.GetArgValue(arg)` 获取命令行参数值；
2. 如果未指定参数，则构建默认路径：

```text
<GameRoot>/BigCityLegacy/<json>
```

3. 返回经 `LegacyCommandLine.StripQuotes(...)` 处理后的路径。

`LegacyCommandLine.GetArgValue(...)` 要求严格使用以下格式：

```text
-key:value
-key:"value"
```

## 示例

```csharp
string serversListPath = LegacyHelpers.GetJsonPath(
    "-serversList",
    "ServersList.json");
```

命令行：

```text
-serversList:"C:\Big City\ServersList.json"
```

如果未指定参数，将使用以下路径：

```text
<GameRoot>/BigCityLegacy/ServersList.json
```

## 现有配置的示例

```csharp
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string events = LegacyHelpers.GetJsonPath("-eventsList", "Events.json");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
```

---

# `CheckFileExistence(string path)`

检查文件是否存在；如果未找到文件，则向日志写入错误。

签名：

```csharp
public static bool CheckFileExistence(string path)
```

行为：

- 如果 `File.Exists(path)` 为真，返回 `true`；
- 如果文件不存在，返回 `false`；
- 文件缺失时，写入以下消息：

```text
Failed to load <path>: File not found
```

## 示例

```csharp
string path = LegacyHelpers.GetJsonPath("-eventsList", "Events.json");

if (!LegacyHelpers.CheckFileExistence(path))
{
    return;
}

string json = File.ReadAllText(path);
```

## 适用场景

该方法适用于配置文件和外部 JSON 文件，使文件缺失不会导致未处理的异常。

---

# `IsGameplayRunning`

检查客户端是否处于实际进行游戏的状态。

签名：

```csharp
public static bool IsGameplayRunning
```

以下情况下返回 `false`：

- 正在运行服务器模式（`NetManager.isServer`）；
- 缺少 `Nuligine.me` 或 `Nuligine.RealGame`；
- 缺少有效的 `GameUI.me`，或 `GameUI.me.isUsed()` 返回 `false`；
- 正在加载（`Loading.me`）；
- 场景处于忙碌状态（`Scenes.nowBusy()`）；
- 正在进行淡入淡出过渡（`FadeUI.fadeState != FadeUI.FadeState.Unfaded`）；
- Esc 菜单已打开，或需要进行菜单选择（`MenuEsc.me || MenuEsc.needSelectWhere`）；
- 手机已打开（`GamePhone.me.gameObject.activeSelf`）；
- 缺少本地 `InputControl`；
- 本地输入缺少 `current` 或 `currentOrParent`。

## 示例：仅在游戏过程中生效的快捷键

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

## 适用场景

该方法适用于客户端快捷键、叠加界面、调试工具，以及其他不应在菜单、加载过程、淡入淡出过渡或服务器模式中触发的功能。

---

# `IsCsOrSurvivalMatchRunning`

检查本地玩家是否正在参加进行中的 CS 或 CS_Survival 比赛。

签名：

```csharp
public static bool IsCsOrSurvivalMatchRunning
```

该方法仅适用于在线客户端：

```csharp
if (!NetManager.isOnlineClient)
{
    return false;
}
```

随后检查当前活动状态的两个来源：

```csharp
Net_BaseEvent.curUserInRaceInst
Net_BaseEvent.isCurUserAddedToPlayersListG()
```

如果活动属于 `Net_CS`，且处于以下任一状态，则视为正在进行：

```csharp
Net_BaseEvent.CurState.Spawn_AfterLobby
Net_BaseEvent.CurState.BeginRace
Net_BaseEvent.CurState.Race
```

大厅状态和已完成状态不视为正在进行的比赛。

## 示例：在 CS/CS_Survival 期间禁用某项功能

```csharp
if (LegacyHelpers.IsCsOrSurvivalMatchRunning)
{
    return;
}

FlyCamera.Create();
```

## 适用场景

该方法适用于不得干扰竞技模式的客户端功能，例如自由相机、调试工具、UI 工具及实验性快捷键。

---

# `GetBuildVersion`

从 `AlwaysOnline` XML 资源中返回游戏构建版本。
如果 AlwaysOnline 尚未初始化，则向日志写入错误。

签名：

```csharp
public static int GetBuildVersion
```

## 示例

```csharp
int buildVersion = LegacyHelpers.GetBuildVersion;
Debug.Log("Game build version: " + buildVersion);
```

---

# `LegacyCommandLine.HasArg(string arg)`

位于 `LegacyCommandLine` 类中。
检查命令行标志是否存在，忽略大小写和引号。

签名：

```csharp
internal static bool HasArg(string arg)
```

该方法遍历 `Environment.GetCommandLineArgs()`，如果参数列表中包含处理后的 `arg`，则返回 `true`。比较时忽略大小写，并去除引号。

## 示例

```csharp
if (LegacyCommandLine.HasArg("-noServerCli"))
{
    return;
}
```

---

# NativeErrorDialog

`NativeErrorDialog` 是一个独立辅助类，用于显示带有确定（OK）按钮的阻塞式原生错误对话框。

该类与 `LegacyHelpers` 分开，适用于常规 Unity UI 不可用或尚未初始化时发生的严重启动错误。

## `Show(string title, string message)`

显示错误对话框；如果成功显示图形对话框，则返回 `true`。

签名：

```csharp
public static bool Show(string title, string message)
```

各平台的行为：

- Windows：使用 `user32.dll` 中的 `MessageBoxW`；
- Linux：先尝试 `kdialog`，再尝试 `zenity`；
- macOS：通过 `/usr/bin/osascript` 执行 AppleScript 的 `display alert`；
- 如果无法显示图形对话框，则向 `Console.Error` 写入备用消息。

## 示例

```csharp
NativeErrorDialog.Show(
    "BigCityLegacy Startup Error",
    "Failed to load required configuration file.");
```

## `ShowAndExit(string title, string message, int exitCode = 1)`

显示错误对话框，并通过 `Application.Quit(exitCode)` 退出应用程序。

签名：

```csharp
public static void ShowAndExit(
    string title,
    string message,
    int exitCode = 1)
```

## 示例

```csharp
NativeErrorDialog.ShowAndExit(
    "BigCityLegacy Startup Error",
    "Outdated plugin version detected.",
    1);
```

## 适用场景

发生继续启动可能带来风险的严重启动错误时，可使用 `NativeErrorDialog`，例如旧插件冲突、版本不兼容或必需文件缺失。

对于常规运行时错误，优先使用 BepInEx 日志记录器或 Unity 的 `Debug.LogError`，避免游戏被原生对话框阻塞。

在无图形界面或服务器模式下，显示对话框可能不合适。对于服务器，通常最好将错误写入控制台或日志，并通过标准服务器机制终止进程。

---
