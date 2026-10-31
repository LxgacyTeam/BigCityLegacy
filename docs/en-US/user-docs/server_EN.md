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
| `server` | `port` (1–65535), `maxConn` (1–100), `Mode` (`None`, `RP`, `Race`, `CS`, `CS_Sur`), `lang`, `maxCars`, `maxCarsByPlayer`, `maxVinylLayers`, `CarSpawnTimeout`, `maxNikLength`, `idleKickTimeout`, `disconnectedKickTimeout`, `noChatEvents`, `noKillChat`, `noCsDamageCooldown`, `useVanillaRespawn`, `requireFingerprint` |
| `plugins` | `noPluginsPolicyAndAlerts` |
| `mirror` | `masterServer` (default `true`), `masterPort` (default `35000`), `masterDebug` |
| `files` | `serversList`, `eventsDir`, `respawnPoints`, `banList`, `pluginsPolicy`, `alerts` |
| `console` | `noServerConsole`, `noServerCli`, `verboseServerConsole`, `noUpdCheck` |
| `logging` | `logsDir`, `noLogging`, `chatLogging`, `logLevel` |

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
maxVinylLayers = 1024
CarSpawnTimeout = 1000
maxNikLength = 20
idleKickTimeout = "120s"
disconnectedKickTimeout = "30s"
noChatEvents = false
noKillChat = false
noCsDamageCooldown = false
useVanillaRespawn = false
requireFingerprint = false

[plugins]
noPluginsPolicyAndAlerts = false

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
pluginsPolicy = ''
alerts = ''

[console]
noServerConsole = false
noServerCli = false
verboseServerConsole = false
noUpdCheck = false

[logging]
logsDir = ''
noLogging = false
chatLogging = false
logLevel = "Log"
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
      "onlineProtocolVersion": 94,
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

`onlineProtocolVersion` is the online protocol version, default `94`. The global value is the `OnlineProtocolVersion` constant in `BigCityLegacyPlugin.cs`. The mirror uses that value for its own `self` entry. Entries for other servers may explicitly specify their version. The client option `-onlineVersion:93` lowers the version; values above the global version are capped at the global value.

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
| banlist | Display active bans. |
| kick {PlayerID} [reason] | Kick a player. |
| kill {PlayerID} | Kill a player character. |
| ban {PlayerID} {1/2/3/4} [time] [reason] | Ban a player; 1 = ban by GUID, 2 = ban by IP. |
| unban {BanID} | Unban a player by BanID. |
| setprop {PlayerID} {prop} {0/1} [time] | Set/remove a property; `help setprop` |
| tp {PlayerID} {preset/x y z/json} [wcar] | Teleport to coordinates; `help tp` |
| tpto {PlayerID} {to PlayerID} [wcar] | Teleport to a player |
| clearchat | Clear chat |
| help [setprop/tp/alert] | Command help |
| reloadmirror | Reload the current ServersList.json while the mirror is running. |
| reloadcfg | Reload configs: EventsList, RespawnPoints, BanList, PluginsPolicy, Alerts. |
| alert {PlayerID/all} {message} {small/large/hint} [seconds] | Display an alert; `help alert` |
| msg {text} | Send a message as the server. See note. |
| cleardrop | Clear all drops on the server: medkits and weapons. |
| delcars all | Delete all cars on the server. |
| delcars {PlayerID} | Delete all cars of a specific player. |
| stats | Display resource consumption and utilization statistics. |
| showcfg | Display the effective server configuration. |
| stop | Stop the server. |

`reloadmirror` is available only while the mirror is active: it rereads ServersList.json without restarting the game server, retaining the previous list on error. `banlist` shows active bans, excluding expired bans and Type 0 records. `showcfg` displays ServerConfig values loaded at startup, including defaults and launch-argument overrides.

> [!WARNING]
> At the moment, the `msg` command requires at least one player on the server and sends the message effectively on their behalf, only replacing the nickname with "Server". This is caused by the current chat architecture in the game.

#### PlayerID

Each player on the server is assigned a temporary unique **PlayerID** valid within their current game session. The PlayerID counter advances on every new player connection, is never cleared, and never decreases. If a player reconnects to the server, they receive a new PlayerID, and the old one will never be used again. PlayerIDs are displayed in the player list shown by the `players` command.

#### Bans and player properties

`ban <PlayerID> <1|2|3|4> [time] [reason]` blocks connections by GUID (`1`) or IP (`2`). The optional duration is a positive integer followed by `s`, `m`, `h` or `d`, for example `30s`, `15m`, `12h` or `5d`. Omitting it makes the ban permanent. Example: `ban 15 1 12h`. The command does not accept type `0`.

