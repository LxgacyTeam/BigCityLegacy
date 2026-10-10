# 概览

**BigCityLegacy** 项目旨在全面恢复 Steam 版 MadOut2 BigCityOnline 的在线功能，以保留原有的游戏体验和游戏历史。

本项目是以 BepInEx 5 插件形式分发的游戏模组。

# 功能与能力详解

* **模组移除了游戏对在线服务器的强制依赖**，并提供完整的离线模式功能。
* **模组允许以服务器模式启动游戏**，并**通过独立菜单轻松连接服务器**。服务器具有多种模式、灵活的配置，以及通过交互式 CLI 进行管理的功能。详见[服务器](server_ZH.md)章节。
* **服务器支持所有主要游戏模式**：自由漫游（FreeRoam）、角色扮演（RP）、竞速（Race）和警匪对抗（Cops vs Bandits）。
  *在线跑酷（Online Parkour）计划在未来版本中恢复，目前尚不支持。*
* **服务器支持完整的无图形界面模式**，运行时无需图形界面，可在 Windows 或 Linux 控制台中运行。
* **服务器具有 CLI 和管理系统**：封禁或踢出玩家、清理车辆和掉落物，以及重新加载配置。详见[服务器](server_ZH.md)章节。
* 可运行 Mirror 服务器，为客户端提供服务器列表及服务器状态。详见[服务器](server_ZH.md)章节。
* **可以为在线竞速创建自定义赛道，为警匪对抗创建自定义区域。**
  原版所谓的活动，即赛道和区域，已完全丢失，因为它们从服务器加载，并不存储在游戏客户端中。因此，这些模式必须加载自定义活动才能运行。当前版本仅附带少量活动，作为创建自有活动的模板。详见[活动创建](events_ZH.md)章节。
* 模组使用 LegacyUIFramework，这是用于创建可拖动窗口和带样式 UI 元素的自定义框架。
* 模组的所有 UI 元素均支持本地化，并会响应游戏设置中的语言切换。
* 模组提供新版本更新通知系统。
* 模组包含 Point Tool v2，方便创建自定义活动所需的检查点或位置坐标序列。
* 模组添加了以下快捷键：
  - `F5` - 快速切换天气。
  - `F6` - 立即生成上次选择的车辆，无需等待冷却时间。
  - `F12` - 紧急断开服务器连接，适用于游戏卡在黑屏的情况。
* 模组可将帧率上限设为 10000，从而解除 FPS 限制。
* 模组添加了启用玩家角色阴影的选项，默认开启。

# 启动参数

模组提供多种传递给游戏可执行文件的启动参数。它们主要用于启动服务器，也包含一些客户端参数。

**参数传递方式如下：**

```
game.exe -arg1 -arg2 -arg3
```

带值的参数必须严格采用冒号语法：

```text
-key:value
-key:"value with spaces"
```

#### 客户端参数

| 参数 | 用途 | 可接受的值 |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------- |
| -connectIP:[value] | 设置直连游戏服务器的地址。 | 游戏服务器 IP/域名。 |
| -connectPort:[value] | 设置直连游戏服务器的端口。 | 游戏服务器端口。 |
| -useMaster | 自动使用指定或已保存的主服务器。 | - |
| -masterIP:[value] | 设置主服务器地址。 | Mirror 服务器 IP/域名。 |
| -masterPort:[value] | 设置主服务器端口。 | Mirror 服务器端口。 |
| -forceFullMode | 在低于 9.4 的游戏版本上强制以完整模式运行模组。 | - |
| -forceCompatMode | 强制以兼容模式运行模组。 | - |
| -noUpdCheck | 禁用游戏或服务器启动时的自动更新检查。 | - |

#### 服务器参数

