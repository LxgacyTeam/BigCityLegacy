# Server

Game server settings are stored in `BigCityLegacy/config/ServerConfig.toml`. The template is `config_example/ServerConfig.toml`; the server creates the standard file when it is missing.

```text
game.exe -bend_GameServer -batchmode -nographics
game.exe -bend_GameServer -batchmode -nographics -serverConfig:"C:\Servers\race.toml"
```

The `-bend_GameServer` launch flag is required and is not a TOML setting. `-serverConfig` selects a file without enabling server mode. A regular game client does not read the server config. Explicit command-line options override TOML values and aliases. Empty paths use default files; relative paths resolve from the game root. Single quotes are convenient for Windows paths.

`masterServer = true` starts the mirror alongside the game server and is the default. Set `masterServer = false` for a game server without a mirror. The mirror port is configured by `masterPort` in TOML or `-masterPort:<port>`; `ServersList.json` contains the server list and polling intervals.

| Section | Options |
| --- | --- |
| `server` | `port` (1–65535), `maxConn` (1–100), `Mode` (`None`, `RP`, `Race`, `CS`, `CS_Sur`), `lang`, `maxCars`, `maxCarsByPlayer`, `CarSpawnTimeout`, `maxNikLength`, `idleKickTimeout`, `disconnectedKickTimeout`, `noChatEvents`, `noKillChat`, `noCsDamageCooldown`, `useVanillaRespawn` |
| `mirror` | `masterServer` (default `true`), `masterPort` (default `35000`), `masterDebug` |
| `files` | `serversList`, `eventsDir`, `respawnPoints`, `banList` |
| `console` | `noServerConsole`, `noServerCli`, `verboseServerConsole`, `noUpdCheck` |
| `logging` | `logsDir`, `noLogging`, `logLevel`, `serverDiagnostics` |
| `repository` | `repositorySource`: `GitHub`, `Codeberg`, or an empty string to use the saved PlayerPrefs choice |

Example configuration:

```toml
# BigCityLegacy server configuration. CLI arguments override these values. 
# Keys match launch options without the leading '-'.

[server]
port = 7800
maxConn = 16
Mode = "None"
lang = "en"
maxCars = 100
maxCarsByPlayer = 15
CarSpawnTimeout = 1000
maxNikLength = 20
idleKickTimeout = "120s"
disconnectedKickTimeout = "30s"
noChatEvents = false
noKillChat = false
noCsDamageCooldown = false
useVanillaRespawn = false

[mirror]
masterServer = true
masterPort = 35000
masterDebug = false

[files]
# Relative paths resolve from the game directory. Empty values use defaults.
serversList = ''
eventsDir = ''
respawnPoints = ''
banList = ''

[console]
noServerConsole = false
noServerCli = false
verboseServerConsole = false
noUpdCheck = false

[logging]
logsDir = ''
noLogging = false
logLevel = "Log"
serverDiagnostics = false
```

The game server can generally work in 2 modes: **Master** and **Base**.

**Base** runs the game server without a mirror (`masterServer = false`).
**Master** means Mirror + Game Server: in addition to the game server, a mirror is launched as well. In this case, the Master server should become the main server in an architecture with several game servers, connecting all of them together.

---

# Game Server

**Game Server** is the actual game server, representing one online lobby.

Game Server has 5 modes: `None`, `RP`, `Race`, `CS`, `CS_Sur`.

* **None** - the base mode, shown in the game as FreeRoam. All other modes are based on it.

- **RP** - None mode, but with weapons disabled.
- **Race** - None mode, but with periodically launched online race events.
- **CS** - None mode, but with periodically launched Cops vs Bandits events, effectively team deathmatch.
- **CS_Sur** - None mode, but with periodically launched Survival events, effectively deathmatch/FFA.

Game Server is configured with TOML settings and launch arguments. See Launch arguments. It also supports administration through an interactive CLI interface. See below.

There is no hard practical limit on the number of players on the server, but the game is designed for **no more than 100 players**.

---

# Mirror server

**Mirror server** is the server that handles the list of game servers and their state. It sends the game client the list of available servers, their settings, and current online count. In other words, it determines what the game shows in the server list in the **Online** section. Mirror can receive and send the server name, maximum player count, language, game mode, group placement, and some other parameters. It also provides the automatic server selection feature used when pressing the green Connect button in the game.

Mirror server configuration is stored in a separate JSON config. By default, it is stored in `BigCityLegacy/config/ServersList.json`. Example config:

```json
  {
  "version": 1,
  "pollIntervalMs": 1500,
  "staleAfterMs": 6000,
  "servers": [
    {
      "id": "master-self",
      "name": "RU FreeRoam #1",
      "connectIp": "127.0.0.1",
      "queryIp": "127.0.0.1",
      "port": 7800,
      "maxPlayers": 16,
      "lang": "ru",
      "menuGroup": "FreeRoam",
      "serverMode": "None",
      "onlineProtocolVersion": 93,
      "includeInAuto": true,
      "showIfOffline": false,
      "self": true
    }
  ]
}
```

**Parameter meanings:**

`pollIntervalMs` - interval for polling game servers to check whether they are online.

`staleAfterMs` - defines how long it takes for a server entry to be considered stale if the server is no longer available.

`servers` - array of game servers shown in the list.

`id` - unique ID for each server.

`name` - server name displayed in the menu.

`connectIp` - public server IP passed to the game client.

`queryIp` - server IP used by the mirror for polling. If the server is in the same local network or on the same machine, it is convenient to separate the public IP from the internal IP.

