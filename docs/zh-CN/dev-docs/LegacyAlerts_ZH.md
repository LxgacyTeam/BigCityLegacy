# BigCityLegacy.Api.LegacyAlerts

BigCityLegacy 1.4.2 客户端 API，用于在 MadOut2 9.4 中显示 LegacyUIFramework 对话框和提示。添加对 `BigCityLegacy.dll` 的引用，并声明对 `com.bigcitylegacy.baseplugin` 1.4.2 或更高版本的 BepInEx 依赖。类型位于 `BigCityLegacy.Api` 命名空间。

## 公开签名

```csharp
namespace BigCityLegacy.Api
{
    public enum LegacyAlertDisplay { Small, Large, Hint }

    public static class LegacyAlerts
    {
        public static void Show(string message,
            LegacyAlertDisplay display = LegacyAlertDisplay.Small,
            string title = "", float duration = 6f);
        public static void ShowLocalized(string message, string messageRu,
            LegacyAlertDisplay display = LegacyAlertDisplay.Small,
            string title = "", string titleRu = null, float duration = 6f);
        public static void Small(string message, string title = "");
        public static void Large(string message, string title = "");
        public static void Hint(string message, float duration = 6f);
    }
}
```


| 类型 | 显示方式 |
| --- | --- |
| `Small` | 带 HintBox 的可拖动窗口。较长内容自动滚动；任一语言的正文超过 1000 字符时切换为 Large。 |
| `Large` | 带 ScrollView 的可拖动窗口，适合规则和长文本。 |
| `Hint` | 屏幕顶部的 HintBoxAlt，持续时间以秒计。BCL 使用 `ScaleMultiplier = 1.5f`；新 hint 替换当前 hint。 |

Small/Large 窗口显示鼠标并阻止点击穿透至游戏。普通 Hint 不捕获输入。对话框使用 LegacyUIFramework 动画；HintBoxAlt 还可通过 BCL 的视觉设置使用背景和 backdrop blur。LegacyAlerts 的公开方法不提供逐条消息的样式参数。

## 插件示例

```csharp
using BepInEx;
using BigCityLegacy.Api;

[BepInPlugin("com.example.notifications", "Notification Example", "1.0.0")]
[BepInDependency("com.bigcitylegacy.baseplugin", "1.4.2")]
public sealed class NotificationExample : BaseUnityPlugin
{
    private void Start()
    {
        LegacyAlerts.Small("Operation completed.", "Result");
        LegacyAlerts.Large("First paragraph.\n\nSecond paragraph.", "Details");
        LegacyAlerts.Hint("Settings saved.", duration: 5f);
        LegacyAlerts.Show("Download finished.", LegacyAlertDisplay.Hint,
            title: "Download", duration: 8f);
        LegacyAlerts.ShowLocalized("Operation complete", "Операция завершена",
            LegacyAlertDisplay.Small, title: "Result", titleRu: "Результат");
    }
}
```


`Show` 为通用方法，`Small`、`Large`、`Hint` 为简写。需要带标题的 hint 时使用 `Show` 或 `ShowLocalized`。模态窗口的空标题会替换为本地化的“通知”。

## 文本、本地化与限制

`ShowLocalized` 接收主要文本及可选的俄语版本。`messageRu` 必须显式传入字符串或 `null`，`titleRu` 可省略。俄语字段按当前游戏语言选择，消息打开期间切换语言也会更新。俄语正文/标题缺失或为空时，分别回退至对应的主要字段，不会自动翻译。

本地 API 接收普通 C# 字符串：`"\n"` 为换行，`"\t"` 为制表。`"\\n"` 包含反斜杠和字母 n，不会再次解码。此 API 不替换 `{nickname}`、`{reason}` 等服务器占位符，且禁用 rich text。

标题上限为 160 字符，正文为 8000 字符；两种语言分别按 `string.Length` 检查。`duration` 必须为 `0.1..3600` 秒内的有限数值，默认 `6f`。所有显示类型均检查该参数，但只有 Hint 使用它控制显示时长。类型、长度或时长无效时会同步抛出 `ArgumentException`。主要文本中的 `null` 转为空字符串。

允许从后台线程调用：API 验证参数并通过锁保护的队列传递消息，Unity UI 在主线程创建。传递队列最多保存 32 条消息，溢出时丢弃最旧的一条；客户端对话框队列也最多为 32 条。Hint 从实际显示时开始计时。此接口仅用于客户端，服务器不创建 UI。

## 与服务器 Alerts 的关系

本地调用不发送网络消息，也不读取 Alerts.toml。服务器模板、变量、`alert` 命令及 welcome 参见[服务器文档](../user-docs/server_ZH.md)。Welcome 的 `updatedAt` 和“不再显示”仅适用于服务器 welcome，不属于公开本地 API 调用。

源文件：[LegacyAlerts.cs](../../../src/BigCityLegacy/API/LegacyAlerts.cs)。[开发者文档](README.md)。