| 参数 | 用途 | 可接受的值 |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| -bend_GameServer | 以服务器模式启动游戏。 | - |
| -batchmode -nographics | 使用无图形界面模式。推荐使用。 | - |
| -port:[value] | 启动的游戏服务器所使用的端口。 | 0-65535 |
| -maxConn:[value] | 启动的服务器所允许的最大玩家数。 | 1-100 |
| -Mode:[value] | 服务器上的游戏模式。 | None / RP / Race / CS / CS_Sur |
| -maxCars:[value] | 服务器上允许存在的最大车辆数。<br />默认值：100 | 车辆数；0 = 无限制。 |
| -maxCarsByPlayer:[value] | 每位玩家允许拥有的最大车辆数。<br />默认值：15 | 车辆数；0 = 无限制。 |
| -maxNikLength:[value] | 服务器上允许的最大玩家昵称长度。<br />默认值：20 | 昵称长度；0 = 无限制。 |
| -noChatEvents | 禁用服务器事件的聊天通知，目前包括玩家连接和断开连接的消息。 | - |
| -idleKickTimeout:[value] | 玩家无操作后被踢出的等待时间（闲置踢出）。<br /><br />默认值：120 秒 | `{n}` 或 `{n}s` - 秒数；`{n}ms` - 毫秒数。示例：120、60s、90000ms；0 = 禁用闲置踢出。 |
| -useVanillaRespawn | 启用原版的死亡后重生系统，禁用 RespawnPoints.json。 | - |
| -masterServer | 启动 Mirror 服务器。 | - |
| -serversList:[path] | 手动指定 Mirror 服务器配置的路径。默认从 BigCityLegacy/ServersList.json 读取。仅在需要指定其他文件时使用。 | Mirror 服务器配置文件的路径。 |
| -eventsList:[path] | 手动指定活动列表文件的路径。默认从 BigCityLegacy/Events.json 读取。仅在需要指定其他文件时使用。 | 活动列表文件的路径。 |
| -respawnPoints:[path] | 手动指定重生点列表文件的路径。默认从 BigCityLegacy/RespawnPoints.json 读取。仅在需要指定其他文件时使用。 | 重生点文件的路径。 |
| -banList:[path] | 手动指定封禁玩家列表的路径。默认从 BigCityLegacy/BanList.json 读取。仅在需要指定其他文件时使用。 | 封禁玩家列表的路径。 |
| -masterDebug | 输出 Mirror 服务器运行的详细日志。 | - |
| -verboseServerConsole | 将完整的游戏日志输出到服务器控制台。此模式输出较多。 | - |
| -noServerConsole | 不打开服务器控制台窗口，完全在后台启动。 | - |
| -noServerCLI | 禁用服务器 CLI。 | - |

#### 使用示例

启动游戏，并预先指定游戏服务器：

```
game.exe -connectIP:1.2.3.4 -connectPort:7800
```

启动游戏，并预先指定 Mirror 服务器：

```
game.exe -useMaster -masterIP:1.2.3.4 -masterPort:35000
```

以 Base 模式启动**服务器**，仅运行游戏服务器：

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race
```

以 Master 模式启动**服务器**，同时运行游戏服务器和 Mirror：

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer
```

以 Master 模式启动**服务器**，并设置自定义限制：

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer -maxCars:100 -maxCarsByPlayer:15 -maxNikLength:20 -idleKickTimeout:120s
```

以 Master 模式启动**服务器**，并使用自定义配置文件：

```
game.exe -bend_GameServer -batchmode -nographics -port:7800 -maxConn:32 -Mode:Race -masterServer -serversList:myservers.json -eventsList:myevents.json
```

> [!NOTE]
> 模组包包含用于快速启动服务器的 `runServer.cmd` 和 `runServer.sh` 脚本，可直接在文件内方便地配置参数。

# 兼容模式

> [!NOTE]
> 模组仅完整支持游戏版本 `9.4`，即最新的 Steam 版本。

在较早版本的游戏上启动时，模组会进入兼容模式，并尝试提供不含在线功能的基本功能。

> [!WARNING]
> **不保证**游戏及其离线功能在兼容模式下稳定运行。

未来计划改进兼容模式和对旧版游戏的支持。

# 连接服务器

可以通过**服务器连接（Server Connection）**窗口连接服务器。

窗口包含两个选项卡：**主服务器连接（Master Connection）**和**直接连接（Direct Connection）**。

连接主服务器时，游戏会从主服务器获取服务器列表，并显示在菜单中。地址和端口会被保存。

使用直接连接时，游戏会根据 IP 和端口立即连接游戏服务器。在这种情况下，菜单中不会显示在线状态。
