# 服务器

游戏服务器设置保存在 `BigCityLegacy/config/ServerConfig.toml`。模板位于 `config_example/ServerConfig.toml`；标准文件不存在时，服务器会自动创建它。

```text
game.exe -bend_GameServer -batchmode -nographics
game.exe -bend_GameServer -batchmode -nographics -serverConfig:"C:\Servers\race.toml"
```

启动服务器必须使用 `-bend_GameServer`，该标志不属于 TOML 配置。`-serverConfig` 仅选择配置文件，不启用服务器模式。普通客户端不读取服务器配置。明确指定的命令行参数优先于 TOML 值及其别名。空路径使用默认文件；相对路径以游戏根目录为基准。Windows 路径可使用单引号。

`masterServer = true` 是默认值，表示同时启动游戏服务器和 Mirror。仅启动游戏服务器时，设置 `masterServer = false`。Mirror 端口通过 TOML 的 `masterPort` 或 `-masterPort:<端口>` 设置。`ServersList.json` 保存服务器列表和轮询间隔。



每辆车的贴花图层上限通过 `[server]` 中的 `maxVinylLayers` 或 `-maxVinylLayers:<数量>` 设置。默认值为 `1024`，车身与玻璃图层合并计数。可用值为 `1..4096`；`0` 使用传输上限 `4096`。超过限制的车辆生成或涂装更新会被拒绝并写入服务器日志，不会截断贴花。

角色生命值由服务器控制，范围为 `0..100`，并定期同步给所属客户端。修理命令仅适用于车辆。医疗包保留原有的服务器拾取检查和恢复机制；修改本地 HP 不会恢复服务器上的生命值。

| 节 | 参数 |
| --- | --- |
| `server` | `port`（1–65535）、`maxConn`（1–100）、`Mode`（`None`、`RP`、`Race`、`CS`、`CS_Sur`）、`lang`、`maxCars`、`maxCarsByPlayer`、`CarSpawnTimeout`、`maxNikLength`、`idleKickTimeout`, `disconnectedKickTimeout`、`noChatEvents`、`noKillChat`、`noCsDamageCooldown`、`useVanillaRespawn` |
| `mirror` | `masterServer`（默认 `true`）、`masterPort`（默认 `35000`）、`masterDebug` |
| `files` | `serversList`、`eventsDir`、`respawnPoints`、`banList` |
| `console` | `noServerConsole`、`noServerCli`、`verboseServerConsole`、`noUpdCheck` |
| `logging` | `logsDir`, `noLogging`, `logLevel`, `serverDiagnostics` |
| `repository` | `repositorySource`：`GitHub`、`Codeberg`，或留空以使用 PlayerPrefs 保存的选择 |

配置示例：

```toml
# BigCityLegacy server configuration. CLI arguments override these values. 
# Keys match launch options without the leading '-'.

[server]
port = 7800
maxConn = 16
Mode = "None"
lang = "en"
maxCars = 100
maxCarsByPlayer = 15
maxVinylLayers = 1024
CarSpawnTimeout = 1000
maxNikLength = 20
idleKickTimeout = "120s"
disconnectedKickTimeout = "30s"
noChatEvents = false
noKillChat = false
noCsDamageCooldown = false
useVanillaRespawn = false

[mirror]
masterServer = true
masterPort = 35000
masterDebug = false

[files]
# Relative paths resolve from the game directory. Empty values use defaults.
serversList = ''
eventsDir = ''
respawnPoints = ''
banList = ''

[console]
noServerConsole = false
noServerCli = false
verboseServerConsole = false
noUpdCheck = false

[logging]
logsDir = ''
noLogging = false
logLevel = "Log"
```


游戏服务器通常可在两种模式下运行：**Master** 和 **Base**。

**Base** 仅启动游戏服务器，不启动 Mirror（`masterServer = false`）。
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

