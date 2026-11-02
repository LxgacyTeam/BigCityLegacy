# LegacyHelpers

`LegacyHelpers` 是 BigCityLegacy 的辅助方法集合，用于检查游戏状态、处理路径、时间和资源、通过反射安全调用方法，以及执行常见任务。

该类为全局命名空间中的公开类。外部插件需引用 BigCityLegacy.dll 并声明对 `com.bigcitylegacy.baseplugin` 的 BepInEx 依赖。`internal` 成员会在各节注明，其示例适用于 BCL 自身代码。涉及 Unity API 的方法应在主线程调用。

---

# `ModDataPath`

返回位于游戏根目录中的 BigCityLegacy 文件夹路径。

签名：

```csharp
public static string ModDataPath { get; }
```

每次访问都会检查目录，缺失时尝试创建。创建失败会向 Unity 记录 Error，但仍返回路径。

## 示例

```csharp
string data = LegacyHelpers.ModDataPath;
string config = Path.Combine(data, "config");
```

## 适用场景

`BigCityLegacy` 文件夹用于存储模组的用户数据：配置、JSON 文件、辅助目录及模组本地资源。

---

# `GetCurTimestamp`

以适合文件名的字符串格式返回当前日期和时间。

签名：

```csharp
public static string GetCurTimestamp { get; }
```

每次访问读取 `DateTime.Now`，格式化为 `yyyy-MM-dd_HH-mm-ss-fff`，例如 `2026-10-31_14-25-36-123`。使用操作系统本地时间。

## 示例

```csharp
string stamp = LegacyHelpers.GetCurTimestamp;
string filename = "backup_" + stamp + ".json";
```

## 适用场景

适合命名导出文件、备份和诊断快照。

## 注意

这是属性，而非方法或 Unix 时间戳。BCL 服务器日志记录器独立使用 UTC。

---

# `FormatSeconds(long seconds)`

将以秒计的时长转换为简短字符串。

签名：

```csharp
public static string FormatSeconds(long seconds)
```

使用 `TimeSpan.FromSeconds(seconds)`，最多选择两个时间单位：

- 至少一天：天和小时；
- 至少一小时：小时和分钟；
- 至少一分钟：分钟和秒；
- 不足一分钟：秒。

缩写固定为 `d.`、`h.`、`min.`、`sec.`，不随游戏语言变化。

## 示例

```csharp
string a = LegacyHelpers.FormatSeconds(45);     // 45 sec.
string b = LegacyHelpers.FormatSeconds(90);     // 1 min. 30 sec.
string c = LegacyHelpers.FormatSeconds(3660);   // 1 h. 1 min.
string d = LegacyHelpers.FormatSeconds(90061);  // 1 d. 1 h.
```

## 适用场景

用于显示运行时长、等待时间、限制时长和其他时间间隔。

## 注意

请传入 TimeSpan 支持范围内的非负时长。方法不截断输入，也不捕获溢出异常。

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
string folder = Path.Combine(BepInEx.Paths.GameRootPath, "BigCityLegacy");
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
    LegacyHelpers.SafeInvoke(GameUI.me, "UpResolution");
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
    typeof(GameUI),
    GameUI.me,
    "CheckActualDefferedRendeing");
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

找不到方法时跳过。查找错误（包括重载不明确）也记录为 Warning。Helper 不返回被调用方法的结果或成功标志。

---

# `GetJsonPath(string arg, string json)`

返回 JSON 配置文件的路径。

签名：

```csharp
public static string GetJsonPath(string arg, string json)
```

工作流程：

1. 通过 `LegacyCommandLine.GetArgValue(arg)` 从 BCL 有效参数读取值；服务器进程包含 ServerConfig 设置；
2. 如果未指定参数，则构建默认路径：

```text
<GameRoot>/BigCityLegacy/config/<json>
```

3. 通过 `LegacyCommandLine.StripQuotes(...)` 移除外层引号；
4. 相对路径通过 `Path.Combine` 与 `BepInEx.Paths.GameRootPath` 组合。

默认分支尝试创建 `BigCityLegacy/config`。显式指定的路径不会移入该目录，helper 也不创建其父目录。方法不读取或创建文件本身。方法名称不限制扩展名，例如 Alerts 使用 TOML。

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
<GameRoot>/BigCityLegacy/config/ServersList.json
```

## 现有配置的示例

```csharp
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string policy = LegacyHelpers.GetJsonPath("-pluginsPolicy", "PluginsPolicy.json");
string alerts = LegacyHelpers.GetJsonPath("-alerts", "Alerts.toml");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
```

事件以独立 JSON 保存在 `BigCityLegacy/events/Race`、`CS` 和 `CS_Sur`；根目录由 `-eventsDir` 指定。该结构不使用单一 Events.json 示例。

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
string path = LegacyHelpers.GetJsonPath("-exampleConfig", "ExamplePlugin.json");

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
public static bool IsGameplayRunning { get; }
```

