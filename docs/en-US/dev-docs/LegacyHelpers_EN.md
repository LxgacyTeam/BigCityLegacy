# LegacyHelpers

`LegacyHelpers` is a set of BigCityLegacy helpers for checking game state, working with paths, time and resources, safely invoking methods through reflection, and performing routine tasks.

The class is public and belongs to the global namespace. External plugins need a BigCityLegacy.dll reference and a BepInEx dependency on `com.bigcitylegacy.baseplugin`. Members with `internal` visibility are marked in their sections; those examples apply to BCL code itself. Call members that use Unity APIs on the main thread.

---

# `ModDataPath`

Returns the path to the BigCityLegacy folder located in the game's root folder.

Signature:

```csharp
public static string ModDataPath { get; }
```

Each access checks the directory and attempts to create it if missing. A creation failure logs Error to Unity but still returns the path.

## Example

```csharp
string data = LegacyHelpers.ModDataPath;
string config = Path.Combine(data, "config");
```

## When to use

The `BigCityLegacy` folder is used to store the mod's user data: configs, JSON files, auxiliary directories, and local mod resources.

---

# `GetCurTimestamp`

Returns the current date and time as a string suitable for a filename.

Signature:

```csharp
public static string GetCurTimestamp { get; }
```

Each access reads `DateTime.Now` and formats it as `yyyy-MM-dd_HH-mm-ss-fff`, for example `2026-10-31_14-25-36-123`. It uses the operating system's local time.

## Example

```csharp
string stamp = LegacyHelpers.GetCurTimestamp;
string filename = "backup_" + stamp + ".json";
```

## When to use

Use it for exported filenames, backups, and diagnostic snapshots.

## Important

This is a property, not a method or Unix timestamp. The BCL server logger uses UTC independently of this helper.

---

# `FormatSeconds(long seconds)`

Converts a duration in seconds to a short string.

Signature:

```csharp
public static string FormatSeconds(long seconds)
```

Uses `TimeSpan.FromSeconds(seconds)` and selects up to two time units:

- one day or more: days and hours;
- one hour or more: hours and minutes;
- one minute or more: minutes and seconds;
- less than a minute: seconds.

The abbreviations `d.`, `h.`, `min.`, `sec.` are fixed and do not follow the game language.

## Example

```csharp
string a = LegacyHelpers.FormatSeconds(45);     // 45 sec.
string b = LegacyHelpers.FormatSeconds(90);     // 1 min. 30 sec.
string c = LegacyHelpers.FormatSeconds(3660);   // 1 h. 1 min.
string d = LegacyHelpers.FormatSeconds(90061);  // 1 d. 1 h.
```

## When to use

Use it to display uptime, waits, restrictions, and other time intervals.

## Important

Pass a nonnegative duration within TimeSpan's supported range. The method does not clamp the input or catch overflow.

---

# `OpenFolder(string folderPath)`

Opens a folder in the operating system file manager.

Signature:

```csharp
public static void OpenFolder(string folderPath)
```

Behavior:

