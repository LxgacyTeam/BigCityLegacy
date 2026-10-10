# BigCityLegacy

### [俄文 README](https://github.com/LxgacyTeam/BigCityLegacy/blob/main/docs/README_RU.md)

---

适用于 Steam 版 **MadOut2 BigCityOnline** 的 BepInEx 插件，可全面恢复在线和离线功能。

### 有关模组功能、服务器启动及配置的详细信息，请参阅[文档概览](zh-CN/user-docs/overview_ZH.md)。

## 主要功能

- 搭建自己的游戏服务器，并灵活进行配置和管理。
- 支持所有在线模式：自由漫游（FreeRoam）、角色扮演（RP）、竞速（Race）和警匪对抗（Cops vs Bandits）。
- 为在线竞速和警匪对抗创建自定义赛道和场地。
- 提供额外设置和快捷键。详见文档。

## 兼容性

仅完整支持最新的 Steam 游戏版本 `9.4`。

较早版本（`4.9 - 9.2`）可在兼容模式下运行，但仅提供离线功能。目前，兼容模式尚不能保证旧版游戏完全稳定运行。

## 安装

* 从 Steam 下载最新的**原版**游戏。**不支持**经过修改的游戏版本。
* 将 [BepInEx 5](https://github.com/BepInEx/BepInEx) 安装到游戏文件夹中，然后启动游戏一次。
* 从 [Releases](https://github.com/LxgacyTeam/BigCityLegacy/releases) 页面下载 ZIP 文件，并解压到游戏根目录。

## 构建

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

## 条款与条件

### 有关项目使用条款和适用法律规定的详细信息，请参阅 [NOTICE](NOTICE_ZH.md)。