GUID identifies a game profile and is stored in PlayerPrefs. Deleting the save creates another GUID, so a GUID ban does not prevent a new profile.

`setprop <PlayerID> <noweapon|nomove|nocars|nochat|invisible> <0|1> [time]` enables (`1`) or removes (`0`) a property. Durations follow the ban syntax and are ignored when removing a property. Assigning it again replaces its duration. Examples: `setprop 15 nochat 1 15m`, `setprop 15 nochat 0`.

| Property | Effect |
| --- | --- |
| `noweapon` | Blocks shooting and bomb placement/activation; switches the selected weapon to hands. |
| `nomove` | Freezes the character and ejects it from a vehicle. The server rejects movement and vehicle entry; BCL clients also suppress movement controls. Stock clients receive position corrections through the normal terrain/teleport handshake. |
| `nocars` | Blocks car spawning; existing cars remain. |
| `nochat` | Suppresses the player's chat messages. |
| `invisible` | Hides the overhead nickname/health, map marker, and player-list row on BCL clients. The server suppresses chat events about this player: connection, reconnection, disconnection, nickname change, and kill chat. A kill by an invisible player does not reveal their nickname. The character remains visible. Stock clients have no network flag for the UI hiding and retain their normal UI rules. |

When a player enters a vehicle, the server checks its own lock state. Entering a locked vehicle deletes the car and kicks the player with reason `Suspicion of cheat usage`. A forged request naming a distant car or invalid seat kicks the player without deleting the car.

Bans and properties are stored in `BigCityLegacy/config/BanList.json`, whose path can be set with `banList`. Properties match only by GUID, including on an IP-ban record. `until` is a Unix timestamp in UTC seconds; JSON numbers and numeric strings are accepted. Omitting `until` means permanent. Ban and property durations are independent. Expired restrictions are ignored immediately and periodically removed from disk. Use `reloadcfg` after manual edits.

```json
{
  "Version": 1,
  "Bans": [
    {
      "BanID": 1,
      "GUID": "1d9b4ba4-76f9-417f-b275-49e863726cb7",
      "IP": "1.2.3.4",
      "Type": 0,
      "Nickname": "BadPlayer",
      "Props": [
        { "Prop": "noweapon", "until": 1792388642 },
        { "Prop": "nochat" }
      ]
    }
  ]
}
```

`Type`: `0` = properties only, `1` = GUID ban, `2` = IP ban. A temporary ban has its own `until` field beside `Type` and `Nickname`.

`unban <BanID>` removes the ban. Entries with properties remain as `Type: 0`; otherwise they are deleted. It does not remove properties. `playerinfo <PlayerID>` includes an active-property line such as `Props: noweapon (5h), nochat (perm)`.

Kick/ban chat events describe the reason and ban duration instead of the normal disconnect message. Weapon, car, chat and movement restrictions are announced when chat events are enabled.

#### Teleportation and chat cleanup

`tp <PlayerID> <preset|x y z|json> [wcar]` accepts a preset name, decimal-dot coordinates or Point Tool JSON: `tp 15 police`, `tp 15 100.5 20 -30`, or `tp 15 {"x":100.5,"y":20,"z":-30,"yaw":90} wcar`. JSON requires `x`, `y`, `z`; `yaw` is optional. Presets include coordinates and heading: police, park, airport, sto1, sto2, sto3, sto4, desert, mountain, cemetry, derbyarena, mansion, farm. Names are case insensitive. Without yaw, rotation is preserved. `tpto <PlayerID> <to PlayerID> [wcar]` moves to another player’s current position. With `wcar`, a seated character travels with the vehicle and all its passengers; otherwise the character exits the vehicle. Teleportation preserves health and uses the stock terrain-loading handshake; frozen players remain frozen at the destination. Example: `tpto 15 16 wcar`.

`kill <PlayerID>` sets the character’s health to zero through the stock server death system. `kick` and `ban` immediately remove the game session and character. `disconnectedKickTimeout` applies to lost connections rather than administrative removal.

`clearchat` clears server history and replaces stock-client chat history with empty messages through the existing RPC. No new client protocol is required. Empty-row highlighting fades with the normal chat highlight timer.

`help` lists commands; `help setprop` and `help tp` provide detailed examples.

During active CS/CS_Sur matches, a freeroam character is killed after five seconds inside the arena. Vehicles driven into the arena are also removed after five seconds, even if the driver gets out. Vehicles that leave the zone or carry event participants are retained; passengers are ejected before removal.

