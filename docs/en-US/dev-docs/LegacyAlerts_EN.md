# BigCityLegacy.Api.LegacyAlerts

BigCityLegacy 1.4.2 client API for LegacyUIFramework dialogs and notifications in MadOut2 9.4. Reference `BigCityLegacy.dll` and declare a BepInEx dependency on `com.bigcitylegacy.baseplugin` version 1.4.2 or newer. The types are in the `BigCityLegacy.Api` namespace.

## Public signatures

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


| Type | Display |
| --- | --- |
| `Small` | Draggable window with a HintBox. Long content gains scrolling; a message longer than 1000 characters in either language switches to Large. |
| `Large` | Draggable window with a ScrollView, suitable for rules and long text. |
| `Hint` | HintBoxAlt at the top of the screen, timed in seconds. BCL uses `ScaleMultiplier = 1.5f`. A new hint replaces the previous one. |

Small/Large windows show the cursor and block clicks into the game. An ordinary Hint does not capture input. Dialogs use LegacyUIFramework animations; HintBoxAlt also supports backgrounds and backdrop blur through BCL's visual settings. The public LegacyAlerts methods do not expose per-message style options.

## Plugin example

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


`Show` is the general method; `Small`, `Large` and `Hint` are shortcuts. Use `Show` or `ShowLocalized` for a hint with a title. An empty modal title is replaced by the localized “Notification”.

## Text, localization and limits

`ShowLocalized` takes primary text and an optional Russian variant. The `messageRu` argument must explicitly receive a string or `null`; `titleRu` can be omitted. Russian fields are selected by the current game language, including when the language changes while the message is open. Missing or empty Russian text/title falls back to its corresponding primary field. Text is not translated automatically.

The local API accepts normal C# strings: `"\n"` is a newline and `"\t"` is a tab. `"\\n"` contains a backslash and the letter n and is not decoded again. Server placeholders such as `{nickname}` and `{reason}` are not substituted here. Rich text is disabled.

Titles are limited to 160 characters and bodies to 8000, checked independently for both languages using `string.Length`. `duration` must be finite and within `0.1..3600` seconds; it defaults to `6f`. It is validated for every display type but controls the lifetime of Hint only. Invalid display, length or duration throws `ArgumentException` synchronously. Primary `null` strings become empty strings.

Calls may come from worker threads: the API validates and enqueues messages under a lock, and Unity UI is created on the main thread. The transfer queue holds up to 32 messages and discards the oldest on overflow. The client dialog queue is also limited to 32. Hint timing starts when displayed. This is a client interface; servers do not create UI.

## Server Alerts

A local call does not send a network message or read Alerts.toml. Server templates, variables, the `alert` command and welcome are covered in the [server documentation](../user-docs/server_EN.md). Welcome's `updatedAt` and “Don't show again” apply to server welcome messages, not to public local API calls.

Source: [LegacyAlerts.cs](../../../src/BigCityLegacy/API/LegacyAlerts.cs). [Developer documentation](README.md).
