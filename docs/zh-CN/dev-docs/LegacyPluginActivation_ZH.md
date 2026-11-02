# 插件启用状态 API

BigCityLegacy 1.4.2 的客户端 API 面向 MadOut2 9.4，位于 `BigCityLegacy.Api`。引用 `BigCityLegacy.dll`，并声明对 `com.bigcitylegacy.baseplugin` 1.4.2 或更高版本的 BepInEx 依赖。此 API 管理已加载插件的逻辑状态，不负责加载或卸载 DLL。

## 接口与显式支持

插件主类必须同时声明 `[LegacyPluginActivation]` 并实现 `ILegacyPluginActivation`。必需成员为 `bool IsEnabled { get; }`、`void Enable()`、`void Disable()`。该属性不继承。`[LegacyPluginActivation(false)]`、未声明属性或未实现接口均表示不支持；BCL 不读取状态，也不调用相关方法。

`RequiresRestart` 默认是 `true`。`Enable` 与 `Disable` 使用插件自己的存储方式保存目标设置。重启后由插件读取设置并初始化。`IsEnabled` 应报告当前进程中的实际状态，而非为下次启动保存的目标状态。功能关闭时插件仍需保留在 Chainloader 中，否则 BCL 无法再次启用它。

确实支持即时切换时，设置 `RequiresRestart = false`：方法返回前必须完成切换，`IsEnabled` 应立即反映结果。方法和 getter 在 Unity 主线程执行。实现应具备幂等性并同步完成；BCL 不等待插件内部的后台操作。不要在 `Enable`/`Disable` 内重启游戏；BCL 会先处理整批更改，再统一重启一次。

## 保存设置的示例

```csharp
using BepInEx;
using BepInEx.Configuration;
using BigCityLegacy.Api;

[BepInPlugin("com.example.map", "Example Map", "1.0.0")]
[BepInDependency("com.bigcitylegacy.baseplugin", "1.4.2")]
[LegacyPluginActivation(RequiresRestart = true)]
public sealed class ExampleMapPlugin : BaseUnityPlugin, ILegacyPluginActivation
{
    private ConfigEntry<bool> enabledSetting;
    private bool activeAtStartup;
    public bool IsEnabled => activeAtStartup;

    private void Awake()
    {
        enabledSetting = Config.Bind("General", "Enabled", true);
        activeAtStartup = enabledSetting.Value;
        if (activeAtStartup)
        {
            // Initialize the plugin's own map/resources here.
        }
    }

    public void Enable() { enabledSetting.Value = true; }
    public void Disable() { enabledSetting.Value = false; }
}
```

该示例用于展示接口，并非具体地图的实现。初始化逻辑与设置存储由插件作者负责。`RequiresRestart = true` 时，`activeAtStartup` 在下次启动之前保持不变。

## 查询和修改状态

在 Unity 主线程调用 `LegacyPlugins.GetStatus(string pluginGuid)`。返回的 `LegacyPluginStatus` 包含 `Guid`、`Name`、`Version`、`Installed`、`SupportsActivation`、`IsEnabled`、`RequiresRestart`。先检查 `Installed` 与 `SupportsActivation`；仅支持 API 的插件，其 `IsEnabled` 才可作为实际状态使用。GUID 比较不区分大小写。

`Task<bool> LegacyPlugins.SetEnabledAsync(string pluginGuid, bool enabled, bool confirm = true)` 显示确认框，在主线程调用对应方法，必要时通过 `LegacyGame` 重启。`false` 表示用户拒绝；`true` 表示即时切换完成，或重启请求已被接受。插件不存在、不支持、确认期间版本变化，或方法执行失败时，Task 以异常结束。即时切换还会验证最终实际状态。

```csharp
LegacyPluginStatus status = LegacyPlugins.GetStatus("com.example.map");
if (status.Installed && status.SupportsActivation && !status.IsEnabled)
{
    bool applied = await LegacyPlugins.SetEnabledAsync(status.Guid, true);
}
```

使用 `await`，不要在主线程用 `.Result` 或 `.Wait()` 阻塞。从后台线程开始的 `await` 不会自动赋予后续代码访问 Unity 对象的能力。插件方法在抛出异常前已执行的任意更改，无法自动回滚。

## 服务器策略

客户端随插件列表上报 API 支持、实际状态及是否需要重启。`PluginsPolicy.json` 中每条规则的 `controlByStatus` 决定是否按状态检查。未开启时按安装情况检查。不支持 API 的插件，以及未上报状态的旧客户端，也保留安装情况检查。

hardlist 中已安装且版本合适、但关闭的插件会触发启用建议。blacklist 中已启用且支持 API 的插件会触发关闭建议，已关闭的则允许进入。版本限制仍然有效。插件缺失或版本不合适时，仍需常规安装或更新。

```json
{
  "version": 1,
  "hardlist": [
    { "guid": "com.example.map", "minVersion": "1.0.0", "controlByStatus": true }
  ],
  "blacklist": [
    { "guid": "com.example.othermap", "controlByStatus": true }
  ]
}
```

服务器在生成角色前结束本次连接，并发送有限制的更改请求。客户端显示服务器地址、插件名称/GUID 及重启提醒。只有用户同意后才调用本地方法。拒绝则保持设置不变。更改后需手动重新连接，不会自动重连。网络确认包仅表示已收到，不表示用户同意。

只有全部不兼容项都能通过切换状态解决时才提出更改。混合错误（如一个地图关闭且另一个插件缺失）仅显示普通拒绝消息，不会部分更改。每次最多 32 个插件。新的连接尝试会使旧的未确认请求失效。不能用 `controlByStatus` 管理 BCL 自身；同一 GUID 不能同时在 hardlist 与 blacklist 中启用此选项。

上报内容来自客户端，不能证明客户端未被修改。基本插件列表格式与旧服务器兼容，online protocol 保持为 94。

[对话框与重启](LegacyDialogs_ZH.md) · [服务器](../user-docs/server_ZH.md) · [开发文档](README.md)