Game Server 通过 TOML 设置和启动参数配置。请参阅[启动参数](overview_ZH.md#启动参数)。它也支持通过交互式 CLI 进行管理，详见下文。

服务器玩家数量在实际使用中没有硬性上限，但游戏设计面向**不超过 100 名玩家**。

---

# Mirror 服务器

**Mirror 服务器**负责管理游戏服务器列表及其状态。它向游戏客户端发送可用服务器列表、服务器设置及当前在线人数。换言之，它决定游戏在**在线（Online）**区域的服务器列表中显示什么。Mirror 可以接收和发送服务器名称、最大玩家数、语言、游戏模式、分组位置及其他一些参数。它还提供自动选择服务器的功能，供玩家点击游戏中的绿色连接（Connect）按钮时使用。

Mirror 服务器的配置存储在独立的 JSON 配置文件中，默认路径为 `BigCityLegacy/config/ServersList.json`。配置示例：

```json
  {
  "version": 1,
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
      "onlineProtocolVersion": 94,
      "includeInAuto": true,
      "showIfOffline": false,
      "self": true
    }
  ]
}
```

**参数含义：**

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

`onlineProtocolVersion` 为在线协议版本，默认值为 `94`。全局值由 `BigCityLegacyPlugin.cs` 中的 `OnlineProtocolVersion` 常量定义。Mirror 的自身条目（`self`）使用该值；其他服务器条目可明确指定各自的版本。客户端参数 `-onlineVersion:93` 可降低版本；高于全局值的输入会被限制为全局值。

`includeInAuto` - 是否将该服务器纳入自动连接的候选范围。

`showIfOffline` - 服务器离线时是否仍在游戏中显示。

`self` - 标记主服务器自身的游戏服务器。只能指定一次。

如果希望同时运行多个游戏服务器，并在游戏中以列表形式显示它们，就需要一台 Master 服务器。预期且推荐的架构为 `1 Master Server + n Base servers`。

---

# 管理与 CLI

在交互式 Linux 控制台（包括 tmux）中，↑/↓ 可浏览最近最多 100 条非空命令，连续重复的命令只保留一条。在最新记录后按 ↓ 可恢复尚未提交的输入。支持 ←/→、Home/End、Backspace/Delete 和 Ctrl+U（清空输入）。长命令会水平滚动，输入上限为 4096 个字符。历史仅保留到当前进程结束；日志输出会保留正在输入的文本。重定向输入和 `TERM=dumb` 使用普通的逐行输入，不提供交互式历史。

游戏服务器运行期间，可以通过服务器控制台窗口中的交互式 CLI 进行控制。支持以下命令：

| 命令 | 操作 |
| --------------------- | ------------------------------------------------------------------------------------------------- |
| players | 显示服务器当前的玩家列表。 |
| playerinfo {PlayerID} | 显示某位玩家的信息。 |
| kick {PlayerID} | 踢出玩家。 |
| kill {PlayerID} | 杀死玩家角色。 |
| ban {PlayerID} {1/2} [time] | 封禁玩家；1 = 按 GUID 封禁，2 = 按 IP 封禁。 |
| unban {BanID} | 根据 BanID 解除封禁。 |
| setprop {PlayerID} {prop} {0/1} [time] | 设置或移除属性；`help setprop` |
| tp {PlayerID} {preset/x y z/json} [wcar] | 按坐标传送；`help tp` |
| tpto {PlayerID} {to PlayerID} [wcar] | 传送到玩家 |
| clearchat | 清理聊天 |
| help [setprop/tp] | 命令帮助 |
| reloadcfg | 重新加载配置：EventsList、RespawnPoints、BanList。 |
| msg {text} | 以服务器名义发送消息。详见下方说明。 |
| cleardrop | 清理服务器上的所有掉落物：医疗包和武器。 |
| delcars all | 删除服务器上的所有车辆。 |
| delcars {PlayerID} | 删除指定玩家的所有车辆。 |
| stats | 显示资源消耗和加载统计信息。 |
| stop | 停止服务器。 |

> [!WARNING]
> 目前，`msg` 命令要求服务器上至少有一名玩家，并且实质上以该玩家的名义发送消息，仅将昵称替换为 "Server"。这是由游戏当前的聊天架构造成的。

#### PlayerID

服务器会为每位玩家分配一个临时且唯一的 **PlayerID**，仅在该玩家当前的游戏会话中有效。每次有新玩家连接，PlayerID 计数器都会递增，且永不清零、永不递减。玩家重新连接服务器时会获得新的 PlayerID，旧 ID 不会再次使用。PlayerID 显示在 `players` 命令输出的玩家列表中。

#### 封禁与玩家属性

`ban <PlayerID> <1|2> [time]` 按 GUID（`1`）或 IP（`2`）禁止连接。可选时长为正整数加 `s`、`m`、`h` 或 `d`，例如 `30s`、`15m`、`12h`、`5d`；省略表示永久封禁。示例：`ban 15 1 12h`。命令不接受类型 `0`。

GUID 标识游戏存档并保存在 PlayerPrefs 中。删除存档会生成另一个 GUID，因此按 GUID 封禁不能阻止创建新存档。

`setprop <PlayerID> <noweapon|nomove|nocars|nochat|invisible> <0|1> [time]` 设置（`1`）或移除（`0`）属性。时长格式与封禁相同，移除时忽略时长。重复设置会替换该属性的时长。示例：`setprop 15 nochat 1 15m`、`setprop 15 nochat 0`。

| 属性 | 作用 |
| --- | --- |
| `noweapon` | 禁止射击及放置、引爆炸弹，并将所选武器切换为拳头。 |
| `nomove` | 冻结角色并使其离开车辆。服务器拒绝移动和进入车辆；BCL 客户端还会屏蔽移动输入。原版客户端通过原有的地形加载与传送流程修正位置。 |
| `nocars` | 禁止生成车辆，不删除已有车辆。 |
| `nochat` | 不发送该玩家的聊天消息。 |
| `invisible` | 在 BCL 客户端隐藏头顶昵称、生命条及地图标记，角色本身仍可见。原版客户端没有对应网络标志，继续按原有规则显示。此属性不发送聊天公告。 |

封禁与属性保存在 `BigCityLegacy/config/BanList.json`，可用 `banList` 指定路径。属性仅按 GUID 匹配，即使同一记录按 IP 封禁。`until` 为 UTC 秒数形式的 Unix 时间戳，支持 JSON 数字或数字字符串；省略表示永久。封禁与各属性分别计时，过期后立即失效并由服务器定期从文件中清理。手动编辑后使用 `reloadcfg`。

```json
{
  "Version": 1,
  "Bans": [
    {
      "BanID": 1,
      "GUID": "1d9b4ba4-76f9-417f-b275-49e863726cb7",
      "IP": "1.2.3.4",
      "Type": 0,
      "Nickname": "BadPlayer",
      "Props": [
        { "Prop": "noweapon", "until": 1792388642 },
        { "Prop": "nochat" }
      ]
    }
  ]
}
```

`Type`：`0` 为仅属性、未封禁；`1` 按 GUID 封禁；`2` 按 IP 封禁。临时封禁在 `Type`、`Nickname` 同级使用自己的 `until` 字段。

`unban <BanID>` 解除封禁：若记录仍有属性，则保留并设置 `Type: 0`；否则删除记录。该命令不移除属性。`playerinfo <PlayerID>` 会显示有效属性，例如 `Props: noweapon (5h), nochat (perm)`。

踢出与封禁公告显示具体原因和封禁时长，代替普通断开消息。启用聊天事件时，武器、车辆、聊天和移动限制也会发送公告。

#### 传送与清理聊天

`tp <PlayerID> <预设|x y z|json> [wcar]` 接受预设名称、小数点坐标或 Point Tool JSON，例如 `tp 15 police`、`tp 15 100.5 20 -30` 或 `tp 15 {"x":100.5,"y":20,"z":-30,"yaw":90} wcar`。JSON 必须包含 `x`、`y`、`z`，`yaw` 可选。预设包含位置和朝向：police, park, airport, sto1, sto2, sto3, sto4, desert, mountain, cemetry, derbyarena, mansion, farm。名称不区分大小写，省略 yaw 时保留原朝向。`tpto <PlayerID> <to PlayerID> [wcar]` 传送到另一玩家的当前位置。添加 `wcar` 时，车内角色会连同车辆及全部乘客一起传送；否则角色会下车。传送保留生命值并使用原有地形加载流程，冻结角色在目标位置继续冻结。例如：`tpto 15 16 wcar`。

`kill <PlayerID>` 通过原有服务器死亡机制将角色生命值设为零。`kick` 和 `ban` 立即删除游戏会话与角色。`disconnectedKickTimeout` 仅用于连接丢失后的会话保留，不用于管理员移除玩家。

`clearchat` 清空服务器历史，并通过已有 RPC 发送空消息来替换原版客户端聊天历史，无需新客户端协议。空行高亮按原有聊天高亮时间消退。

`help` 显示命令列表，`help setprop` 和 `help tp` 提供详细示例。

CS/CS_Sur 比赛进行时，自由模式角色在竞技区内停留五秒后会被击杀；驶入区域的车辆在五秒后也会被删除，即使司机已下车。离开区域或载有赛事参与者的车辆不会被删除，删除前会先让乘客下车。

## 日志与诊断

服务器每次启动都会创建独立文件 `BigCityLegacy/logs/port_yyyy-MM-dd_HH-mm-ss-fff.log`。文件名和记录时间均使用 UTC。日志收集所有线程的 Unity 消息、BepInEx 日志（避免重复记录 Unity 消息），以及 BCL 控制台输出和输入的 CLI 命令。每条记录包含级别、来源、线程 ID 及 Unity 提供的堆栈信息。记录会立即刷新到文件系统，BCL 不在内存中保留历史列表。采用原子文件创建方式，即使多个实例同时启动也不会覆盖彼此的文件。旧日志不会自动删除。

`[logging]` 配置项也可通过启动参数设置：

| TOML | 启动参数 | 说明 |
| --- | --- | --- |
| `logsDir = ''` | `-logsDir:"server-logs/instance-1"` | 日志目录。空值使用 `BigCityLegacy/logs`。相对路径以游戏目录为基准，也支持绝对路径。目录会自动创建。 |
| `noLogging = false` | `-noLogging` | 禁用默认启用的 BCL 文件日志。 |
| `logLevel = "Log"` | `-logLevel:Warning` | 最低级别，按严重程度递增：`Log`、`Warning`、`Error`、`Exception`。`Assert` 归为 `Error`。 |
| `serverDiagnostics = false` | `-serverDiagnostics` | 启用详细诊断跟踪。完整跟踪需使用 `logLevel = "Log"`。 |

诊断会在网络缓冲区接收、UNet 消息分发、玩家初始化、车辆生成、XML 加载和父对象设置前后记录 `ENTER`、`EXIT` 或 `THROW`，包含操作 ID、父操作 ID 和嵌套深度。还会记录字符串/缓冲区大小、读取位置、消息类型、通道，以及可用的连接 ID 和网络对象 ID。诊断不会读取或修改数据包，也不会保存数据包或 XML 的内容。嵌套深度达到 32 和 64 时记录托管堆栈；每 30 秒记录内存、GC 和玩家数量。诊断会增加磁盘负载，适合排查故障时启用。`noLogging = true` 会禁用诊断；普通启动不会安装诊断所用的网络补丁。

原生 `m_log_<port>.log` 默认禁用，只能通过启动参数 **`-mlog`** 单独启用；

