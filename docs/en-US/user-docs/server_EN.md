# Server

The game server can generally work in 2 modes: **Master** and **Base**.

**Base** means launching only the game server (Game Server), provided by the basic `-bend_GameServer` flag.
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

Game Server is configured with launch arguments. See Launch arguments. It also supports administration through an interactive CLI interface. See below.

There is no hard practical limit on the number of players on the server, but the game is designed for **no more than 100 players**.

---

# Mirror server

**Mirror server** is the server that handles the list of game servers and their state. It sends the game client the list of available servers, their settings, and current online count. In other words, it determines what the game shows in the server list in the **Online** section. Mirror can receive and send the server name, maximum player count, language, game mode, group placement, and some other parameters. It also provides the automatic server selection feature used when pressing the green Connect button in the game.

Mirror server configuration is stored in a separate JSON config. By default, it is stored in `BigCityLegacy/ServersList.json`. Example config:

```json
  {
  "version": 1,
  "masterPort": 35000,
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

`masterPort` - port used by the master server.

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