## Logging and diagnostics

Each server start creates a separate `serverlog_<port>_<yyyy-MM-dd_HH-mm-ss-fff>.log` file. Filenames and record timestamps use UTC. It captures Unity messages from all threads, BepInEx logs without duplicating Unity messages, and BCL console output including entered CLI commands. Records include severity, source, thread ID and any stack trace supplied by Unity. Each record is flushed immediately; BCL retains no in-memory history. Atomic file creation prevents overwriting another instance's log when startup timestamps coincide. Existing logs are retained without automatic deletion.

`chatLogging = true` in `[logging]`, or `-chatLogging`, enables a separate `chatlog_<port>_<yyyy-MM-dd_HH-mm-ss-fff>.log` file. This setting is independent of `noLogging` and the server log level.

Logs are stored in `BigCityLegacy/logs` by default.

The `[logging]` options also accept launch arguments:

| TOML | Argument | Purpose |
| --- | --- | --- |
| `logsDir = ''` | `-logsDir:"server-logs/instance-1"` | Log directory. An empty value uses `BigCityLegacy/logs`. Relative paths resolve from the game directory; absolute paths are supported. The directory is created automatically. |
| `noLogging = false` | `-noLogging` | Disable the BCL file log, which is enabled by default. |
| `chatLogging = true` | `-chatLogging` | Enable the chat log; disabled by default. |
| `logLevel = "Log"` | `-logLevel:Warning` | Minimum severity: `Log`, `Warning`, `Error`, `Exception`, from least to most severe. `Assert` maps to `Error`. |

Detailed diagnostics is available in a Debug build with `-serverDiagnostics`; use `logLevel = "Log"` for a complete trace. Diagnostics writes `ENTER`/`EXIT`/`THROW` records with operation ID, parent ID and nesting depth around network buffer reception, UNet dispatch, player initialization, car spawning, XML loading and parent assignment. It records string/buffer sizes, reader positions, message type, channel, connection ID and network object ID when available. It does not consume or modify packets or save packet/XML contents. Managed stacks are recorded at nesting depths 32 and 64; memory, GC and player counts are sampled every 30 seconds. Tracing increases disk traffic and is intended for investigations. `noLogging = true` disables diagnostics; ordinary runs do not install its network patches.

The native `m_log_<port>.log` is disabled by default. The launch-only **`-mlog`** flag enables it independently of BCL logging.

## Plugin admission and BCL version

BCL clients report their own BigCityLegacy version and loaded BepInEx plugins (GUID, version, name) before the server creates the player. Dependency DLLs and plugins not loaded by BepInEx are not included. `playerinfo <PlayerID>` displays the reported plugins.

`BigCityLegacy/config/PluginsPolicy.json` controls admission. `allowClientsWithoutManifest` and `allowClientsWithoutBclVersion` independently allow missing plugin lists and missing BCL versions. Both default to `true`, `bcl` defaults to `null`, and both lists are empty, preserving access for vanilla and older clients. Set both flags to `false` to require the complete report.

`[plugins] noPluginsPolicyAndAlerts = true`, or `-noPluginsPolicyAndAlerts`, completely disables server plugin/BCL version checks and outgoing Alerts. Neither file is read; ordinary bans remain active. This setting applies at startup.

```json
{
  "version": 1,
  "allowClientsWithoutManifest": false,
  "allowClientsWithoutBclVersion": false,
  "bcl": {
    "minVersion": "1.4.2",
    "message": "BigCityLegacy {pluginVersion} is not supported. Required: {requiredVersion}.",
    "messageRu": "BigCityLegacy версии {pluginVersion} не поддерживается. Необходима версия: {requiredVersion}."
  },
  "hardlist": [
    {
      "guid": "com.bigcitylegacy.enhancedcartuning",
      "name": "EnhancedCarTuning",
      "minVersion": "1.0.0",
      "extra": "Install the required plugin before reconnecting.",
      "extraRu": "Перед повторным подключением установите необходимый плагин."

    }
  ],
  "blacklist": [
    {
      "guid": "com.example.prohibited",
      "message": "Remove {pluginName} before joining.",
      "messageRu": "Удалите {pluginName} перед подключением."
    }
  ]
}
```

`hardlist` requires a GUID and matching version. `blacklist` prohibits a GUID; with a version or range, only matching versions are prohibited. GUID comparison ignores case; names are display labels. `version` means exact equality; `minVersion` and `maxVersion` are inclusive bounds. Use either exact version or one or both bounds. Missing numeric components are zero (`1.4 == 1.4.0.0`); prereleases sort below the corresponding release; build metadata is ignored. `bcl` independently constrains the reported BCL version. A reported incompatible version is rejected even if missing versions are allowed.