`port` - server port passed to the game client.

`maxPlayers` - maximum number of players on the server, displayed in the menu.

`lang` - server language displayed in the menu.

`menuGroup` - category where the server is displayed in the game menu. Accepted values: `FreeRoam`, `RP`, `Race`, or `CS`.

`serverMode` - game mode on the server.

`onlineProtocolVersion` - online protocol version. For game version 9.4, it is 93.

`includeInAuto` - whether the server is included in auto-connect selection.

`showIfOffline` - whether to show the server in the game if it is offline.

`self` - marks the master's own game server. SPECIFY ONLY ONCE.

A Master server is required if you want to run several game servers at once and display them as a list in the game. The expected and recommended architecture is `1 Master Server + n Base servers`.

---

# Administration and CLI interface

In an interactive Linux console (including tmux), ↑/↓ browse up to 100 recent nonempty commands. Consecutive duplicates are omitted. Moving down past the newest entry restores the unfinished input. ←/→, Home/End, Backspace/Delete and Ctrl+U (clear line) are supported. Long input scrolls horizontally; the line limit is 4096 characters. History lasts for the current process only. Log output preserves the input text. Redirected input and `TERM=dumb` use ordinary line input without interactive history.

While the game server is running, it can be controlled through the interactive CLI interface in the server console window. The following commands are supported:


| Command | Action |
| --------------------- | ------------------------------------------------------------------------------------------------- |
| players | Display the current list of players on the server. |
| playerinfo {PlayerID} | Display information about a player. |
| kick {PlayerID} | Kick a player. |
| ban {PlayerID} {1/2} | Ban a player; 1 = ban by GUID, 2 = ban by IP. |
| unban {BanID} | Unban a player by BanID. |
| reloadcfg | Reload configs: EventsList, RespawnPoints, BanList. |
| msg {text} | Send a message as the server. See note. |
| cleardrop | Clear all drops on the server: medkits and weapons. |
| delcars all | Delete all cars on the server. |
| delcars {PlayerID} | Delete all cars of a specific player. |
| stats | Display resource consumption and utilization statistics. |
| stop | Stop the server. |

> [!WARNING]
> At the moment, the `msg` command requires at least one player on the server and sends the message effectively on their behalf, only replacing the nickname with "Server". This is caused by the current chat architecture in the game.

#### PlayerID

Each player on the server is assigned a temporary unique **PlayerID** valid within their current game session. The PlayerID counter advances on every new player connection, is never cleared, and never decreases. If a player reconnects to the server, they receive a new PlayerID, and the old one will never be used again. PlayerIDs are displayed in the player list shown by the `players` command.

#### Ban system

A player can be banned in 2 different ways: by player GUID and by player IP.

GUID is a unique game profile identifier stored in PlayerPrefs and generated every time the game creates a new profile/save. In practice, a GUID ban can be easily bypassed by simply deleting the game save.

A player is unbanned by **BanID**, which is printed to the console after a ban, never changes, and is stored in the ban list.

The ban list is stored in `BanList.json`. If several server copies are launched, each one can use its own file specified through a launch argument.
Each ban entry is stored in the following format:

```json
    {
      "BanID": 1,
      "GUID": "1d9b4ba4-76f9-417f-b275-49e863726cb7",
      "IP": "1.2.3.4",
      "Type": 1,
      "Nickname": "BadPlayer"
    }

```
`Type` defines the ban type: 1 = GUID, 2 = IP.

Example command to ban a player by IP with PlayerID 52: `ban 52 2`.

Example command to unban a player with BanID 37: `unban 37`.

## Logging and diagnostics

Each server start creates a separate `BigCityLegacy/logs/port_yyyy-MM-dd_HH-mm-ss-fff.log` file. Filenames and record timestamps use UTC. It captures Unity messages from all threads, BepInEx logs without duplicating Unity messages, and BCL console output including entered CLI commands. Records include severity, source, thread ID and any stack trace supplied by Unity. Each record is flushed immediately; BCL retains no in-memory history. Atomic file creation prevents overwriting another instance's log when startup timestamps coincide. Existing logs are retained without automatic deletion.

The `[logging]` options also accept launch arguments:

| TOML | Argument | Purpose |
| --- | --- | --- |
| `logsDir = ''` | `-logsDir:"server-logs/instance-1"` | Log directory. An empty value uses `BigCityLegacy/logs`. Relative paths resolve from the game directory; absolute paths are supported. The directory is created automatically. |
| `noLogging = false` | `-noLogging` | Disable the BCL file log, which is enabled by default. |
| `logLevel = "Log"` | `-logLevel:Warning` | Minimum severity: `Log`, `Warning`, `Error`, `Exception`, from least to most severe. `Assert` maps to `Error`. |
| `serverDiagnostics = false` | `-serverDiagnostics` | Enable detailed diagnostic tracing. Use `logLevel = "Log"` for a complete trace. |

Diagnostics writes `ENTER`/`EXIT`/`THROW` records with operation ID, parent ID and nesting depth around network buffer reception, UNet dispatch, player initialization, car spawning, XML loading and parent assignment. It records string/buffer sizes, reader positions, message type, channel, connection ID and network object ID when available. It does not consume or modify packets or save packet/XML contents. Managed stacks are recorded at nesting depths 32 and 64; memory, GC and player counts are sampled every 30 seconds. Tracing increases disk traffic and is intended for investigations. `noLogging = true` disables diagnostics; ordinary runs do not install its network patches.

The native `m_log_<port>.log` is disabled by default. The launch-only **`-mlog`** flag enables it independently of BCL logging