- on Windows, replaces `/` with `\`;
- checks whether the directory exists through `Directory.Exists(...)`;
- on Windows, launches `explorer.exe`;
- on macOS, launches `open`;
- on Linux, launches `xdg-open`;
- if the native method does not work, tries `Application.OpenURL("file://" + folderPath)`;
- logs an error message to the Unity log on failure.

## Example

```csharp
string folder = Path.Combine(BepInEx.Paths.GameRootPath, "BigCityLegacy");
LegacyHelpers.OpenFolder(folder);
```

## When to use

This method is convenient for UI buttons that open folders with configs, logs, or user files.

## Important

The method expects a path to an existing directory. If the directory does not exist, it is not created automatically.

---

# `SafeInvoke(object instance, string methodName)`

Safely calls a parameterless method on an object through reflection.

Signature:

```csharp
public static void SafeInvoke(object instance, string methodName)
```

The method supports two scenarios:

```csharp
LegacyHelpers.SafeInvoke(someObject, "SomeMethod");
LegacyHelpers.SafeInvoke(typeof(SomeType), "SomeStaticMethod");
```

If `instance` itself is a `Type`, the call is redirected to the overload:

```csharp
SafeInvoke((Type)instance, null, methodName);
```

Otherwise, the type is taken from `instance.GetType()`.

## Example: calling an instance method

```csharp
if (GameUI.me)
{
    LegacyHelpers.SafeInvoke(GameUI.me, "UpResolution");
}
```

## Example: calling a static method

```csharp
LegacyHelpers.SafeInvoke(typeof(PlayerPrefs), "Save");
```

## Important

`instance` must not be `null`. In the current implementation, the object overload calls `instance.GetType()` before entering the second overload, so null must be checked beforehand.

```csharp
if (target != null)
{
    LegacyHelpers.SafeInvoke(target, "MethodName");
}
```

---

# `SafeInvoke(Type type, object instance, string methodName)`

Safely calls a parameterless method through reflection when the type is known explicitly.

Signature:

```csharp
public static void SafeInvoke(Type type, object instance, string methodName)
```

The method searches for `methodName` with the following flags:

```csharp
BindingFlags.Instance |
BindingFlags.Static |
BindingFlags.Public |
BindingFlags.NonPublic
```

If the method is found, it is called like this:

```csharp
method.Invoke(instance, null);
```

Call errors are caught and logged as warnings:

```text
[BigCityLegacy] SafeInvoke failed: <methodName> -> <message>
```

## Example: private instance method

```csharp
LegacyHelpers.SafeInvoke(
    typeof(GameUI),
    GameUI.me,
    "CheckActualDefferedRendeing");
```

## Example: static method

```csharp
LegacyHelpers.SafeInvoke(
    typeof(PlayerPrefs),
    null,
    "Save");
```

## When to use

Use `SafeInvoke` when you need to call a method from the original game without a hard compile-time dependency on its availability or access level.

The method is intended only for methods **without parameters**. For methods with arguments, use separate reflection code.

A missing method is skipped. Lookup errors, including ambiguous overloads, are also logged as Warning. The helper returns neither the invoked method's result nor a success flag.

---

# `GetJsonPath(string arg, string json)`

Returns the path to a configuration JSON file.

Signature:

```csharp
public static string GetJsonPath(string arg, string json)
```

How it works:

1. reads the value through `LegacyCommandLine.GetArgValue(arg)` from BCL's effective arguments; server processes include ServerConfig values;
2. if the argument is not specified, builds the default path:

```text
<GameRoot>/BigCityLegacy/config/<json>
```

3. strips outer quotes through `LegacyCommandLine.StripQuotes(...)`;
4. combines relative paths with `BepInEx.Paths.GameRootPath` through `Path.Combine`.

The default branch attempts to create `BigCityLegacy/config`. An explicit path is not moved there, and this helper does not create its parent directory. It neither reads nor creates the file itself. The method name does not restrict extensions: Alerts uses TOML, for example.

`LegacyCommandLine.GetArgValue(...)` is designed for the strict format:

```text
-key:value
-key:"value"
```

## Example

```csharp
string serversListPath = LegacyHelpers.GetJsonPath(
    "-serversList",
    "ServersList.json");
```

CLI:

```text
-serversList:"C:\Big City\ServersList.json"
```

If the argument is not specified, this path will be used:

```text
<GameRoot>/BigCityLegacy/config/ServersList.json
```

## Examples for existing configs

```csharp
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string policy = LegacyHelpers.GetJsonPath("-pluginsPolicy", "PluginsPolicy.json");
string alerts = LegacyHelpers.GetJsonPath("-alerts", "Alerts.toml");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
```

Events are separate JSON files in `BigCityLegacy/events/Race`, `CS` and `CS_Sur`; `-eventsDir` selects their root directory. A single Events.json example does not apply to this layout.

---

# `CheckFileExistence(string path)`

Checks whether a file exists and writes an error to the log if the file is not found.

Signature:

```csharp
public static bool CheckFileExistence(string path)
```

Behavior:

- returns `true` if `File.Exists(path)`;
- returns `false` if the file does not exist;
- when the file is missing, writes:

```text
Failed to load <path>: File not found
```

## Example

```csharp
string path = LegacyHelpers.GetJsonPath("-exampleConfig", "ExamplePlugin.json");

