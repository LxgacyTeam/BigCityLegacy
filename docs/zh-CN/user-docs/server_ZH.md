# 服务器

游戏服务器通常可在两种模式下运行：**Master** 和 **Base**。

**Base** 表示仅启动游戏服务器（Game Server），通过基本的 `-bend_GameServer` 标志启用。
**Master** 表示 Mirror + Game Server：除游戏服务器外，同时启动一个 Mirror。此时，Master 服务器应作为包含多个游戏服务器的架构中的主服务器，将它们连接在一起。

---

# 游戏服务器（Game Server）

**Game Server** 是实际的游戏服务器，对应一个在线大厅。

Game Server 有五种模式：`None`、`RP`、`Race`、`CS`、`CS_Sur`。

* **None** - 基础模式，在游戏中显示为自由漫游（FreeRoam）。其他所有模式均基于此模式。

- **RP** - 在 None 模式的基础上禁用武器。
- **Race** - 在 None 模式的基础上定期启动在线竞速活动。
- **CS** - 在 None 模式的基础上定期启动警匪对抗活动，实质上是团队死斗。
- **CS_Sur** - 在 None 模式的基础上定期启动生存（Survival）活动，实质上是死斗/自由混战（FFA）。

Game Server 通过启动参数配置。请参阅[启动参数](overview_ZH.md#启动参数)。它也支持通过交互式 CLI 进行管理，详见下文。

服务器玩家数量在实际使用中没有硬性上限，但游戏设计面向**不超过 100 名玩家**。

---

# Mirror 服务器

**Mirror 服务器**负责管理游戏服务器列表及其状态。它向游戏客户端发送可用服务器列表、服务器设置及当前在线人数。换言之，它决定游戏在**在线（Online）**区域的服务器列表中显示什么。Mirror 可以接收和发送服务器名称、最大玩家数、语言、游戏模式、分组位置及其他一些参数。它还提供自动选择服务器的功能，供玩家点击游戏中的绿色连接（Connect）按钮时使用。

Mirror 服务器的配置存储在独立的 JSON 配置文件中，默认路径为 `BigCityLegacy/ServersList.json`。配置示例：

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

**参数含义：**

`masterPort` - 主服务器使用的端口。

`pollIntervalMs` - 轮询游戏服务器、检查其是否在线的时间间隔。

`staleAfterMs` - 服务器不再可用后，经过多长时间将其列表条目视为已过期。

`servers` - 列表中显示的游戏服务器数组。

`id` - 每台服务器的唯一 ID。

`name` - 菜单中显示的服务器名称。

`connectIp` - 传递给游戏客户端的服务器公网 IP。

`queryIp` - Mirror 用于轮询的服务器 IP。如果服务器位于同一局域网或同一台机器上，将公网 IP 与内部 IP 分开设置会更方便。

`port` - 传递给游戏客户端的服务器端口。

`maxPlayers` - 菜单中显示的服务器最大玩家数。

`lang` - 菜单中显示的服务器语言。

`menuGroup` - 游戏菜单中显示该服务器的分类。可接受的值：`FreeRoam`、`RP`、`Race` 或 `CS`。

`serverMode` - 服务器上的游戏模式。

`onlineProtocolVersion` - 在线协议版本。游戏版本 9.4 对应的协议版本为 93。

`includeInAuto` - 是否将该服务器纳入自动连接的候选范围。

`showIfOffline` - 服务器离线时是否仍在游戏中显示。

`self` - 标记主服务器自身的游戏服务器。只能指定一次。

如果希望同时运行多个游戏服务器，并在游戏中以列表形式显示它们，就需要一台 Master 服务器。预期且推荐的架构为 `1 Master Server + n Base servers`。

---

# 管理与 CLI

游戏服务器运行期间，可以通过服务器控制台窗口中的交互式 CLI 进行控制。支持以下命令：

| 命令 | 操作 |
| --------------------- | ------------------------------------------------------------------------------------------------- |
| players | 显示服务器当前的玩家列表。 |
| playerinfo {PlayerID} | 显示某位玩家的信息。 |
| kick {PlayerID} | 踢出玩家。 |
| ban {PlayerID} {1/2} | 封禁玩家；1 = 按 GUID 封禁，2 = 按 IP 封禁。 |
| unban {BanID} | 根据 BanID 解除封禁。 |
| reloadcfg | 重新加载配置：EventsList、RespawnPoints、BanList。 |
| msg {text} | 以服务器名义发送消息。详见下方说明。 |
| cleardrop | 清理服务器上的所有掉落物：医疗包和武器。 |
| delcars all | 删除服务器上的所有车辆。 |
| delcars {PlayerID} | 删除指定玩家的所有车辆。 |
| stop | 停止服务器。 |

> [!WARNING]
> 目前，`msg` 命令要求服务器上至少有一名玩家，并且实质上以该玩家的名义发送消息，仅将昵称替换为 "Server"。这是由游戏当前的聊天架构造成的。

#### PlayerID

服务器会为每位玩家分配一个临时且唯一的 **PlayerID**，仅在该玩家当前的游戏会话中有效。每次有新玩家连接，PlayerID 计数器都会递增，且永不清零、永不递减。玩家重新连接服务器时会获得新的 PlayerID，旧 ID 不会再次使用。PlayerID 显示在 `players` 命令输出的玩家列表中。

#### 封禁系统

可通过两种方式封禁玩家：按玩家 GUID 或按玩家 IP。

GUID 是存储在 PlayerPrefs 中的唯一游戏档案标识符，每次游戏创建新档案或存档时都会生成。实际上，只需删除游戏存档就可以轻易绕过 GUID 封禁。

解除封禁使用 **BanID**。该 ID 在封禁后输出到控制台，始终不变，并保存在封禁列表中。

封禁列表存储在 `BanList.json` 中。如果启动多个服务器实例，每个实例都可以通过启动参数指定自己的文件。
每条封禁记录按以下格式保存：

```json
    {
      "BanID": 1,
      "GUID": "1d9b4ba4-76f9-417f-b275-49e863726cb7",
      "IP": "1.2.3.4",
      "Type": 1,
      "Nickname": "BadPlayer"
    }

```

`Type` 定义封禁类型：1 = GUID，2 = IP。

按 IP 封禁 PlayerID 为 52 的玩家的命令示例：`ban 52 2`。

解除 BanID 为 37 的封禁记录的命令示例：`unban 37`。
