# LegacyHelpers

`LegacyHelpers` is a set of small internal BigCityLegacy helper methods for checking game state, working with paths, safely calling methods through reflection, and performing common routine tasks.

---

# `GameRoot`

Returns the path to the game root folder.

Signature:

```csharp
public static string GameRoot { get; }
```

How it works:

1. the base fallback is `Directory.GetCurrentDirectory()`;
2. if `Application.dataPath` is available, its parent folder is used;
3. if the path cannot be obtained through Unity, the fallback is returned.

For a regular Unity game, `Application.dataPath` points to a folder such as:

```text
game_Data
```

Therefore, `GameRoot` returns the parent directory, which is the game root.

## Example

```csharp
string root = LegacyHelpers.GameRoot;
string configFolder = Path.Combine(root, "BigCityLegacy");
```

## When to use

Use `GameRoot` when you need to build a path relative to the game folder: configs, JSON files, helper directories, or local mod resources.

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
string folder = Path.Combine(LegacyHelpers.GameRoot, "BigCityLegacy");
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
    LegacyHelpers.SafeInvoke(GameUI.me, "Refresh");
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
    typeof(MenuEsc),
    MenuEsc.me,
    "UpdateButtons");
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

---

# `GetJsonPath(string arg, string json)`

Returns the path to a configuration JSON file.

Signature:

```csharp
public static string GetJsonPath(string arg, string json)
```

How it works:

1. tries to get the command-line argument value through `LegacyCommandLine.GetArgValue(arg)`;
2. if the argument is not specified, builds the default path:

```text
<GameRoot>/BigCityLegacy/<json>
```

3. returns the path after `LegacyCommandLine.StripQuotes(...)`.

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
<GameRoot>/BigCityLegacy/ServersList.json
```

## Examples for existing configs

```csharp
string servers = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
string events = LegacyHelpers.GetJsonPath("-eventsList", "Events.json");
string respawn = LegacyHelpers.GetJsonPath("-respawnPoints", "RespawnPoints.json");
string bans = LegacyHelpers.GetJsonPath("-banList", "BanList.json");
```

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
string path = LegacyHelpers.GetJsonPath("-eventsList", "Events.json");

if (!LegacyHelpers.CheckFileExistence(path))
{
    return;
}

string json = File.ReadAllText(path);
```

## When to use

This method is suitable for configs and external JSON files where a missing file should not cause an unhandled exception.

---

# `IsGameplayRunning()`

Checks whether the client is in an active gameplay state.

Signature:

```csharp
public static bool IsGameplayRunning()
```

The method returns `false` if:

- server mode is running (`NetManager.isServer`);
- `Nuligine.me` or `Nuligine.RealGame` is missing;
- active `GameUI.me` is missing or `GameUI.me.isUsed()` returns `false`;
- loading is active (`Loading.me`);
- scenes are busy (`Scenes.nowBusy()`);
- a fade transition is in progress (`FadeUI.fadeState != FadeUI.FadeState.Unfaded`);
- the escape menu is open or menu selection is required (`MenuEsc.me || MenuEsc.needSelectWhere`);
- the phone is open (`GamePhone.me.gameObject.activeSelf`);
- local `InputControl` is missing;
- the local input does not have `current` or `currentOrParent`.

## Example: gameplay-only hotkey

```csharp
private void Update()
{
    if (!LegacyHelpers.IsGameplayRunning())
    {
        return;
    }

    if (Input.GetKeyDown(KeyCode.F8))
    {
        LegacyPointTool.Toggle();
    }
}
```

## When to use

This method is useful for client-side hotkeys, overlays, debug tools, and other features that should not trigger in menus, while loading, during fade transitions, or in server mode.

---

# `IsCsOrSurvivalMatchRunning()`

Checks whether the local player is in an active CS or CS_Survival match.

Signature:

```csharp
public static bool IsCsOrSurvivalMatchRunning()
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

## Example: disabling a feature during CS/CS_Survival

```csharp
if (LegacyHelpers.IsCsOrSurvivalMatchRunning())
{
    return;
}

FlyCamera.Create();
```

## When to use

This method is suitable for client-side features that must not interfere with competitive modes: free camera, debug tools, UI tools, and experimental hotkeys.

---

# `GetBuildVersion()`

Returns the game build version from the `AlwaysOnline` XML asset.

Signature:

```csharp
public static int GetBuildVersion()
```

## Example

```csharp
int buildVersion = LegacyHelpers.GetBuildVersion();
Debug.Log("Game build version: " + buildVersion);
```

## Important

The method assumes that `AlwaysOnline.me` already exists and `buildVersionAsset` is available. Do not call it too early during game startup without additional checks.

Safe variant:

```csharp
if (AlwaysOnline.me && AlwaysOnline.me.buildVersionAsset)
{
    int buildVersion = LegacyHelpers.GetBuildVersion();
}
```

---

# `LegacyCommandLine.HasArg(string arg)`

Located in `LegacyCommandLine` class.
Checks whether a command-line flag exists, ignoring case and quotes.

Signature:

```csharp
internal static bool HasArg(string arg)
```

The method iterates through `Environment.GetCommandLineArgs()` and returns `true` if the processed `arg` is present among the arguments. The argument is compared case-insensitively and with quotes stripped.

## Example

```csharp
if (LegacyCommandLine.HasArg("-noServerCli"))
{
    return;
}
```

---

# NativeErrorDialog

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