if (!LegacyHelpers.CheckFileExistence(path))
{
    return;
}

string json = File.ReadAllText(path);
```

## When to use

This method is suitable for configs and external JSON files where a missing file should not cause an unhandled exception.

---

# `IsGameplayRunning`

Checks whether the client is in an active gameplay state.

Signature:

```csharp
public static bool IsGameplayRunning { get; }
```

The method returns `false` if:

- server mode is running (`NetManager.isServer`);
- `Nuligine.me` or `Nuligine.RealGame` is missing;
- active `GameUI.me` is missing or `GameUI.me.isUsed()` returns `false`;
- loading is active (`Loading.me`);
- scenes are busy (`Scenes.nowBusy()`);
- a fade transition is in progress (`FadeUI.me && FadeUI.fadeState != FadeUI.FadeState.Unfaded`);
- the escape menu is open or menu selection is required (`MenuEsc.me || MenuEsc.needSelectWhere`);
- the phone is open (`GamePhone.me.gameObject.activeSelf`);
- local `InputControl` is missing;
- the local input does not have `current` or `currentOrParent`.

## Example: gameplay-only hotkey

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

## When to use

This method is useful for client-side hotkeys, overlays, debug tools, and other features that should not trigger in menus, while loading, during fade transitions, or in server mode.

---

# `IsCsOrSurvivalMatchRunning`

Checks whether the local player is in an active CS or CS_Sur match.

Signature:

```csharp
public static bool IsCsOrSurvivalMatchRunning { get; }
```

The method works only for an online client:

```csharp
if (!NetManager.isOnlineClient)
{
    return false;
}
```

Then it checks two sources of the current event state:

```csharp
Net_BaseEvent.curUserInRaceInst
Net_BaseEvent.isCurUserAddedToPlayersListG()
```

The event is considered active if it is `Net_CS` and is in one of these states:

```csharp
Net_BaseEvent.CurState.Spawn_AfterLobby
Net_BaseEvent.CurState.BeginRace
Net_BaseEvent.CurState.Race
```

Lobby and complete states are not considered an active match.

## Example: disabling a feature during CS/CS_Sur

```csharp
if (LegacyHelpers.IsCsOrSurvivalMatchRunning)
{
    return;
}

