# BigCityLegacy

A BepInEx plugin for the Steam version of **MadOut2 BigCityOnline** that fully restores online and offline functionality.

#### Detailed information about the mod features, server startup, and server configuration is available in the Wiki section.

## Key features

- Run your own game server with flexible configuration and administration.
- Support for all online modes: FreeRoam, RP, Race, and Cops vs Bandits.
- Ability to create custom tracks and locations for online races and Cops vs Bandits.
- Additional settings and hotkeys. See the Wiki.

## Compatibility

Only the latest Steam game version, `9.4`, is fully supported.

Earlier versions (`4.9 - 9.2`) can run in Compatibility Mode, which provides offline functionality only. At the moment, Compatibility Mode does not guarantee fully stable operation of older game versions.

## Installation

* Download the latest **original** version of the game from Steam. Modified game builds are **not supported**.
* Install BepInEx 5 into the game folder and launch the game once.
* Download the zip file from the Releases section and unpack it into the game root folder.

## Building

**Requirements:**

- MadOut2 9.4 with BepInEx 5 installed.
- .NET SDK.

**Build process:**

1. Install BepInEx 5 into the game folder and launch the game once.
2. Clone and build the project, passing the **absolute path** to the game root folder:

```bat
git clone --recurse-submodules https://github.com/LxgacyTeam/BigCityLegacy.git
cd BigCityLegacy
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

[!NOTE]
When opening the project in Visual Studio, make sure to set the path to the game root folder in the `<GameDir>` property in every `.csproj` file. After that, all required Unity and BepInEx assemblies will be referenced automatically.

[!WARNING]
When passing the `GameDir` property through a CLI flag, you **must use an absolute path**. Using `..\` or other relative paths can cause build errors, because this property also affects the dependent LegacyUIFramework project located in one of the nested directories.

## Terms and conditions

1. The project is distributed under the MIT License and requires compliance with its terms.
2. The project is not affiliated with MadOut Games and is not supported by the game developers. The PC version of MadOut2 is officially discontinued and unsupported.
3. This project is an independent, non-commercial fan modification. It does not contain or distribute unofficial or modified builds or files of the MadOut2 game.
