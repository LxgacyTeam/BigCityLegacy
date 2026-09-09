# Server

The game server can generally work in two modes: **Master** and **Base**.

**Base** means launching only the Game Server, using the basic `-bend_GameServer` argument.

**Master** means Mirror + Game Server. In this mode, a Mirror server is launched alongside the game server. In an architecture with multiple game servers, the Master server is intended to act as the main server that connects all of them together.

---

# Game Server

**Game Server** is the actual game server, representing one online lobby.

The Game Server has 5 modes: `None`, `RP`, `Race`, `CS`, `CS_Sur`.

* **None** - the base mode, shown in the game as FreeRoam. All other modes are based on it.

- **RP** - the None mode, but with weapons disabled.
- **Race** - the None mode, but with periodically launched online race events.
- **CS** - the None mode, but with periodically launched "Cops vs. Bandits" events, effectively team deathmatch.
- **CS_Sur** - the None mode, but with periodically launched "Survival" events, effectively free-for-all deathmatch.

The Game Server is configured using launch arguments; see Launch Arguments.

There is no hard technical limit on the number of players on the server, but the game was designed for **no more than 100 players**.

---

# Mirror Server

**Mirror server** is the server that handles the list of game servers and their status. It provides the game client with a list of available servers, their settings, and their current online player count. In other words, it determines what the game displays in the server list under the **Online** section. Mirror can receive and send the server name, maximum player count, language, game mode, menu group placement, and several other parameters. It also provides the automatic server selection feature, used when pressing the green Connect button in the game.

The Mirror server is configured through a separate JSON config. By default, it is stored at `MadOutLegacy/ServersList.json`. An example config looks like this:

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

**Parameter descriptions:**

`masterPort` - the port used by the Master server.

`pollIntervalMs` - the interval used to poll game servers and check whether they are online.

`staleAfterMs` - determines how long it takes for an entry to be considered stale if the server is no longer available.

`servers` - an array of game servers displayed in the list.

`id` - the unique ID of each server.

`name` - the server name displayed in the menu.

`connectIp` - the public server IP passed to the game client.

`queryIp` - the server IP used by Mirror for polling. If the server is on the same local network or on the same machine, it is convenient to separate the public and internal IP addresses.

`port` - the server port passed to the game client.

`maxPlayers` - the maximum number of players on the server, displayed in the menu.

`lang` - the server language displayed in the menu.

`menuGroup` - the category where the server is displayed in the game menu. Accepted values: `FreeRoam`, `RP`, `Race`, or `CS`.

`serverMode` - the game mode running on the server.

`onlineProtocolVersion` - the online protocol version. For game version 9.4, this value is 93.

`includeInAuto` - whether the server should be included in automatic connection selection.

`showIfOffline` - whether the server should be displayed in the game when it is offline.

`self` - marks the Master's own game server. SPECIFY THIS ONLY ONCE.

A Master server is required if you want to launch multiple game servers and display them as a list in the game. The intended and recommended architecture is `1 Master Server + n Base servers`.