Per-rule `message` replaces the global plugin-incompatibility template; `extra` appends a paragraph. This also applies to the `bcl` rule. All mismatches are combined. If the absent plugin has no reported name, the rule's `name` or GUID is used.

Allowing an absent manifest means its hardlist/blacklist cannot be checked; allowing an absent BCL version skips that version check. A malformed submitted report is rejected, not treated as absent. This is compatibility metadata reported by the client, not proof that a modified client has no cheats.

Report limits: 256 plugins, GUID/name 128 characters, version 64 characters, binary manifest 20000 bytes, complete login XML 30000 bytes. A client that cannot submit the full list cancels login with a local explanation; it never submits a partial list. The channel uses additional native-login XML attributes, which older servers ignore. Online protocol version remains 94.

## Player alerts

Templates are stored in `BigCityLegacy/config/Alerts.toml`. Sections: `plugins`, `missingManifest`, `missingBclVersion`, `invalidManifest`, `banRejected`, `banned`, `kicked`, `welcome`. Each supports `enabled`, `title`, `message`, `display`, `duration`. Omitted sections inherit built-in defaults. Welcome is disabled by default; the others are enabled. Default display is `small`, except welcome (`large`).

`small` is a draggable window with a HintBox; `large` uses a ScrollView; `hint` is a HintBoxAlt at the top of the screen for `duration` seconds (0.1–3600, default 6). Long small messages automatically gain scrolling. Hints do not block input; an open dialog shows the cursor and blocks clicks into the game. Rejection/kick messages survive disconnection. Queues are bounded; hint duration starts when shown, not when received.

Optional `titleRu` and `messageRu` fields in each Alerts.toml section provide Russian text. The client uses its current game language; a missing or empty Russian field falls back to the primary field. PluginsPolicy.json supports per-rule `messageRu` and `extraRu`, including in `bcl`.

```toml
[welcome]
enabled = true
title = "Server rules"
titleRu = "Правила сервера"
display = "large"
message = """
Welcome, {nickname}!
1. Respect other players.
2. Follow the server rules.
"""
messageRu = """
Добро пожаловать, {nickname}!
1. Уважай других игроков.
2. Соблюдай правила сервера.
"""
updatedAt = 1793150000

[kicked]
title = "Disconnected by administrator"
titleRu = "Отключен администратором"
message = "{nickname}, you were kicked.\nReason: {reason}"
messageRu = "{nickname}, вы были отключены от сервера.\nПричина: {reason}"
display = "small"

[plugins]
title = "Plugin compatibility"
titleRu = "Совместимость плагинов"
message = "{reason}\n{pluginName}: {pluginVersion}\nRequired: {requiredVersion}"
messageRu = "{reason}\n{pluginName}: {pluginVersion}\nНеобходима: {requiredVersion}"
display = "small"

[banned]
title = "Banned!"
titleRu = "Вы были забанены!"
message = "{nickname}\nBan ID: {banId}\nDuration: {duration}\nReason: {reason}"
messageRu = "{nickname}\nID бана: {banId}\nДлительность: {duration}\nПричина: {reason}"
display = "hint"
duration = 15
```

Use `\n` for a newline and `\t` for a tab in double-quoted strings; triple double quotes support long messages. Single-quoted TOML strings are literal. JSON `message`/`extra` strings use normal JSON escaping. Variables are substituted once, without another pass over escape sequences or rich-text markup.

| Variables | Availability |
| --- | --- |
| `{nickname}`, `{serverPort}`, `{bclVersion}`, `{reason}` | General templates and manual messages; unknown version is `not reported`, missing reason is `Not specified`. |
| `{playerId}` | After the server session is created; empty before admission. |
| `{banId}`, `{until}`, `{duration}` | Ban ID, expiry Unix timestamp or `permanent`, remaining duration or `perm`. The reason comes from `BanList.json` (`Reason`). |
| `{pluginGuid}`, `{pluginName}`, `{pluginVersion}`, `{requiredVersion}`, `{rule}` | Plugin/BCL incompatibility; rule is `hardlist`, `blacklist` or `bcl`. An absent plugin's version is `not installed`. |

Commands:

```text
alert <PlayerID|all> <message> <small|large|hint> [seconds] [title="Text"] [titleRu="Текст"] [messageRu="Текст"]
alert all "Welcome, {nickname}!\nRead the rules." large
alert all "Maintenance soon" hint 10 title="Notice" titleRu="Объявление" messageRu="Скоро техработы"
help alert
```

