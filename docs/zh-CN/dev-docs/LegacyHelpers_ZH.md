# LegacyHelpers

当前 BCL 1.4.2 实现参考：[LegacyHelpers.cs](../../../src/BigCityLegacy/Core/LegacyHelpers.cs)。`LegacyHelpers` 为全局命名空间中的公开静态类。外部插件需引用 BigCityLegacy.dll，并声明对 `com.bigcitylegacy.baseplugin` 的 BepInEx 依赖。涉及 Unity API 的成员应在主线程调用。

## 公开成员

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

## 路径与文件

`ModDataPath` 返回 `<GameRoot>/BigCityLegacy`。每次访问都会检查目录，并在缺失时尝试创建。创建失败会写入 Unity 日志，但仍返回路径。不存在 `LegacyHelpers.GameRoot` 属性；游戏根目录应使用 `BepInEx.Paths.GameRootPath`。

`GetJsonPath(arg, json)` 从 BCL 的有效参数读取 `arg`，服务器进程中包含 ServerConfig 设置。未指定或值为空时，构造 `<GameRoot>/BigCityLegacy/config/<json>` 并尝试创建 `config`。所选路径的外层引号会被移除，相对路径与游戏根目录组合。显式路径不会移入 `config`，此方法也不创建其父目录。它不读取、检查或创建文件。方法名称不限制扩展名，BCL 也用它定位 Alerts.toml。

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

ServersList 的默认路径为 `<GameRoot>/BigCityLegacy/config/ServersList.json`。事件使用 `BigCityLegacy/events/Race`、`CS`、`CS_Sur` 中的独立 JSON，以及 `-eventsDir` 参数；不再使用单一 Events.json 或 `-eventsList` 设置。

`CheckFileExistence(path)` 返回 `File.Exists` 的结果。文件缺失时，将 `Failed to load <path>: File not found` 写入 Unity error log，不加载或创建文件。

`OpenFolder(folderPath)` 要求目录已存在，否则记录错误并返回。在 WindowsPlayer 上将 `/` 转换为 `\` 并启动 `explorer.exe`；OSXPlayer 使用 `open`；LinuxPlayer 使用 `xdg-open`。原生启动抛出异常时，尝试 `Application.OpenURL("file://" + folderPath)`。方法不创建目录。平台判断针对 Player 构建，不保证 Unity Editor 中的行为。

```csharp
LegacyHelpers.OpenFolder(LegacyHelpers.ModDataPath);
```

## 时间

`GetCurTimestamp` 返回当前**操作系统本地时间**，格式为 `yyyy-MM-dd_HH-mm-ss-fff`。它是属性而非方法，不返回 Unix 时间戳。服务器日志的文件名和记录时间独立使用 UTC。

`FormatSeconds(seconds)` 使用 `TimeSpan.FromSeconds`，最多输出两个带英文缩写的时间单位：天/小时、小时/分钟、分钟/秒，或仅秒。应传入 TimeSpan 范围内的非负时长；溢出异常不会被捕获。

```csharp
string stamp = LegacyHelpers.GetCurTimestamp;
string shortTime = LegacyHelpers.FormatSeconds(90);     // 1 min. 30 sec.
string longTime = LegacyHelpers.FormatSeconds(90061);   // 1 d. 1 h.
```

## SafeInvoke

两个重载均通过反射调用无参数方法，查找范围为 `Instance | Static | Public | NonPublic`；找不到方法时跳过。调用同步执行，不返回方法结果或成功标志。接收 `Type` 的重载捕获查找/调用异常并记录 Warning。

在 `object` 重载中，传入 `Type` 表示静态调用。普通对象使用 `instance.GetType()`；传入 `null` 会在进入受保护的重载前抛出 `NullReferenceException`。不要传入需要参数的方法；存在多个同名重载时可能产生 `AmbiguousMatchException`，并写入日志。

```csharp
LegacyHelpers.SafeInvoke(typeof(UnityEngine.PlayerPrefs), "Save");
if (target != null)
    LegacyHelpers.SafeInvoke(target.GetType(), target, "Refresh");
