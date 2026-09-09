# Online Events

**Online events** are events that periodically start on servers running the `Race`, `CS`, or `CS_Sur` modes. Depending on the mode, they are either race tracks or shootout locations for the "Cops vs. Bandits" mode. Their database is loaded by the game client from the server, and the game itself does not store any pre-made tracks or locations locally. This is why the original tracks and locations were permanently lost.

Because of that, the mod implements a mechanism for loading a custom event database from a JSON file. This makes it possible to create your own events.

By default, events are loaded by the server from `MadOutLegacy/Events.json`; see the file template.

At the moment, the mod includes only a few ready-made events as templates for creating new ones. A larger event database is planned and will later be shipped with the mod so that regular players get a more complete experience out of the box. If you would like to take part in creating events and share your work, send it to the appropriate topic in the Discussions section.

# Online Races: Creating Tracks

Creating an online race means creating its track, or in other words, placing checkpoints around the map. You can get point coordinates on the map using the Debug Overlay tool; see here.

**A race template in the database looks like this:**

```
    {
      "mode": "Race",
      "name": "airport_01",
      "displayName": "Airport Сircuit Race 01",
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

**A race defines the following fields:**

`mode` - the event mode. For a race, this is `Race`.

`name` - the unique race name in the database.

`displayName` - the race name displayed in the game.

`isSprint` - sets the race type to "Sprint".

`laps` - the number of laps in the race. If this value is set to `0`, the number of laps will be chosen randomly, either 1 or 2.

`useRespawn` - whether respawning at the last checkpoint is allowed.

`start` - an array of starting points. The number of points defines the maximum number of participants in the race. The order of points in the array is also tied to the player order. For example, the 3rd participant will spawn at the 3rd starting point.

`points` - an ordered array of checkpoints. When multiple laps are specified, the game duplicates the checkpoints automatically.

# Cops vs. Bandits: Creating Locations

Creating locations, or event areas, for the "Cops vs. Bandits" event is very simple. You only need to specify player spawn points. The number of points equals the number of players. The game determines the area radius automatically based on the distance between the points.

The game has two variants of the "Cops vs. Bandits" mode: CS and CS_Sur, also known as Survival.

`CS` - team deathmatch with two teams.

`CS_Sur` - free-for-all deathmatch, where every player fights for themselves.

**A location template in the database for CS looks like this:**

```
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

Here you need to define points for each team using the `counter` and `terror` arrays. The number of points in each team must be equal.

---

**A location template in the database for CS_Sur looks like this:**

```
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

Here you only need to define player spawn points in the `start` array.

The remaining parameters are already familiar from races:

`mode` - the event mode: `CS` or `CS_Sur`.

`name` - the unique location name in the database.

`displayName` - the location name displayed in the game.

# Debug Overlay Tool

The Debug Overlay tool is used to determine point coordinates on the map. It displays the current camera and player coordinates as an in-game overlay. It can also save an array in the required coordinate format directly to a file, making it easier to copy the coordinates into the config later.

The tool is launched using the `-debugoverlay` launch argument.

**There are 2 hotkeys:**

`Z` - write the current camera coordinates to `MadOutLegacy/pos_out.txt`. Coordinates are written as a list so they can be easily copied into JSON. The file is cleared every time the game is launched, so make sure to copy the coordinates while the game is still running.

`X` - copy the current camera coordinates to the clipboard.

For convenient coordinate recording, it is recommended to fly around the map with the free camera, enabled with the **F** key, and record the coordinates from there.
