# LegacyHelpers

Reference for the current BCL 1.4.2 implementation: [LegacyHelpers.cs](../../../src/BigCityLegacy/Core/LegacyHelpers.cs). `LegacyHelpers` is a public static class in the global namespace. External plugins must reference BigCityLegacy.dll and declare a BepInEx dependency on `com.bigcitylegacy.baseplugin`. Call members that use Unity APIs on the main thread.

## Public members

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

## Paths and files

`ModDataPath` returns `<GameRoot>/BigCityLegacy`. Every access checks the directory and attempts to create it if missing. Creation errors are logged to Unity, but the path is still returned. There is no `LegacyHelpers.GameRoot` property; use `BepInEx.Paths.GameRootPath` for the game root.

`GetJsonPath(arg, json)` reads `arg` from BCL's effective arguments, including ServerConfig settings in a server process. With no value or an empty value, it builds `<GameRoot>/BigCityLegacy/config/<json>` and attempts to create `config`. Outer quotes are stripped from the selected path; a relative path is combined with the game root. An explicit path is not moved into `config`, and this method does not create its parent directory. It does not read, check or create the file. The method name does not restrict extensions: BCL also uses it for Alerts.toml.

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

The default ServersList path is `<GameRoot>/BigCityLegacy/config/ServersList.json`. Events use separate JSON files in `BigCityLegacy/events/Race`, `CS`, `CS_Sur` and the `-eventsDir` option; there is no single Events.json file or current `-eventsList` setting.

`CheckFileExistence(path)` returns `File.Exists`. A missing file produces `Failed to load <path>: File not found` in the Unity error log. It neither loads nor creates the file.

`OpenFolder(folderPath)` requires an existing directory; otherwise it logs an error and returns. On WindowsPlayer it normalizes `/` to `\` and starts `explorer.exe`; OSXPlayer uses `open`, LinuxPlayer uses `xdg-open`. If the native launch throws, it tries `Application.OpenURL("file://" + folderPath)`. It does not create directories. Platform checks target Player builds; Unity Editor behavior is not guaranteed.

```csharp
LegacyHelpers.OpenFolder(LegacyHelpers.ModDataPath);
```

## Time

`GetCurTimestamp` returns the current **local OS time** as `yyyy-MM-dd_HH-mm-ss-fff`. It is a property, not a method, and does not return a Unix timestamp. Server log filenames and record times use UTC independently of this helper.

`FormatSeconds(seconds)` uses `TimeSpan.FromSeconds` and returns up to two time units with English abbreviations: days/hours, hours/minutes, minutes/seconds, or seconds alone. Supply a nonnegative duration within TimeSpan's range; overflow is not caught.

```csharp
string stamp = LegacyHelpers.GetCurTimestamp;
string shortTime = LegacyHelpers.FormatSeconds(90);     // 1 min. 30 sec.
string longTime = LegacyHelpers.FormatSeconds(90061);   // 1 d. 1 h.
```

## SafeInvoke

Both overloads invoke a parameterless method through reflection. Lookup uses `Instance | Static | Public | NonPublic`; a missing method is skipped. Calls are synchronous and return neither the method's result nor a success flag. The overload taking `Type` catches lookup/invocation errors and logs a Warning.

In the `object` overload, passing a `Type` requests a static call. An ordinary object uses `instance.GetType()`; passing `null` throws `NullReferenceException` before reaching the protected overload. Do not use methods with arguments; multiple same-named overloads can produce an `AmbiguousMatchException`, which is logged.

```csharp
LegacyHelpers.SafeInvoke(typeof(UnityEngine.PlayerPrefs), "Save");
if (target != null)
    LegacyHelpers.SafeInvoke(target.GetType(), target, "Refresh");
```

## Game state

`IsGameplayRunning` checks client gameplay state. It returns false on a server; without `Nuligine.me`/`Nuligine.RealGame`, active GameUI, local InputControl or its `current`/`currentOrParent`; during Loading, busy scenes or an unfinished FadeUI; while MenuEsc/menu selection or the phone is open. Missing FadeUI alone does not fail the check. This is not a connection test or a test for a specific event.

```csharp
private void Update()
{
    if (!LegacyHelpers.IsGameplayRunning) return;
    if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
        BigCityLegacy.Api.LegacyAlerts.Hint("Tool ready");
}
```

`IsCsOrSurvivalMatchRunning` works only for `NetManager.isOnlineClient`. It checks `Net_BaseEvent.curUserInRaceInst`, then `Net_BaseEvent.isCurUserAddedToPlayersListG()`. It returns true only for `Net_CS` in `Spawn_AfterLobby`, `BeginRace` or `Race`. Lobby and completed matches are excluded. This checks the local player's participation, not all matches on a server.

`GetBuildVersion` reads `AlwaysOnline.me.buildVersionAsset.text`, parses XML through `AlwaysOnline.GetBuildVersionFromXML(text, false)` and returns `curVersion`. On error, including an early call before AlwaysOnline initialization, it logs Error and returns `-1`. This is the game's numeric build, not the BCL version or `OnlineProtocolVersion`.

## Embedded textures

`LoadEmbeddedTexture(resourceName)` looks up a manifest resource **inside the BigCityLegacy assembly**, not the calling plugin. It reads bytes and calls `Texture2D.LoadImage` on a new RGBA32 texture without mipmaps; the texture name is set to the resource name. A missing resource logs Error and returns null. A false return from LoadImage destroys the new texture, logs Error and returns null. Read/decode exceptions have no general catch handler. The caller owns the returned texture and should release it with `UnityEngine.Object.Destroy` when no longer needed.

For a plugin's own embedded resource, use `typeof(MyPlugin).Assembly.GetManifestResourceStream(...)` and load the Texture2D yourself.

## Internal BCL helpers

The following members are not public APIs for external plugins:

| Member | Purpose |
| --- | --- |
| `LegacyHelpers.isDebugBuild` | Determined by the DEBUG compilation symbol. |
| `LegacyHelpers.VersionLine` | BCL version and `Application.version` text. |
| `EnsureServerNetChat(NetManager)` | Creates and spawns NetChat on an active UNet server if absent; checks the prefab/NetworkIdentity and logs errors. |
| `EnsureServerUnetSettings(NetManager)` | Creates UNet_Settings from its prefab on an active server only when no instance exists. |
| `ResetSubsStatus()` | Once resets Subs_Vk/Subs_Fb/Subs_Instagram in PlayerPrefs, sets BigCityLegacy.isSubsReseted and saves PlayerPrefs. |
| `LegacyCommandLine.HasArg(string)` | Internal class: checks an exact flag, ignoring case and outer quotes, among effective arguments including ServerConfig. It does not read a `-key:value` value. |
| `LegacyServerShutdown` | Internal shutdown request, console-thread signaling and main-thread Application.Quit. |

`NativeErrorDialog` is also an **internal class**, despite its public `Show(title, message)` and `ShowAndExit(title, message, exitCode = 1)` methods. `Show` synchronously displays a blocking dialog: MessageBoxW on Windows, kdialog/zenity on Linux, osascript on macOS. Failure writes a fallback to Console.Error and returns false. `ShowAndExit` then calls `Application.Quit(exitCode)`. It is an internal critical-startup-error mechanism, not a public dialog API. Plugins should use [LegacyAlerts](LegacyAlerts_EN.md) for notifications.

[Developer documentation](README.md).
