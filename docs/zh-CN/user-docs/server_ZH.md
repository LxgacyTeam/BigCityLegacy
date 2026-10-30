# 服务器

游戏服务器设置保存在 `BigCityLegacy/config/ServerConfig.toml`。模板位于 `config_example/ServerConfig.toml`；标准文件不存在时，服务器会自动创建它。

```text
game.exe -bend_GameServer -batchmode -nographics
game.exe -bend_GameServer -batchmode -nographics -serverConfig:"C:\Servers\race.toml"
```

启动服务器必须使用 `-bend_GameServer`，该标志不属于 TOML 配置。`-serverConfig` 仅选择配置文件，不启用服务器模式。普通客户端不读取服务器配置。明确指定的命令行参数优先于 TOML 值及其别名。空路径使用默认文件；相对路径以游戏根目录为基准。Windows 路径可使用单引号。

`masterServer = true` 是默认值，表示同时启动游戏服务器和 Mirror。仅启动游戏服务器时，设置 `masterServer = false`。Mirror 端口通过 TOML 的 `masterPort` 或 `-masterPort:<端口>` 设置。`ServersList.json` 保存服务器列表和轮询间隔。



| 节 | 参数 |
| --- | --- |
| `server` | `port`（1–65535）、`maxConn`（1–100）、`Mode`（`None`、`RP`、`Race`、`CS`、`CS_Sur`）、`lang`、`maxCars`、`maxCarsByPlayer`、`maxVinylLayers`、`CarSpawnTimeout`、`maxNikLength`、`idleKickTimeout`, `disconnectedKickTimeout`、`noChatEvents`、`noKillChat`、`noCsDamageCooldown`、`useVanillaRespawn` |
| `plugins` | `noPluginsPolicyAndAlerts` |
| `mirror` | `masterServer`（默认 `true`）、`masterPort`（默认 `35000`）、`masterDebug` |
| `files` | `serversList`、`eventsDir`、`respawnPoints`、`banList`、`pluginsPolicy`、`alerts` |
| `console` | `noServerConsole`、`noServerCli`、`verboseServerConsole`、`noUpdCheck` |
| `logging` | `logsDir`, `noLogging`, `chatLogging`, `logLevel` |

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

[plugins]
noPluginsPolicyAndAlerts = false

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
pluginsPolicy = ''
alerts = ''

[console]
noServerConsole = false
noServerCli = false
verboseServerConsole = false
noUpdCheck = false

[logging]
logsDir = ''
noLogging = false
chatLogging = false
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
| banlist | 显示当前有效的封禁列表。 |
| kick {PlayerID} [reason] | 踢出玩家。 |
| kill {PlayerID} | 杀死玩家角色。 |
| ban {PlayerID} {1/2} [time] [reason] | 封禁玩家；1 = 按 GUID 封禁，2 = 按 IP 封禁。 |
| unban {BanID} | 根据 BanID 解除封禁。 |
| setprop {PlayerID} {prop} {0/1} [time] | 设置或移除属性；`help setprop` |
| tp {PlayerID} {preset/x y z/json} [wcar] | 按坐标传送；`help tp` |
| tpto {PlayerID} {to PlayerID} [wcar] | 传送到玩家 |
| clearchat | 清理聊天 |
| help [setprop/tp/alert] | 命令帮助 |
| reloadmirror | Mirror 运行时重载当前 ServersList.json。 |
| reloadcfg | 重新加载配置：EventsList、RespawnPoints、BanList、PluginsPolicy、Alerts。 |
| alert {PlayerID/all} {message} {small/large/hint} [seconds] | 向指定玩家或所有人显示提示；`help alert` |
| msg {text} | 以服务器名义发送消息。详见下方说明。 |
| cleardrop | 清理服务器上的所有掉落物：医疗包和武器。 |
| delcars all | 删除服务器上的所有车辆。 |
| delcars {PlayerID} | 删除指定玩家的所有车辆。 |
| stats | 显示资源消耗和加载统计信息。 |
| showcfg | 显示当前生效的服务器配置。 |
| stop | 停止服务器。 |

`reloadmirror` 仅在 Mirror 运行时可用：重新读取 ServersList.json，无需重启游戏服务器，出错时保留旧列表。`banlist` 显示有效封禁，不包含已过期封禁和 Type 0 记录。`showcfg` 显示启动时加载的 ServerConfig 值，包括默认值及启动参数覆盖。

> [!WARNING]
> 目前，`msg` 命令要求服务器上至少有一名玩家，并且实质上以该玩家的名义发送消息，仅将昵称替换为 "Server"。这是由游戏当前的聊天架构造成的。

#### PlayerID