以下情况下返回 `false`：

- 正在运行服务器模式（`NetManager.isServer`）；
- 缺少 `Nuligine.me` 或 `Nuligine.RealGame`；
- 缺少有效的 `GameUI.me`，或 `GameUI.me.isUsed()` 返回 `false`；
- 正在加载（`Loading.me`）；
- 场景处于忙碌状态（`Scenes.nowBusy()`）；
- 正在进行淡入淡出过渡（`FadeUI.me && FadeUI.fadeState != FadeUI.FadeState.Unfaded`）；
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
        BigCityLegacy.Api.LegacyAlerts.Hint("Tool ready");
    }
}
```

## 适用场景

该方法适用于客户端快捷键、叠加界面、调试工具，以及其他不应在菜单、加载过程、淡入淡出过渡或服务器模式中触发的功能。

---

# `IsCsOrSurvivalMatchRunning`

检查本地玩家是否正在参加进行中的 CS 或 CS_Sur 比赛。

签名：

```csharp
public static bool IsCsOrSurvivalMatchRunning { get; }
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

## 示例：在 CS/CS_Sur 期间禁用某项功能

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
public static int GetBuildVersion { get; }
```

读取 `AlwaysOnline.me.buildVersionAsset.text`，通过 `AlwaysOnline.GetBuildVersionFromXML(text, false)` 获取 `curVersion`。任何错误均返回 `-1`。这是游戏构建版本，不是 BCL 版本或在线协议版本。

## 示例

```csharp
int buildVersion = LegacyHelpers.GetBuildVersion;
Debug.Log("Game build version: " + buildVersion);
```

---

# `LoadEmbeddedTexture(string resourceName)`

从 BigCityLegacy 程序集的内嵌资源加载 Texture2D。

签名：

```csharp
public static Texture2D LoadEmbeddedTexture(string resourceName)
```

工作流程：

1. 获取 `Assembly.GetExecutingAssembly()`；
2. 打开 `GetManifestResourceStream(resourceName)` 并读取资源字节；
3. 创建不含 mipmaps 的 `Texture2D(2, 2, TextureFormat.RGBA32, false)`；
4. 通过 `LoadImage(data)` 加载图像；
5. 将纹理命名为 `resourceName` 并返回。

资源不存在时记录 Error 并返回 `null`。若 `LoadImage` 返回 `false`，则销毁新纹理、记录 Error 并返回 `null`。

## 示例

```csharp
Texture2D logo = LegacyHelpers.LoadEmbeddedTexture("bcl-logo.png");
if (logo != null)
{
    Debug.Log(logo.name);
    // Release when the UI no longer needs the texture.
    UnityEngine.Object.Destroy(logo);
}
```

## 适用场景

用于加载内嵌在 BigCityLegacy 中的图像，例如供自己 UI 使用的标志。

## 注意

方法查找 BigCityLegacy.dll，而非调用方插件。加载插件自身资源时，使用 `typeof(MyPlugin).Assembly.GetManifestResourceStream(...)` 并自行加载 Texture2D。应在 Unity 主线程调用；不再使用的纹理通过 `UnityEngine.Object.Destroy` 释放。此 helper 不提供读取/解码异常的通用捕获。

---

# `isDebugBuild`

表示 BCL 是否使用 DEBUG 符号编译。

BCL 内部成员，不属于外部插件的公开 API。

签名：

```csharp
internal static bool isDebugBuild { get; }
```

在 `#if DEBUG` 下返回 `true`，否则为 `false`。取决于 BCL 构建，而非游戏的 `Debug.isDebugBuild` 或启动参数。

## 示例

```csharp
if (LegacyHelpers.isDebugBuild)
{
    Debug.Log("BCL debug diagnostics enabled");
}
```

## 适用场景

用于 BCL 内部诊断和调试分支。

---

# `VersionLine`

返回包含模组和游戏版本的字符串。

BCL 内部成员，不属于外部插件的公开 API。

签名：

```csharp
internal static string VersionLine { get; }
```

格式为 `BigCityLegacy v<VersionInfo.ModVersionName> | Game ver: <Application.version>`，访问属性时读取各值。

