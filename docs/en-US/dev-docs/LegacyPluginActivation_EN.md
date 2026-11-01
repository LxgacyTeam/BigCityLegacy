# Plugin activation API

The BigCityLegacy 1.4.2 client API for MadOut2 9.4 lives in `BigCityLegacy.Api`. Reference `BigCityLegacy.dll` and declare a BepInEx dependency on `com.bigcitylegacy.baseplugin` version 1.4.2 or later. It manages the logical state of an already loaded BepInEx plugin; it does not load or unload DLLs.

## Contract and explicit support

The plugin's main class must declare both `[LegacyPluginActivation]` and `ILegacyPluginActivation`. Required members are `bool IsEnabled { get; }`, `void Enable()` and `void Disable()`. The attribute is not inherited. `[LegacyPluginActivation(false)]`, a missing attribute or a missing interface means unsupported: BCL neither reads the state nor invokes the methods.

`RequiresRestart` defaults to `true`. `Enable` and `Disable` persist the desired setting using the plugin's own storage. On restart, the plugin reads that setting and initializes itself. `IsEnabled` reports the actual state of this process, rather than the desired state saved for the next launch. Keep the plugin loaded in Chainloader when its feature is disabled, so BCL can enable it again.

For genuine switching without restart, set `RequiresRestart = false`: methods must apply the state before returning, and `IsEnabled` must immediately reflect it. Methods and the getter run on Unity's main thread. Implementations should be idempotent and synchronous; BCL does not await the plugin's background work. Do not restart inside `Enable`/`Disable`: BCL applies the entire batch and arranges one restart.

## Example with a persisted setting

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

This illustrates the contract, rather than implementing a particular map. Initialization and setting storage belong to the plugin. With `RequiresRestart = true`, `activeAtStartup` stays unchanged until the next launch.

## Reading and changing state

Call `LegacyPlugins.GetStatus(string pluginGuid)` on Unity's main thread. Its `LegacyPluginStatus` contains `Guid`, `Name`, `Version`, `Installed`, `SupportsActivation`, `IsEnabled`, `RequiresRestart`. Check `Installed` and `SupportsActivation` first; `IsEnabled` is authoritative only for a supported plugin. GUID comparison ignores case.

`Task<bool> LegacyPlugins.SetEnabledAsync(string pluginGuid, bool enabled, bool confirm = true)` asks for confirmation, invokes the appropriate method on the main thread and restarts through `LegacyGame` when required. `false` means the user declined; `true` means a completed hot change or an accepted restart attempt. A missing/unsupported plugin, a version change during confirmation or a method failure faults the Task. Hot changes additionally verify the resulting actual state.

```csharp
LegacyPluginStatus status = LegacyPlugins.GetStatus("com.example.map");
if (status.Installed && status.SupportsActivation && !status.IsEnabled)
{
    bool applied = await LegacyPlugins.SetEnabledAsync(status.Guid, true);
}
```

Use `await`; do not block the main thread with `.Result` or `.Wait()`. An `await` called from a worker does not give subsequent code permission to access Unity objects. Arbitrary changes already made by plugin methods before an exception cannot be rolled back automatically.

## Server policy

Clients report API support, actual state and restart requirements alongside the plugin list. Per-rule `controlByStatus` in `PluginsPolicy.json` opts into status checks. Otherwise the usual installation check applies. Plugins without API support and older clients without status reports also use the installation check.

In hardlist, a supported installed but disabled plugin triggers an enable offer. In blacklist, an active supported plugin triggers a disable offer, while a disabled one passes. Version constraints still apply. Missing plugins and incompatible versions require the usual installation changes.

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

The server ends the connection attempt before spawning and sends a bounded change request. The client displays the server endpoint, plugin names/GUIDs and any restart warning. Only approval invokes local methods. Declining keeps settings unchanged. Reconnect manually after changes; there is no automatic reconnect. A packet delivery acknowledgement is not user consent.

Every incompatibility must be fixable by a state change. A mixed failure, such as one disabled map and one missing plugin, displays the normal rejection without partial changes. A request contains at most 32 plugins. A new connection attempt invalidates an older unconfirmed request. `controlByStatus` cannot manage BCL itself; the same GUID cannot use it in both hardlist and blacklist.

The report is client-provided metadata, not proof against modified clients. The base plugin list remains compatible with older servers; online protocol remains 94.

[Dialogs and restart](LegacyDialogs_EN.md) · [Server](../user-docs/server_EN.md) · [Developer documentation](README.md)