服务器会为每位玩家分配一个临时且唯一的 **PlayerID**，仅在该玩家当前的游戏会话中有效。每次有新玩家连接，PlayerID 计数器都会递增，且永不清零、永不递减。玩家重新连接服务器时会获得新的 PlayerID，旧 ID 不会再次使用。PlayerID 显示在 `players` 命令输出的玩家列表中。

#### 封禁与玩家属性

`ban <PlayerID> <1|2> [time] [reason]` 按 GUID（`1`）或 IP（`2`）禁止连接。可选时长为正整数加 `s`、`m`、`h` 或 `d`，例如 `30s`、`15m`、`12h`、`5d`；省略表示永久封禁。示例：`ban 15 1 12h`。命令不接受类型 `0`。

GUID 标识游戏存档并保存在 PlayerPrefs 中。删除存档会生成另一个 GUID，因此按 GUID 封禁不能阻止创建新存档。

`setprop <PlayerID> <noweapon|nomove|nocars|nochat|invisible> <0|1> [time]` 设置（`1`）或移除（`0`）属性。时长格式与封禁相同，移除时忽略时长。重复设置会替换该属性的时长。示例：`setprop 15 nochat 1 15m`、`setprop 15 nochat 0`。

| 属性 | 作用 |
| --- | --- |
| `noweapon` | 禁止射击及放置、引爆炸弹，并将所选武器切换为拳头。 |
| `nomove` | 冻结角色并使其离开车辆。服务器拒绝移动和进入车辆；BCL 客户端还会屏蔽移动输入。原版客户端通过原有的地形加载与传送流程修正位置。 |
| `nocars` | 禁止生成车辆，不删除已有车辆。 |
| `nochat` | 不发送该玩家的聊天消息。 |
| `invisible` | 在 BCL 客户端隐藏头顶昵称、生命条、地图标记和玩家列表中的该玩家。服务器不发送关于该玩家的连接、重连、断线、改名及击杀聊天事件；隐身玩家击杀他人时不会暴露其昵称。角色本身仍可见。原版客户端没有对应的 UI 隐藏网络标志，继续按原有规则显示。 |

玩家进入车辆时，服务器检查车辆在服务器上的锁定状态。进入锁定车辆会删除该车，并以 `Suspicion of cheat usage` 为由踢出玩家。伪造请求若指向远处车辆或无效座位，玩家仍会被踢出，但车辆不会被删除。

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

服务器每次启动都会创建独立文件 `serverlog_<port>_<yyyy-MM-dd_HH-mm-ss-fff>.log`。文件名和记录时间均使用 UTC。日志收集所有线程的 Unity 消息、BepInEx 日志（避免重复记录 Unity 消息），以及 BCL 控制台输出和输入的 CLI 命令。每条记录包含级别、来源、线程 ID 及 Unity 提供的堆栈信息。记录会立即刷新到文件系统，BCL 不在内存中保留历史列表。采用原子文件创建方式，即使多个实例同时启动也不会覆盖彼此的文件。旧日志不会自动删除。

`[logging]` 中的 `chatLogging = true` 或 `-chatLogging` 启用独立的 `chatlog_<port>_<yyyy-MM-dd_HH-mm-ss-fff>.log` 文件。该设置独立于 `noLogging` 和服务器日志级别。

日志默认保存在 `BigCityLegacy/logs`。

`[logging]` 中的参数也可通过启动参数设置：

| TOML | 启动参数 | 用途 |
| --- | --- | --- |
| `logsDir = ''` | `-logsDir:"server-logs/instance-1"` | 日志目录。空值使用 `BigCityLegacy/logs`；相对路径以游戏目录为基准，也支持绝对路径。目录会自动创建。 |
| `noLogging = false` | `-noLogging` | 关闭默认启用的 BCL 文件日志。 |
| `chatLogging = true` | `-chatLogging` | 启用聊天日志，默认关闭。 |
| `logLevel = "Log"` | `-logLevel:Warning` | 最低级别，按严重程度递增为 `Log`、`Warning`、`Error`、`Exception`；`Assert` 按 `Error` 记录。 |

详细诊断仅在 Debug 构建中通过 `-serverDiagnostics` 启用；完整追踪需设置 `logLevel = "Log"`。诊断会在网络缓冲区接收、UNet 分发、玩家初始化、车辆生成、XML 加载和父对象设置周围记录 `ENTER`/`EXIT`/`THROW`，包含操作 ID、父操作 ID 和嵌套深度。可用时还记录字符串/缓冲区长度、读取位置、消息类型、通道、连接 ID 和网络对象 ID。追踪不会读取或修改数据包，也不会保存数据包或 XML 的内容。在深度 32 和 64 时记录 managed stack trace；每 30 秒记录内存、GC 和玩家数。诊断会增加磁盘负载，适用于问题调查。`noLogging = true` 会禁用诊断；普通运行不安装这些网络追踪补丁。

