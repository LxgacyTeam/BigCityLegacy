# BigCityLegacy.Api.LegacyDialogs and LegacyGame

Client helpers in BigCityLegacy 1.4.2 for MadOut2 9.4. Reference `BigCityLegacy.dll`, use `BigCityLegacy.Api`, and declare a BepInEx dependency on `com.bigcitylegacy.baseplugin` version 1.4.2 or later.

## A dialog with a result

`Task<bool> LegacyDialogs.ShowAsync(LegacyDialogOptions options)` queues a dialog and returns `true` after Yes, or `false` after No, the close button or UI shutdown. The result completes after the closing animation. `void LegacyDialogs.Show(LegacyDialogOptions options, Action<bool> completed)` provides a callback on Unity's main thread.

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

`LegacyDialogOptions` has these public fields:

| Field | Purpose |
| --- | --- |
| `Title`, `Message` | Caption and body; empty by default. |
| `NoBtnText`, `YesBtnText` | Labels; `null` chooses localized No/Yes. |
| `NoBtnType`, `YesBtnType` | `LegacyDialogButtonType.Normal`, `.Green` or `.Danger`; default Normal. |
| `TitleRu`, `MessageRu`, `NoBtnTextRu`, `YesBtnTextRu` | Optional Russian variants, selected by game language. Empty fields fall back to the primary text. |

The body uses a HintBox, with `LegacyUIScrollView` for longer content. Rich text is disabled. Pass `\n` and `\t` as ordinary C# escape sequences; server substitutions such as `{nickname}` are not expanded here. Limits apply separately to each language: title 160, body 8000 and button label 80 characters (`string.Length`). Invalid values or button types throw `ArgumentException`; null options/callback throws `ArgumentNullException`.

Options are copied when queued; subsequent mutations do not change the dialog. Calls may originate on a worker thread; UI runs on the main thread. The queue holds up to 32 waiting dialogs and throws `InvalidOperationException` when full. Use `await`, not `.Result`/`.Wait()` on the main thread. This is a client API; the server does not create UI.

## Input and cursor

All dialogs and [Alerts](LegacyAlerts_EN.md), including hints, use `LegacyUIInputBlockMode.Window`: input is blocked inside their bounds, rather than across the screen. The game cursor is enabled while they are shown. It remains visible through closing fades and until the last such window closes. If previously hidden, its hidden state is restored while respecting other native menus. Concurrent Alerts and Dialogs do not release one another's cursor.

## Restarting the game

`Task<bool> LegacyGame.RestartAsync(bool confirm = true)` asks to restart to apply changes. `confirm: false` skips the dialog. `LegacyGame.IsRestarting` indicates a current accepted restart attempt.

```csharp
bool accepted = await LegacyGame.RestartAsync(); // Ask for confirmation.
bool acceptedQuietly = await LegacyGame.RestartAsync(confirm: false);
bool restarting = LegacyGame.IsRestarting;
```

`false` means the user declined. `true` means the helper started and the old process was asked to quit; it is not confirmation that the new process started successfully. Preparation failures fault the Task. If the game refuses to quit, the waiting helper is cancelled and an Alert appears. Concurrent restart attempts are rejected.

The helper is for standalone clients, not dedicated servers or Unity Editor. It preserves the executable, working directory and arguments, and waits externally for the old process to exit. On Windows it preserves the loader environment, clears Doorstop process guards and removes only the `maybe_bad_settings` marker after exit, avoiding the native crash-startup graphics reset. On Linux it uses `run_bepinex.sh` when available, otherwise the original executable. It does not import or replace the player profile. Plugins must save their own settings before requesting restart.

The plugin activation API arranges one quiet restart after the user approves the entire change batch. Ordinary settings menus can call `await LegacyGame.RestartAsync()` directly.

[Plugin activation](LegacyPluginActivation_EN.md) · [Developer documentation](README.md)