FlyCamera.Create();
```

## When to use

This method is suitable for client-side features that must not interfere with competitive modes: free camera, debug tools, UI tools, and experimental hotkeys.

---

# `GetBuildVersion`

Returns the game build version from the `AlwaysOnline` XML asset.
Writes an error to log if AlwaysOnline not initialized yet.

Signature:

```csharp
public static int GetBuildVersion { get; }
```

Reads `AlwaysOnline.me.buildVersionAsset.text` and gets `curVersion` through `AlwaysOnline.GetBuildVersionFromXML(text, false)`. Any error returns `-1`. This is the game build, not the BCL version or online protocol version.

## Example

```csharp
int buildVersion = LegacyHelpers.GetBuildVersion;
Debug.Log("Game build version: " + buildVersion);
```

---

# `LoadEmbeddedTexture(string resourceName)`

Loads a Texture2D from an embedded resource in the BigCityLegacy assembly.

Signature:

```csharp
public static Texture2D LoadEmbeddedTexture(string resourceName)
```

How it works:

1. gets `Assembly.GetExecutingAssembly()`;
2. opens `GetManifestResourceStream(resourceName)` and reads the resource bytes;
3. creates `Texture2D(2, 2, TextureFormat.RGBA32, false)` without mipmaps;
4. loads the image through `LoadImage(data)`;
5. assigns `resourceName` as the texture name and returns it.

A missing resource logs Error and returns `null`. If `LoadImage` returns `false`, the created texture is destroyed, Error is logged, and `null` is returned.

## Example

```csharp
Texture2D logo = LegacyHelpers.LoadEmbeddedTexture("bcl-logo.png");
if (logo != null)
{
    Debug.Log(logo.name);
    // Release when the UI no longer needs the texture.
    UnityEngine.Object.Destroy(logo);
}
```

## When to use

Use it for images embedded specifically in BigCityLegacy, such as a logo for your UI.

## Important

The method searches BigCityLegacy.dll, not the calling plugin. For your plugin's resources, use `typeof(MyPlugin).Assembly.GetManifestResourceStream(...)` and load the Texture2D yourself. Call this on Unity's main thread. Release unused textures with `UnityEngine.Object.Destroy`; the helper has no general handler for read/decode exceptions.

---

# `isDebugBuild`

Reports whether BCL was compiled with the DEBUG symbol.

Internal BCL member; not a public API for external plugins.

Signature:

```csharp
internal static bool isDebugBuild { get; }
```

Returns `true` under `#if DEBUG`, otherwise `false`. It depends on the BCL build, not the game's `Debug.isDebugBuild` or a launch argument.

## Example

```csharp
if (LegacyHelpers.isDebugBuild)
{
    Debug.Log("BCL debug diagnostics enabled");
}
```

## When to use

Use it for internal BCL diagnostics and debug branches.

---

# `VersionLine`

Returns a string containing the mod and game versions.

Internal BCL member; not a public API for external plugins.

Signature:

```csharp
internal static string VersionLine { get; }
```

Format: `BigCityLegacy v<VersionInfo.ModVersionName> | Game ver: <Application.version>`. Values are read when the property is accessed.

## Example

```csharp
Debug.Log(LegacyHelpers.VersionLine);
```

## When to use

Use it for internal BCL startup, status, and diagnostic messages.

---

# `EnsureServerNetChat(NetManager manager)`

Creates the server NetChat if it does not exist yet.

Internal BCL member; not a public API for external plugins.

Signature:

```csharp
internal static void EnsureServerNetChat(NetManager manager)
```

Requires `manager`, server mode, and an active `NetworkServer`. Does nothing if `NetChat.me` already exists. Creates an object from `manager.chat`, checks NetChat and NetworkIdentity, names it `NetChat_Server`, and registers it with `NetworkServer.Spawn`. Prefab, creation, and spawn errors are logged; invalid or unsuccessfully spawned instances are destroyed.

## Example

```csharp
if (NetManager.isServer && NetworkServer.active)
{
    LegacyHelpers.EnsureServerNetChat(NetManager.me);
}
```

## When to use

Use it to prepare chat in a server process, including headless startup, before sending server messages.

---

# `EnsureServerUnetSettings(NetManager manager)`

Creates UNet_Settings for a running server when no instance exists.

Internal BCL member; not a public API for external plugins.

Signature:

```csharp
internal static void EnsureServerUnetSettings(NetManager manager)
```

Checks `manager`, server mode, active NetworkServer, and `manager.unet_settings`. Returns if `FindObjectOfType<UNet_Settings>()` finds an instance. Otherwise instantiates the prefab, names it `UNet_Settings_Server`, and calls `NetworkServer.Spawn`. Creation/spawn has no general try/catch handler.

## Example

```csharp
if (NetManager.isServer && NetworkServer.active)
{
    LegacyHelpers.EnsureServerUnetSettings(NetManager.me);
}
```

## When to use

Use it to initialize native network settings in a server process.

---

# `ResetSubsStatus()`

Resets saved social-subscription states once.

Internal BCL member; not a public API for external plugins.