原生 `m_log_<port>.log` 默认关闭。仅在启动时传入 **`-mlog`** 可独立于 BCL 日志启用它。

## 插件准入与 BCL 版本

连接时，BCL 客户端上报自身 BigCityLegacy 版本，以及已加载的 BepInEx 插件 GUID、版本和名称。依赖 DLL 和未被 BepInEx 作为插件加载的文件不在列表中。服务器在创建玩家角色之前决定是否允许进入。`playerinfo <PlayerID>` 显示收到的插件列表。

规则保存在 `BigCityLegacy/config/PluginsPolicy.json`。`allowClientsWithoutManifest` 和 `allowClientsWithoutBclVersion` 分别控制是否允许缺少插件列表或 BCL 版本的客户端。两者默认均为 `true`，`bcl` 默认为 `null`，两个列表均为空，因此原版和旧版客户端可以进入。若要求完整上报，将两个设置均设为 `false`。

`[plugins] noPluginsPolicyAndAlerts = true` 或 `-noPluginsPolicyAndAlerts` 完全关闭服务器的插件/BCL 版本检查和 Alerts 发送。两个文件均不读取，普通封禁仍然有效。该设置在启动时生效。

```json
{
  "version": 1,
  "allowClientsWithoutManifest": false,
  "allowClientsWithoutBclVersion": false,
  "bcl": {
    "minVersion": "1.4.2",
    "message": "BigCityLegacy {pluginVersion} is not supported. Required: {requiredVersion}.",
    "messageRu": "BigCityLegacy версии {pluginVersion} не поддерживается. Необходима версия: {requiredVersion}."
  },
  "hardlist": [
    {
      "guid": "com.bigcitylegacy.enhancedcartuning",
      "name": "EnhancedCarTuning",
      "minVersion": "1.0.0",
      "extra": "Install the required plugin before reconnecting.",
      "extraRu": "Перед повторным подключением установите необходимый плагин."

    }
  ],
  "blacklist": [
    {
      "guid": "com.example.prohibited",
      "message": "Remove {pluginName} before joining.",
      "messageRu": "Удалите {pluginName} перед подключением."
    }
  ]
}
```

`hardlist` 要求存在指定 GUID 且版本符合规则。`blacklist` 禁止指定 GUID；若设置了版本或范围，则仅禁止匹配的版本。GUID 比较不区分大小写，名称仅用于显示。`version` 要求精确匹配，`minVersion` 和 `maxVersion` 为包含边界的范围；可选择精确版本，或单独/同时使用上下限。缺少的数字段补零（`1.4 == 1.4.0.0`）；预发行版低于对应正式版，build metadata 不参与比较。`bcl` 独立限制 BCL 版本；已上报但不符合规则的版本始终被拒绝，即使允许不报版本。

规则中的 `message` 替换该插件的全局不兼容模板，`extra` 追加一个段落，`bcl` 规则也支持这些字段。所有不兼容项合并显示。缺少插件名称时，使用规则的 `name` 或 GUID。

允许缺少上报时，服务器无法检查该客户端的 hardlist/blacklist；允许缺少 BCL 版本时也不检查该版本。已提交但损坏的上报会被拒绝，不视为缺失。该机制依赖客户端报告的兼容性信息，不能证明客户端没有作弊：修改后的客户端可以伪造上报。

上报限制：最多 256 个插件；GUID/名称各 128 字符；版本 64 字符；二进制列表 20000 字节；包含列表的完整连接 XML 30000 字节。完整列表放不下时，客户端取消连接并显示原因，不发送残缺列表。通道使用原生登录 XML 的附加属性，旧服务器会忽略它们。在线协议版本保持 94。

## 玩家提示

模板保存在 `BigCityLegacy/config/Alerts.toml`。节包括 `plugins`、`missingManifest`、`missingBclVersion`、`invalidManifest`、`banRejected`、`banned`、`kicked`、`welcome`。每节支持 `enabled`、`title`、`message`、`display`、`duration`。省略的节使用内置默认模板。Welcome 默认关闭，其余默认开启；默认类型为 `small`，Welcome 为 `large`。

`small` 使用带 HintBox 的可拖动窗口；`large` 使用 ScrollView；`hint` 在屏幕顶部显示 HintBoxAlt，持续 `duration` 秒（0.1–3600，默认 6）。较长的 small 消息自动启用滚动。Hint 不阻止输入；打开的对话框显示鼠标并阻止点击穿透至游戏。拒绝连接和踢出消息在断线后仍保留。队列大小有限；hint 的计时从实际显示时开始，而非接收时。

