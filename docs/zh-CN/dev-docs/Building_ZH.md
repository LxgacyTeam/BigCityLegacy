# 构建 BCL

**要求：**

- 已安装 BepInEx 5 的 MadOut2 9.4。
- .NET SDK。

**构建步骤：**

1. 将 BepInEx 5 安装到游戏文件夹中，然后启动游戏一次。
2. 克隆并构建项目，同时传入游戏根目录的**绝对路径**：

```bat
git clone --recurse-submodules https://github.com/LxgacyTeam/BigCityLegacy.git
cd BigCityLegacy
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

> [!NOTE]
> 在 Visual Studio 中打开项目时，请务必在每个 `.csproj` 文件的 `<GameDir>` 属性中设置游戏根目录的路径。之后，所有必需的 Unity 和 BepInEx 程序集都会自动添加为引用。

> [!WARNING]
> 通过命令行选项传入 `GameDir` 属性时，**必须使用绝对路径**。使用 `..\` 或其他相对路径可能导致构建错误，因为该属性也会影响位于嵌套目录中的依赖项目 LegacyUIFramework。

[LegacyHelpers](LegacyHelpers_ZH.md) · [LegacyAlerts](LegacyAlerts_ZH.md)