Signature:

```csharp
internal static void ResetSubsStatus()
```

Returns if PlayerPrefs already contains `BigCityLegacy.isSubsReseted = 1`. Otherwise sets that marker, resets existing `Subs_Vk`, `Subs_Fb`, `Subs_Instagram` keys to `0`, and calls `PlayerPrefs.Save()`. Missing subscription keys are not created.

## Example

```csharp
LegacyHelpers.ResetSubsStatus();
```

## When to use

Use it for one-time preparation of an older save during BCL initialization.

---

# `LegacyCommandLine.HasArg(string arg)`

Internal BCL member; not a public API for external plugins.

Located in `LegacyCommandLine` class.
Checks whether a command-line flag exists, ignoring case and quotes.

Signature:

```csharp
internal static bool HasArg(string arg)
```

The method checks BCL's effective arguments, including ServerConfig values in a server process. It compares the complete flag, ignoring case and outer quotes. Read `-key:value` values with `GetArgValue`, not HasArg.

## Example

```csharp
if (LegacyCommandLine.HasArg("-noServerCli"))
{
    return;
}
```

---

# NativeErrorDialog

Internal BCL member; not a public API for external plugins.

`NativeErrorDialog` is a separate helper class for showing a blocking native error dialog with an OK button.

The class is located separately from `LegacyHelpers` and is used for critical startup errors when the regular Unity UI may be unavailable or not initialized yet.

## `Show(string title, string message)`

Shows an error dialog and returns `true` if a graphical dialog was shown successfully.

Signature:

```csharp
public static bool Show(string title, string message)
```

Platform behavior:

- Windows: `MessageBoxW` from `user32.dll`;
- Linux: first `kdialog`, then `zenity`;
- macOS: `/usr/bin/osascript` with AppleScript `display alert`;
- if a graphical dialog cannot be shown, writes a fallback message to `Console.Error`.

## Example

```csharp
NativeErrorDialog.Show(
    "BigCityLegacy Startup Error",
    "Failed to load required configuration file.");
```

## `ShowAndExit(string title, string message, int exitCode = 1)`

Shows an error dialog and exits the application through `Application.Quit(exitCode)`.

Signature:

```csharp
public static void ShowAndExit(
    string title,
    string message,
    int exitCode = 1)
```

## Example

```csharp
NativeErrorDialog.ShowAndExit(
    "BigCityLegacy Startup Error",
    "Outdated plugin version detected.",
    1);
```

## When to use

Use `NativeErrorDialog` for critical startup errors where continuing startup is dangerous: an old plugin conflict, incompatible version, or missing required files.

For regular runtime errors, prefer the BepInEx logger or Unity `Debug.LogError` so the game is not blocked by a native dialog.

In headless/server mode, a dialog may be undesirable. On a server, it is usually better to write the error to the console/log and terminate the process through the standard server mechanism.

---
# `LegacyServerShutdown`

Manages deferred shutdown of the server process.

Internal BCL member; not a public API for external plugins.

Signature:

```csharp
internal static bool IsRequested { get; }
internal static void Initialize();
internal static void Request(string requestReason);
internal static void Tick();
```

`Initialize()` subscribes to `Application.quitting` once. `Request(reason)` atomically marks shutdown requested and signals the console thread; repeated requests are ignored. `IsRequested` reports the request state. Main-thread `Tick()` logs the server shutdown message and calls `Application.Quit()` once. The quitting handler also signals the console thread.

## Example

```csharp
// Once during BCL startup, on the main thread:
LegacyServerShutdown.Initialize();

// For example, from the server CLI:
LegacyServerShutdown.Request("console stop");

// In the plugin's main-thread Update:
LegacyServerShutdown.Tick();
```

## When to use

Use it for stop and other internal BCL shutdown requests. `Request` may be called from the console thread; call `Initialize` and `Tick` on Unity's main thread.

---

[Developer documentation](README.md).