Alerts.toml 每节都可选填 `titleRu` 和 `messageRu`。客户端按当前游戏语言选择俄语文本；俄语字段缺少或为空时回退至主要字段。PluginsPolicy.json 的每条规则也支持 `messageRu` 和 `extraRu`，包括 `bcl`。

```toml
[welcome]
enabled = true
title = "Server rules"
titleRu = "Правила сервера"
display = "large"
message = """
Welcome, {nickname}!
1. Respect other players.
2. Follow the server rules.
"""
messageRu = """
Добро пожаловать, {nickname}!
1. Уважай других игроков.
2. Соблюдай правила сервера.
"""
updatedAt = 1793150000

[kicked]
title = "Disconnected by administrator"
titleRu = "Отключен администратором"
message = "{nickname}, you were kicked.\nReason: {reason}"
messageRu = "{nickname}, вы были отключены от сервера.\nПричина: {reason}"
display = "small"

[plugins]
title = "Plugin compatibility"
titleRu = "Совместимость плагинов"
message = "{reason}\n{pluginName}: {pluginVersion}\nRequired: {requiredVersion}"
messageRu = "{reason}\n{pluginName}: {pluginVersion}\nНеобходима: {requiredVersion}"
display = "small"

[banned]
title = "Banned!"
titleRu = "Вы были забанены!"
message = "{nickname}\nBan ID: {banId}\nDuration: {duration}\nReason: {reason}"
messageRu = "{nickname}\nID бана: {banId}\nДлительность: {duration}\nПричина: {reason}"
display = "hint"
duration = 15
```

双引号字符串使用 `\n` 换行、`\t` 制表；长消息支持三重双引号。TOML 单引号字符串为字面量。JSON 中的 `message`/`extra` 使用标准 JSON 转义。变量仅替换一次，不会再次解析转义序列或 rich text。

| 变量 | 适用范围 |
| --- | --- |
| `{nickname}`、`{serverPort}`、`{bclVersion}`、`{reason}` | 通用模板及手动消息；未知版本为 `not reported`，未指定原因为 `Not specified`。 |
| `{playerId}` | 创建服务器会话后可用，准入前为空字符串。 |
| `{banId}`、`{until}`、`{duration}` | 封禁 ID、到期 Unix 时间戳或 `permanent`、剩余时长或 `perm`。原因来自 `BanList.json` 的 `Reason`。 |
| `{pluginGuid}`、`{pluginName}`、`{pluginVersion}`、`{requiredVersion}`、`{rule}` | 插件/BCL 不兼容；规则为 `hardlist`、`blacklist` 或 `bcl`，未安装插件的版本为 `not installed`。 |

命令：

```text
alert <PlayerID|all> <message> <small|large|hint> [seconds] [title="Text"] [titleRu="Текст"] [messageRu="Текст"]
alert all "Welcome, {nickname}!\nRead the rules." large
alert all "Maintenance soon" hint 10 title="Notice" titleRu="Объявление" messageRu="Скоро техработы"
help alert
```

封禁时长使用 `s/m/h/d`，不指定则为永久。原因随封禁保存，可用于后续连接被拒绝时的消息。`all` 会为每位接收者分别替换变量。旧版/原版客户端不接收 alert；拒绝连接、踢出或封禁时按原有流程断开，提示 UI 需要 BCL 1.4.2+。服务器立即清理游戏会话，同时最多保留两秒传输时间等待 alert 确认；未收到确认不会保留会话。

启动和执行 `reloadcfg` 时，服务器分别记录 PluginsPolicy 和 Alerts 加载成功或失败的信息。

`reloadcfg` 原子重载 PluginsPolicy 和 Alerts；错误时保留之前可用的一对配置。规则仅影响后续连接，不会自动踢出已连接玩家。启动时配置损坏会阻止服务器启动。可通过 ServerConfig.toml 的 `[files] pluginsPolicy = '...'`、`alerts = '...'`，或 `-pluginsPolicy:"path"`、`-alerts:"path"` 指定路径。相对路径以游戏目录为基准；明确指定但不存在的文件视为错误。

Welcome 等待游戏世界加载、角色生成和 fade 完全结束后显示。

Welcome 带有“不再显示”开关。选择及 `updatedAt` 按服务器实际 IP:port 分别保存到 PlayerPrefs。修改规则时，将 `updatedAt` 设为新的 Unix 时间戳（UTC 整数秒，默认 `0`），并执行 `reloadcfg`。值变化后取消隐藏，再次显示更新后的消息。

客户端 C# API 另见：[BigCityLegacy.Api.LegacyAlerts](../dev-docs/LegacyAlerts_ZH.md)。

`banRejected` 用于已被封禁的玩家尝试连接；`banned` 用于封禁已连接玩家。两个模板分别配置。
