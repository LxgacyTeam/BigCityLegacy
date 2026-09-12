# Online events

**Online events** are periodically launched events on servers running `Race`, `CS`, or `CS_Sur` modes. Depending on the mode, they are either race tracks or locations for Cops vs Bandits shootouts. Their database is loaded by the game client from the server, and the game itself does not store prebuilt tracks and locations. Because of this, the original tracks and locations were lost permanently.

For that reason, the mod implements a mechanism for loading a custom event database from a JSON file. This makes it possible to create events manually.

By default, events are loaded by the server from `BigCityLegacy/Events.json`. See the file template.

At the moment, the mod only ships with a few ready-made events as templates for creating new ones. A larger database is planned soon and will be distributed together with the mod so the experience for regular players is as pleasant as possible. If you want to help create events and submit your work, send it to the corresponding topic in the Discussions section.

# Online races: creating tracks

Creating an online race means creating its track by placing checkpoints on the map. Point coordinates on the map can be obtained with Point Tool v2. See the relevant section.

**A race template in the database looks like this:**

```json
    {
      "mode": "Race",
      "name": "airport_01",
      "displayName": "Airport Circuit Race 01",
      "isSprint": false,
      "laps": 3,
      "useRespawn": true,
      "start": [
        { "x": -1642.374, "y": -23.585, "z": 183.954, "yaw": 183.525 },
        { "x": -1638.155, "y": -23.044, "z": 184.996, "yaw": 177.613 },
        { "x": -1647.588, "y": -23.197, "z": 185.731, "yaw": 176.502 },
        { "x": -1647.714, "y": -23.136, "z": 192.009, "yaw": 176.41 },
        { "x": -1642.523, "y": -23.136, "z": 192.331, "yaw": 178.945 },
        { "x": -1637.525, "y": -23.136, "z": 192.426, "yaw": 178.593 }
      ],
      "points": [
        { "x": -1639.268, "y": -23.605, "z": 103.205, "yaw": 175.679 },
        { "x": -1534.495, "y": -23.357, "z": 97.345, "yaw": 90.49 },
        { "x": -1422.056, "y": -23.575, "z": 95.94, "yaw": 90.569 },
        { "x": -1313.92, "y": -23.386, "z": 95.309, "yaw": 90.306 },
        { "x": -1211.561, "y": -23.275, "z": 176.372, "yaw": 41.478 },
        { "x": -1348.206, "y": -23.156, "z": 183.326, "yaw": 270.348 },
        { "x": -1464.071, "y": -23.439, "z": 183.758, "yaw": 270.077 },
        { "x": -1605.617, "y": -23.363, "z": 183.96, "yaw": 270.518 }
      ]
    }
```

**Race fields:**

`mode` - event mode. For a race, this is `Race`.

`name` - unique race name in the database.

`displayName` - displayed race name.

`isSprint` - sets the race type to Sprint.

`laps` - number of laps in the race. If set to `0`, the number of laps will be chosen randomly: 1 or 2.

`useRespawn` - whether players can respawn at the last checkpoint.

`start` - array of starting points. The number of points defines the maximum number of race participants. The order of points in the array is also tied to the order of participants. For example, the third participant will spawn at the third starting point.

`points` - array of checkpoints in order. If several laps are specified, the game duplicates checkpoints automatically.

# Cops vs Bandits: creating locations

Creating locations, or event zones, for Cops vs Bandits is very simple. You only need to specify player spawn points. The number of points equals the number of players. The game determines the zone radius automatically based on the distance between points.

The game has two variants of Cops vs Bandits: CS and CS_Sur, also known as Survival.

`CS` - team deathmatch with two teams.

`CS_Sur` - free-for-all deathmatch where every player fights for themselves.

**A CS location template in the database looks like this:**

```json
    {
      "mode": "CS",
      "name": "CopsVsBandits_01",
      "displayName": "CopsVsBandits 01",
      "counter": [
        { "x": -1634.599, "y": 1.314, "z": 2586.105, "yaw": 335.101 }
      ],
      "terror": [
        { "x": -1603.904, "y": 1.082, "z": 2623.952, "yaw": 345.323 }
      ]
    }
```

Here you must specify points for each team in the `counter` and `terror` arrays. The number of points in each team must be equal.

---

**A CS_Sur location template in the database looks like this:**

```json
    {
      "mode": "CS_Sur",
      "name": "Survival_01",
      "displayName": "Survival 01",
      "start": [
        { "x": -1634.599, "y": 1.314, "z": 2586.105, "yaw": 335.101 },
        { "x": -1603.904, "y": 1.082, "z": 2623.952, "yaw": 345.323 }
      ]
    }
```

Here you simply specify player spawn points in the `start` array.

The remaining fields are already familiar from races:

`mode` - event mode: `CS` or `CS_Sur`.

`name` - unique location name in the database.

`displayName` - displayed location name.

# Point Tool v2

Point Tool v2 is used to determine point coordinates on the map. It can save an array of coordinates directly to a file so they can later be copied into the config conveniently.

**Main features:**

* Get a point coordinate on the map from the camera or player position.
* Save an array of coordinates to a file.
* Display visual/interactive markers on the map.
* Teleport the player to coordinates or to the camera position.
* Delete saved points one by one or clear all points at once.

The tool is launched from the game settings.

**There are 3 hotkeys:**

`Z` - writes the current camera coordinates to `BigCityLegacy/pos_out.txt`. Coordinates are written as a list so they can be easily copied into JSON. The file is cleared on every new game launch, so remember to copy the coordinates while the game is still running.

`X` - removes the last coordinate from the file.

`F9` - copies the current coordinate to the clipboard.

For convenient coordinate saving, it is recommended to fly around the map with the free camera, enabled with **F**, and record coordinates.