## 示例

```csharp
Debug.Log(LegacyHelpers.VersionLine);
```

## 适用场景

用于 BCL 内部启动、状态和诊断消息。

---

# `EnsureServerNetChat(NetManager manager)`

在服务器 NetChat 尚不存在时创建它。

BCL 内部成员，不属于外部插件的公开 API。

签名：

```csharp
internal static void EnsureServerNetChat(NetManager manager)
```

要求 `manager` 存在、处于服务器模式且 `NetworkServer` 已激活。已有 `NetChat.me` 时不执行操作。从 `manager.chat` 创建对象，检查 NetChat 和 NetworkIdentity，命名为 `NetChat_Server`，再通过 `NetworkServer.Spawn` 注册。Prefab、创建或注册错误写入日志；无效或注册失败的实例会被销毁。

## 示例

```csharp
if (NetManager.isServer && NetworkServer.active)
{
    LegacyHelpers.EnsureServerNetChat(NetManager.me);
}
```

## 适用场景

用于服务器进程（包括无图形启动）发送服务器消息前准备聊天系统。

---

# `EnsureServerUnetSettings(NetManager manager)`

运行中的服务器缺少 UNet_Settings 实例时创建它。

BCL 内部成员，不属于外部插件的公开 API。

签名：

```csharp
internal static void EnsureServerUnetSettings(NetManager manager)
```

检查 `manager`、服务器模式、活动 NetworkServer 和 `manager.unet_settings`。若 `FindObjectOfType<UNet_Settings>()` 找到实例则返回，否则实例化 prefab，命名为 `UNet_Settings_Server`，并调用 `NetworkServer.Spawn`。创建/注册过程没有通用 try/catch。

## 示例

```csharp
if (NetManager.isServer && NetworkServer.active)
{
    LegacyHelpers.EnsureServerUnetSettings(NetManager.me);
}
```

## 适用场景

用于服务器进程初始化原生网络设置。

---

# `ResetSubsStatus()`

一次性重置已保存的社交订阅状态。

BCL 内部成员，不属于外部插件的公开 API。

签名：

```csharp
internal static void ResetSubsStatus()
```

如果 PlayerPrefs 已有 `BigCityLegacy.isSubsReseted = 1` 则返回；否则设置此标记，将已有的 `Subs_Vk`、`Subs_Fb`、`Subs_Instagram` 键重置为 `0`，最后调用 `PlayerPrefs.Save()`。不创建缺少的订阅键。

## 示例

```csharp
LegacyHelpers.ResetSubsStatus();
```

## 适用场景

用于 BCL 初始化时对旧存档进行一次性准备。

---

# `LegacyCommandLine.HasArg(string arg)`

BCL 内部成员，不属于外部插件的公开 API。

位于 `LegacyCommandLine` 类中。
检查命令行标志是否存在，忽略大小写和引号。

签名：

```csharp
internal static bool HasArg(string arg)
```

该方法检查 BCL 有效参数，服务器进程中包含 ServerConfig 值。比较完整标志，忽略大小写和外层引号。`-key:value` 的值应通过 `GetArgValue` 读取，而非 HasArg。

## 示例

```csharp
if (LegacyCommandLine.HasArg("-noServerCli"))
{
    return;
}
```

---

# NativeErrorDialog

BCL 内部成员，不属于外部插件的公开 API。

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
# `LegacyServerShutdown`

管理服务器进程的延迟退出。

BCL 内部成员，不属于外部插件的公开 API。

签名：

```csharp
internal static bool IsRequested { get; }
internal static void Initialize();
internal static void Request(string requestReason);
internal static void Tick();
```

`Initialize()` 一次性订阅 `Application.quitting`。`Request(reason)` 原子设置停止请求并通知控制台线程，重复请求被忽略。`IsRequested` 表示是否存在请求。主线程中的 `Tick()` 一次性记录服务器退出消息并调用 `Application.Quit()`；quitting 处理器也会通知控制台线程。

## 示例

```csharp
// Once during BCL startup, on the main thread:
LegacyServerShutdown.Initialize();

// For example, from the server CLI:
LegacyServerShutdown.Request("console stop");

// In the plugin's main-thread Update:
LegacyServerShutdown.Tick();
```

## 适用场景

用于 stop 命令和其他 BCL 内部退出请求。`Request` 可从控制台线程调用；`Initialize` 和 `Tick` 应在 Unity 主线程调用。

---

[开发者文档](README.md).
