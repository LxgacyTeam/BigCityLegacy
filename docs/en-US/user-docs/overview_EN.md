# Overview

**BigCityLegacy** is a project aimed at fully restoring online functionality for the Steam version of MadOut2 BigCityOnline in order to preserve the original gameplay experience and the history of the game.

The project is a game modification distributed as a BepInEx 5 plugin.

# Features and capabilities in detail

* **The mod removes the game's mandatory dependency on online servers** and provides full offline mode functionality.
* **The mod allows you to launch the game in server mode** and **connect to it easily through a separate menu**. The server has several modes, flexible configuration, and administration through an interactive CLI interface. See the Server section for details.
* **The server supports all main game modes**: FreeRoam, RP, Race, and Cops vs Bandits.
  *Online Parkour is planned for restoration in future releases and is not currently supported.*
* **The server has a full headless mode**, does not require graphics to run, and can work in a Windows or Linux console.
* **The server has a CLI interface and an administration system**: banning and kicking players, clearing cars and drops, and reloading configs. See the Server section.
* Ability to run a Mirror server that provides the client with a server list and server states. See the Server section.
* **You can create custom tracks for online races and zones for Cops vs Bandits.**
  The original so-called events, meaning tracks and zones, were fully lost because they were loaded from the server and are not stored in the game client. Therefore, loading custom events is required for these modes to work. The current release includes only a few events as templates for creating your own. See the Event creation section.
* The mod uses LegacyUIFramework, a custom framework for creating draggable windows and styled UI elements.
* All mod UI elements support localization and react to language changes in the game settings.
* The mod has a notification system for new updates.
* The mod includes Point Tool v2 for convenient creation of custom events: coordinate chains for checkpoints or positions.
* The mod adds several hotkeys:
  - `F5` - quick weather switch.
  - `F6` - instant spawn of the last selected car, without timeouts.
  - `F12` - emergency disconnect from the server, useful when the game gets stuck on a black screen.
* The mod can remove the FPS limit by setting the limit to 10000.
* The mod adds an option to enable player character shadows. This is enabled by default.

# Launch arguments

The mod has many launch parameters, or launch arguments, passed to the game executable. They are used mostly for launching the server, but there are also client-side parameters.

**Arguments are passed like this:**

```
game.exe -arg1 -arg2 -arg3
```

Arguments with values use a strict colon syntax:

```text
-key:value
-key:"value with spaces"
```

#### Client arguments


| Argument | Purpose | Accepted values |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------- |
| -connectIP:[value] | Set the game server address for direct connection. | Game server IP/domain. |
| -connectPort:[value] | Set the game server port for direct connection. | Game server port. |
| -useMaster | Automatically use the specified/saved master server. | - |
| -masterIP:[value] | Set the master server address. | Mirror server IP/domain. |
| -masterPort:[value] | Set the master server port. | Mirror server port. |
| -forceFullMode | Force the mod to run in full mode on game versions below 9.4. | - |
| -forceCompatMode | Force the mod to run in Compatibility Mode. | - |
| -noUpdCheck | Disable automatic update checking on game or server startup. | - |

#### Server arguments


| Argument | Purpose | Accepted values |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| -bend_GameServer | Launch the game in server mode. | - |
| -batchmode -nographics | Use headless mode. Recommended. | - |
| -port:[value] | Port of the launched game server. | 0-65535 |
| -maxConn:[value] | Maximum number of players on the launched server. | 1-100 |
| -Mode:[value] | Game mode on the server. | None / RP / Race / CS / CS_Sur |
| -maxCars:[value] | Maximum possible number of cars on the server.<br />Default: 100 | Number of cars; 0 = unlimited. |
| -maxCarsByPlayer:[value] | Maximum possible number of cars per player.<br />Default: 15 | Number of cars; 0 = unlimited. |
| -maxNikLength:[value] | Maximum possible player nickname length on the server.<br />Default: 20 | Nickname length; 0 = unlimited. |
| -noChatEvents | Disables chat notifications about server events, currently player connect/disconnect messages. | - |
| -idleKickTimeout:[value] | Timeout before kicking a player for inactivity, idle-kick.<br /><br />Default: 120s | `{n}` or `{n}s` - value in seconds; `{n}ms` - value in milliseconds. Examples: 120, 60s, 90000ms; 0 = disables idle-kick. |
| -useVanillaRespawn | Enables the original respawn system after death, disabling RespawnPoints.json. | - |
| -masterServer | Launch the mirror server. | - |
| -serversList:[path] | Manually specify the path to the mirror server config. By default, the config is read from BigCityLegacy/ServersList.json. Use only if you want to specify another file. | Path to the Mirror server config file. |
| -eventsList:[path] | Manually specify the path to the event list file. By default, the list is read from BigCityLegacy/Events.json. Use only if you want to specify another file. | Path to the event list file. |
| -respawnPoints:[path] | Manually specify the path to the respawn point list file. By default, the list is read from BigCityLegacy/RespawnPoints.json. Use only if you want to specify another file. | Path to the respawn point file. |
| -banList:[path] | Manually specify the path to the list of banned players. By default, the list is read from BigCityLegacy/BanList.json. Use only if you want to specify another file. | Path to the list of banned players. |
| -masterDebug | Print detailed logs about mirror server operation. | - |
| -verboseServerConsole | Print the full game log to the server console. Noisy mode. | - |
| -noServerConsole | Do not open the server console window. Fully background launch. | - |
| -noServerCLI | Disable the server CLI interface. | - |

#### Usage examples

Launch the game with a game server specified in advance:

```
game.exe -connectIP:1.2.3.4 -connectPort:7800
```

Launch the game with a mirror server specified in advance:

```
game.exe -useMaster -masterIP:1.2.3.4 -masterPort:35000
```

Launch the **server** in base mode, game server only:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race
```

Launch the **server** in master mode, game server + mirror:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer
```

Launch the **server** in master mode with custom limits:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer -maxCars:100 -maxCarsByPlayer:15 -maxNikLength:20 -idleKickTimeout:120s
```

Launch the **server** in master mode with custom config files:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer -serversList:myservers.json -eventsList:myevents.json
```

> [!NOTE]
> The mod package includes `runServer.cmd` and `runServer.sh` scripts for quickly launching a server. They provide convenient parameter configuration inside the files.

# Compatibility Mode

> [!NOTE]
> The mod fully supports only game version `9.4`, which is the latest Steam version.

When launched on earlier game versions, the mod runs in Compatibility Mode and attempts to provide basic functionality without online features.

> [!WARNING]
> Stable operation of the game and its offline functionality in Compatibility Mode is **not guaranteed**.

Compatibility Mode and support for older game versions are planned to be improved in the future.

# Connecting to a server

You can connect to a server through the **Server Connection** window.

There are 2 tabs: **Master Connection** and **Direct Connection**.

When connecting to a Master, the game receives the server list from it and displays it in the menu. The address and port are saved.

When using direct connection, the game immediately connects to the game server by IP and port. In this case, the online status will not be shown in the menu.
