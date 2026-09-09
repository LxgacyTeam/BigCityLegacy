# Overview

**MadOutLegacy** is a project focused on fully restoring the online and offline functionality of the Steam version of MadOut2 BigCityOnline in order to preserve the original gameplay experience and the game's history.

The project is a game modification distributed as a plugin for BepInEx 5.

# Features and Functionality in Detail

* **The mod removes the game's mandatory dependency on online servers** and allows the game to work fully in offline mode.
* **The mod makes the game Steam-less**, meaning it removes the requirement to have Steam running in order to launch the game. Basic Steam functionality still remains available; for example, Steam can still track your online status and count the time spent in the game.
* **The mod allows you to launch the game in server mode** and **connect to it easily through a dedicated menu**. The server has several modes and flexible configuration options; see the Server section for details.
* **The server supports all main game modes**: FreeRoam, RP, Race, and Cops vs. Bandits.
  *Online parkour is planned for restoration in future releases and is currently supported only indirectly.*
* **The server has a full headless mode**, does not require graphics to launch, and can run in a Windows or Linux console. The mod fixes the broken `nographics` server mode in the Linux build of the game.
* Ability to launch a Mirror server that provides the client with a list of servers and their current status; see the Server section.
* **You can create custom tracks for online races and custom areas for the Cops vs. Bandits mode.**
  The original so-called events, meaning tracks and areas, have been completely lost because they were loaded from the server and were not included in the game client. Because of that, loading custom events is required for these modes to work. The current release includes only a few events as templates for creating your own; see the Event Creation section.
  **More ready-made events will be added later** to provide a more complete out-of-the-box experience. You can suggest and publish your own events in the Discussions section.
* The mod includes a small tool, Debug Overlay, that helps with creating custom events by copying camera coordinates and building coordinate chains. The tool is currently basic and is planned to be improved in the future.
* Coins and diamonds can be obtained for free by simply clicking the offers in the shop, opened using the "+" button in the top-right corner of the main menu.
* The mod adds several hotkeys:
  - `F5` - quickly switch the weather
  - `F6` - instantly spawn the last selected vehicle, without timeouts
  - `F12` - emergency disconnect from the server, useful if the game gets stuck on a black screen
* The mod can remove the FPS limit by setting the cap to 10000.

# Launch Arguments

The mod has many launch arguments that are passed to the game executable. They are mostly used for launching the server, but some arguments are available for the client as well.

**Arguments are passed like this:**

```
game.exe -arg1 -arg2 -arg3
```

#### Client Arguments

| Argument                  | Purpose                                                                     | Accepted values                 |
| ------------------------- | --------------------------------------------------------------------------- | ------------------------------- |
| -debugoverlay             | Launch the game with Debug Overlay for creating events                      | -                               |
| -connectIP:[value]        | Set the game server address for a direct connection                         | Game server IP/domain           |
| -connectPort:[value]      | Set the game server port for a direct connection                            | Game server port                |
| -useMaster                | Automatically use the specified/saved Mirror server                         | -                               |
| -masterIP:[value]         | Set the Mirror server address                                               | Mirror server IP/domain         |
| -masterPort:[value]       | Set the Mirror server port                                                  | Mirror server port              |
| -forceFullMode            | Force the mod to run in Full Mode on game versions below 9.4                | -                               |
| -forceCompatMode          | Force the mod to run in Compatibility Mode                                  | -                               |

#### Server Arguments

| Argument                  | Purpose                                                                                                                                               | Accepted values                              |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| -bend_GameServer          | Launch the game in server mode                                                                                                                        | -                                            |
| -batchmode -nographics    | Use headless mode; recommended                                                                                                                        | -                                            |
| -port:[value]             | Set the port of the game server being launched                                                                                                        | Game server port                             |
| -maxConn:[value]          | Set the maximum number of players on the server being launched                                                                                        | Maximum number of players on the server (1-100) |
| -Mode:[value]             | Set the game mode on the server                                                                                                                       | Game mode: None / RP / Race / CS / CS_Sur    |
| -masterServer             | Launch the Mirror server                                                                                                                              | -                                            |
| -serversList:[path]       | Manually set the path to the Mirror server config. By default, the config is read from `MadOutLegacy/ServersList.json`. Use only if you want another file. | Path to the Mirror server config file        |
| -eventsList:[path]        | Manually set the path to the event list file. By default, the list is read from `MadOutLegacy/Events.json`. Use only if you want another file.        | Path to the event list file                  |
| -masterDebug              | Print detailed logs about Mirror server operation                                                                                                     | -                                            |
| -verboseServerConsole     | Print the full game log to the server console; noisy mode                                                                                             | -                                            |

#### Usage Examples

Launch the game with Debug Overlay:

```
game.exe -debugoverlay
```

Launch the game with a predefined game server:

```
game.exe -connectIP:1.2.3.4 -connectPort:7800
```

Launch the game with a predefined Mirror server:

```
game.exe -useMaster -masterIP:1.2.3.4 -masterPort:35000
```

Launch a **server** in Base mode, meaning game server only:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race
```

Launch a **server** in Master mode, meaning game server + Mirror:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer
```

Launch a **server** in Master mode with custom config files:

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer -serversList:myservers.json -eventsList:myevents.json
```

Launch a **server** in graphical mode; not recommended:

```
game.exe -bend_GameServer -port:7800 -maxConn:32 -Mode:Race
```

> [!NOTE]
> The mod package includes `runServer.cmd` and `runServer.sh` scripts for quickly launching a server. They contain convenient settings for all parameters.

# Compatibility Mode

> [!NOTE]
> The mod fully supports only game version `9.4`, which is the final Steam version.

When launched on earlier game versions, the mod starts in Compatibility Mode and attempts to provide basic functionality, without online features.

> [!WARNING]
> Stable operation of the game and its offline functionality in Compatibility Mode **is not guaranteed**!

Compatibility Mode and support for older game versions are planned to be improved in the future.

# Connecting to a Server

You can connect to a server through the **Server Connection** window.

There are two tabs: **Mirror** and **Direct**.

When connecting through Mirror, the game receives the server list from it and displays that list in the menu. The address and port are saved. You can enable automatic connection to Mirror when the game starts by ticking "Auto resolve on start game".

In Direct mode, you can connect directly to a game server using its IP and port. In this case, the online status will not be displayed in the menu.
