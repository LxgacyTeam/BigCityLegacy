# BigCityLegacy.Api.LegacyDialogs 与 LegacyGame

面向 MadOut2 9.4 的 BigCityLegacy 1.4.2 客户端辅助 API。引用 `BigCityLegacy.dll`，使用 `BigCityLegacy.Api`，并声明对 `com.bigcitylegacy.baseplugin` 1.4.2 或更高版本的 BepInEx 依赖。

## 返回结果的对话框

`Task<bool> LegacyDialogs.ShowAsync(LegacyDialogOptions options)` 将对话框加入队列。点击 Yes 后返回 `true`；点击 No、关闭按钮或 UI 停用后返回 `false`。结果在关闭动画结束后完成。`void LegacyDialogs.Show(LegacyDialogOptions options, Action<bool> completed)` 提供在 Unity 主线程执行的回调版本。

```csharp
using BigCityLegacy.Api;

private async void AskToRemove()
{
    bool yes = await LegacyDialogs.ShowAsync(new LegacyDialogOptions
    {
        Title = "Remove preset", TitleRu = "Удаление пресета",
        Message = "Remove the selected preset?", MessageRu = "Удалить выбранный пресет?",
        NoBtnText = "Keep", NoBtnTextRu = "Оставить",
        YesBtnText = "Remove", YesBtnTextRu = "Удалить",
        NoBtnType = LegacyDialogButtonType.Normal,
        YesBtnType = LegacyDialogButtonType.Danger
    });
    if (yes)
    {
        // Remove the preset in the plugin's own code.
    }
}
```

`LegacyDialogOptions` 包含以下公开字段：

| 字段 | 用途 |
| --- | --- |
| `Title`、`Message` | 标题和正文，默认空字符串。 |
| `NoBtnText`、`YesBtnText` | 按钮文字，`null` 使用本地化的 No/Yes。 |
| `NoBtnType`、`YesBtnType` | `LegacyDialogButtonType.Normal`、`.Green` 或 `.Danger`，默认 Normal。 |
| `TitleRu`、`MessageRu`、`NoBtnTextRu`、`YesBtnTextRu` | 可选俄语版本，按游戏语言选择；空字段回退至主要文字。 |

正文位于 HintBox 内，较长内容使用 `LegacyUIScrollView`。禁用 rich text。`\n` 与 `\t` 按普通 C# 转义序列传入；不展开 `{nickname}` 等服务器占位符。各语言分别限制：标题 160、正文 8000、按钮文字 80 字符（`string.Length`）。无效参数或按钮类型抛出 `ArgumentException`；options/callback 为 null 时抛出 `ArgumentNullException`。

入队时复制参数，之后修改 options 对象不会改变对话框。可以从后台线程调用，UI 在主线程运行。队列最多容纳 32 个等待中的对话框，满时抛出 `InvalidOperationException`。在主线程使用 `await`，不要使用 `.Result`/`.Wait()`。这是客户端 API，服务器不创建 UI。

## 输入和鼠标

所有对话框及 [Alerts](LegacyAlerts_ZH.md)（包括 hint）均使用 `LegacyUIInputBlockMode.Window`，只在窗口范围内屏蔽输入。显示期间启用游戏鼠标，关闭动画结束且最后一个此类窗口关闭后才释放。如果之前隐藏鼠标，则在兼顾其他原生菜单的情况下恢复隐藏状态。同时显示的 Alerts 和 Dialogs 不会互相提前释放鼠标。

## 重启游戏

`Task<bool> LegacyGame.RestartAsync(bool confirm = true)` 询问是否重启以应用更改。`confirm: false` 跳过确认框。`LegacyGame.IsRestarting` 表示当前已接受的重启尝试。

```csharp
bool accepted = await LegacyGame.RestartAsync(); // Ask for confirmation.
bool acceptedQuietly = await LegacyGame.RestartAsync(confirm: false);
bool restarting = LegacyGame.IsRestarting;
```

`false` 表示用户拒绝。`true` 表示辅助进程已启动且旧进程收到退出请求，并不保证新进程已成功运行。准备失败时 Task 以异常结束。若游戏拒绝退出，等待中的辅助进程会被取消并显示 Alert。不允许并行发起多个重启。

仅适用于独立客户端，不适用于 dedicated 服务器或 Unity Editor。保留可执行文件、工作目录和启动参数，由外部辅助进程等待旧进程结束。Windows 保留加载器环境、清除 Doorstop 进程标记，并在退出后仅删除 `maybe_bad_settings` 标记，避免原生异常启动检查重置画质。Linux 存在 `run_bepinex.sh` 时使用它，否则启动原始可执行文件。不会导入或替换玩家存档；插件应在重启前保存自身设置。

插件启用 API 在用户同意整批更改后统一执行一次无额外提示的重启。普通设置菜单可直接调用 `await LegacyGame.RestartAsync()`。

[插件启用 API](LegacyPluginActivation_ZH.md) · [开发文档](README.md)
