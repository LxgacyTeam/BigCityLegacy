# MadOutLegacy

#### 🇷🇺 README на Русском

---

A BepInEx plugin for the Steam version of **MadOut2 BigCityOnline** that fully restores the game's online and offline functionality.

#### Detailed information about the mod's features, server launch options, and server configuration is available in the Wiki.

## Features

- Offline gameplay mode with the server dependency fully removed
- The mandatory Steam requirement has been removed
- Host your own game server with flexible configuration
- Support for all online modes: FreeRoam, RP, Race, and Cops vs. Bandits
- Ability to create custom tracks and locations for online races and Cops vs. Bandits events
- Coins and diamonds can be obtained for free from the shop using the "+" button in the main menu
- Additional hotkeys; see the Wiki
- FPS limit removal

## Compatibility

Only the latest Steam version of the game, `9.4`, is fully supported.

For earlier versions (`4.9` - `9.2`), Compatibility Mode is available. It provides offline-only functionality. At the moment, Compatibility Mode does not guarantee fully stable behavior on older game versions.

## Installation

* Download the latest **original** version of the game from Steam, or from here.
* Install BepInEx 5 into the game folder and launch the game once.
* Download the zip file from the Releases section and extract it into the game root folder.

## Building

**Requirements:**

- MadOut2 9.4 with BepInEx 5 installed
- .NET SDK / MSBuild

**Build process:**

1. Install BepInEx 5 into the game folder and launch the game once.
2. Build the project and pass the path to the game root folder:

```bat
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

## Terms and Conditions

1. This project is distributed under the MIT License and requires compliance with its terms.
2. This project is not affiliated with MadOut Games and is not supported by the game's developers. The PC version of MadOut2 is officially closed and unsupported.
3. This project is an independent, non-commercial fan modification. It does not contain or distribute unofficial or modified builds and files of the MadOut2 game.
