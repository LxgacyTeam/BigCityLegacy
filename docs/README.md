### [Русский](.README_RU.md) | [> English <](/README.md)
---

<div align="center">
  <picture>
    <img alt="bcl" src="./img/bcl-logo.png" style="max-width:256px;width:100%">
  </picture>
  <h1>BigCityLegacy</h1>
  <h3>A BepInEx plugin for the Steam version of <b>MadOut2 BigCityOnline</b> that fully restores online and offline functionality.</h3>
  <h3>
    <a href="https://github.com/LxgacyTeam/BCL-installer/releases/download/1.0/BigCityLegacy_Installer.exe">Download installer</a>
     | <a href="https://github.com/LxgacyTeam/BigCityLegacy/wiki#-documentation-on-english">Visit Wiki</a>
     | <a href="https://t.me/BigCityLegacy">Our Telegram</a>
  </h3>
  <p>Detailed information about the mod features, server startup, and server configuration is available in the Wiki section.</p>
</div>

---
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
* Download [**official online installer**](https://github.com/LxgacyTeam/BCL-installer/releases/download/1.0/BigCityLegacy_Installer.exe) and install mod on a clean game.

*or install manually:*

* Install [BepInEx 5](https://github.com/BepInEx/BepInEx) into the game folder and launch the game once.
* Download the zip file from the [Releases](https://github.com/LxgacyTeam/BigCityLegacy/releases) section and unpack it into the game root folder.

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

> [!NOTE]
> When opening the project in Visual Studio, make sure to set the path to the game root folder in the `<GameDir>` property in every `.csproj` file. After that, all required Unity and BepInEx assemblies will be referenced automatically.

> [!WARNING]
> When passing the `GameDir` property through a CLI flag, you **must use an absolute path**. Using `..\` or other relative paths can cause build errors, because this property also affects the dependent LegacyUIFramework project located in one of the nested directories.

## Terms and conditions

### For detailed terms of use for the project and applicable legal provisions, please refer to [NOTICE](https://github.com/LxgacyTeam/BigCityLegacy/blob/main/docs/NOTICE.md).