```

## 游戏状态

`IsGameplayRunning` 检查客户端游戏状态。服务器模式下返回 false；缺少 `Nuligine.me`/`Nuligine.RealGame`、活动 GameUI、本地 InputControl 或其 `current`/`currentOrParent` 时也返回 false。Loading、场景忙碌、FadeUI 尚未结束、MenuEsc/菜单选择或手机打开时均返回 false。单纯缺少 FadeUI 不会导致失败。它不是连接检查，也不判断某个具体事件。

```csharp
private void Update()
{
    if (!LegacyHelpers.IsGameplayRunning) return;
    if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
        BigCityLegacy.Api.LegacyAlerts.Hint("Tool ready");
}
```

`IsCsOrSurvivalMatchRunning` 仅在 `NetManager.isOnlineClient` 下工作。先检查 `Net_BaseEvent.curUserInRaceInst`，再检查 `Net_BaseEvent.isCurUserAddedToPlayersListG()`。只有 `Net_CS` 处于 `Spawn_AfterLobby`、`BeginRace` 或 `Race` 时返回 true。大厅和已结束比赛不计入；检查对象为本地玩家，而非服务器上的所有比赛。

`GetBuildVersion` 读取 `AlwaysOnline.me.buildVersionAsset.text`，通过 `AlwaysOnline.GetBuildVersionFromXML(text, false)` 解析 XML，并返回 `curVersion`。发生错误（包括 AlwaysOnline 初始化前过早调用）时记录 Error 并返回 `-1`。这是游戏的数字构建版本，不是 BCL 版本或 `OnlineProtocolVersion`。

## 内嵌纹理

`LoadEmbeddedTexture(resourceName)` 在 **BigCityLegacy 程序集**中查找 manifest resource，不查找调用方插件。读取字节后，为新建的无 mipmaps RGBA32 纹理调用 `Texture2D.LoadImage`，并以资源名命名纹理。资源不存在时记录 Error 并返回 null。LoadImage 返回 false 时销毁新纹理、记录 Error 并返回 null。读取/解码异常没有通用捕获处理。调用方拥有返回的纹理，不再需要时应使用 `UnityEngine.Object.Destroy` 释放。

加载插件自身的内嵌资源时，应使用 `typeof(MyPlugin).Assembly.GetManifestResourceStream(...)` 并自行加载 Texture2D。

## BCL 内部辅助类

以下成员不是面向外部插件的公开 API：

| 成员 | 用途 |
| --- | --- |
| `LegacyHelpers.isDebugBuild` | 由 DEBUG 编译符号决定。 |
| `LegacyHelpers.VersionLine` | BCL 版本及 `Application.version` 字符串。 |
| `EnsureServerNetChat(NetManager)` | 活动 UNet 服务器缺少 NetChat 时创建并生成它；检查 prefab/NetworkIdentity，记录错误。 |
| `EnsureServerUnetSettings(NetManager)` | 活动服务器上仅在实例缺失时从 prefab 创建 UNet_Settings。 |
| `ResetSubsStatus()` | 一次性重置 PlayerPrefs 中的 Subs_Vk/Subs_Fb/Subs_Instagram，设置 BigCityLegacy.isSubsReseted 并保存。 |
| `LegacyCommandLine.HasArg(string)` | 内部类：在包含 ServerConfig 的有效参数中检查精确标志，忽略大小写和外层引号，不读取 `-key:value` 的值。 |
| `LegacyServerShutdown` | 内部停止请求、控制台线程通知及主线程 Application.Quit。 |

`NativeErrorDialog` 也是 **internal 类**，即使其方法 `Show(title, message)` 和 `ShowAndExit(title, message, exitCode = 1)` 标为 public。`Show` 同步显示阻塞对话框：Windows 使用 MessageBoxW，Linux 使用 kdialog/zenity，macOS 使用 osascript。失败时向 Console.Error 写入 fallback 并返回 false。`ShowAndExit` 随后调用 `Application.Quit(exitCode)`。这是内部严重启动错误机制，不是公开对话框 API。插件通知应使用 [LegacyAlerts](LegacyAlerts_ZH.md)。

[开发者文档](README.md)。