Ban durations use `s/m/h/d`; omitting the duration makes the ban permanent. Reasons are saved with the ban and available on later rejected connections. Sending to `all` formats the message separately for each recipient. Older/vanilla clients are skipped when sending alerts and receive ordinary disconnection on rejection, kick or ban: the UI requires BCL 1.4.2+. The server removes the game session immediately while allowing the transport up to two seconds to acknowledge the alert. Missing acknowledgement does not preserve the session.

At startup and during `reloadcfg`, the server logs success or failure separately for PluginsPolicy and Alerts.

`reloadcfg` reloads PluginsPolicy and Alerts atomically; errors retain the previous working pair. Rules apply to subsequent connections and do not automatically disconnect existing players. Invalid startup configs stop server startup. Paths can be set through `[files] pluginsPolicy = '...'`, `alerts = '...'` in ServerConfig.toml or `-pluginsPolicy:"path"` and `-alerts:"path"`. Relative paths resolve from the game directory. An explicitly selected missing file is an error.

Welcome appears after the game world loads, the character spawns, and fade finishes completely.

Welcome includes a “Don't show again” toggle. The choice and `updatedAt` are saved in PlayerPrefs separately for the server's actual IP:port. When changing the rules, set a new Unix timestamp in `updatedAt` (integer UTC seconds, default `0`) and run `reloadcfg`. A different value resets suppression and allows the updated message to appear.

The client C# API is documented separately: [BigCityLegacy.Api.LegacyAlerts](../dev-docs/LegacyAlerts_EN.md).

`banRejected` applies when an already-banned player attempts to join; `banned` applies when banning a connected player. These templates are configured independently.

## Machine bans

`ban <PlayerID> <3|4> [time] [reason]` uses a fingerprint that BCL clients collect once at startup and send to the selected server when connecting. `playerinfo <PlayerID>` prints its HEX string. These extend the existing command: type `1` is GUID, `2` is GUID or IP, `3` is GUID or fingerprint, and `4` links GUIDs, fingerprints and IPs. Duration, reason and `unban <BanID>` work for every type.

Windows uses MachineGuid, baseboard serial/SMBIOS UUID, system disk serial, and permanent physical NIC MAC addresses. ProcessorId is not used as a unique CPU identifier. Linux uses DMI fields readable by the current user, a separate BCL-derived machine-id identifier, system disk serial/root filesystem UUID, and permanent physical NIC MAC addresses. No sudo/UAC requests are made; unavailable values and known OEM placeholders are omitted.

Board serial and SMBIOS UUID form one group; disk serial and root filesystem UUID form another. The remaining groups are OS installation and physical NICs. Three or four groups permit one mismatch; two groups must both match. Missing values do not match each other. Reducing fields in a new report does not lower the older report's threshold. Fewer than two groups cannot support a new type 3/4 ban.

GUID is a base blocking identifier for every active ban type. For types `1–3`, it provides an additional check without learning other identifiers: matching a GUID on an IP ban does not add the new IP, and matching a different GUID through IP/fingerprint does not save that GUID. Type `3` additionally checks the machine, but not IP.

Type `4` rejects a match on **any** known GUID, fingerprint or IP. Such a match learns new nonempty GUIDs/IPs and usable fingerprints in the same BanID's `GUIDs`, `IPs`, `Fingerprints` arrays. Primary `GUID` and `IP` fields remain compatible; existing entries use their primary GUID without migration. All aliases share the BanID's duration and reason. Limits are 256 GUIDs, 256 IPs and 128 fingerprints; reaching a limit does not cancel enforcement. Unbanning disables all aliases while preserving player properties, which still match only the primary GUID.

For type 4, shared public IPs behind NAT, in a household or on a VPN link machines into one ban record by design. Older clients without a fingerprint are still checked by GUID; type 4 also checks their IP. Require a report with `[server] requireFingerprint = true` or `-requireFingerprint` (default false). Missing, unusable or corrupt reports are then rejected. This requirement and machine bans operate independently of `noPluginsPolicyAndAlerts`.

```text
ban 15 3 12h Suspicion of cheat usage
ban 15 4
playerinfo 15
unban 1
```

A fingerprint contains reversibly packed fields, not a single hash or authenticity proof. A modified client can forge it; reinstalling Linux or changing hardware may alter several groups. Treat stored machine identifiers as administrative data. Manual decoder: `python scripts/decodeFingerprint.py --file fingerprint.txt`.
