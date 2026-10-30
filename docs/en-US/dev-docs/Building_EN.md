# Building BCL

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

[LegacyHelpers](LegacyHelpers_EN.md) · [LegacyAlerts](LegacyAlerts_EN.md)
