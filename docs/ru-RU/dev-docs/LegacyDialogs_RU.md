# BigCityLegacy.Api.LegacyDialogs и LegacyGame

Клиентские хелперы BigCityLegacy 1.4.2 для MadOut2 9.4. Нужны ссылка на `BigCityLegacy.dll`, пространство имён `BigCityLegacy.Api` и BepInEx-зависимость от `com.bigcitylegacy.baseplugin` версии 1.4.2 или выше.

## Диалог с результатом

`Task<bool> LegacyDialogs.ShowAsync(LegacyDialogOptions options)` ставит диалог в очередь и возвращает `true` после Yes, `false` после No, закрытия крестиком или отключения UI. Результат выдаётся после завершения анимации закрытия. `void LegacyDialogs.Show(LegacyDialogOptions options, Action<bool> completed)` — вариант с callback в главном потоке Unity.

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

`LegacyDialogOptions` содержит публичные поля:

| Поле | Назначение |
| --- | --- |
| `Title`, `Message` | Заголовок и текст; по умолчанию пустые строки. |
| `NoBtnText`, `YesBtnText` | Подписи; `null` выбирает локализованные No/Yes. |
| `NoBtnType`, `YesBtnType` | `LegacyDialogButtonType.Normal`, `.Green` или `.Danger`; по умолчанию Normal. |
| `TitleRu`, `MessageRu`, `NoBtnTextRu`, `YesBtnTextRu` | Необязательные русские варианты по языку игры. Пустое поле заменяется основным. |

Текст отображается в HintBox; большой объём получает `LegacyUIScrollView`. Rich text отключён. `\n` и `\t` передавайте как обычные C# escape-последовательности; серверные подстановки `{nickname}` здесь не выполняются. Ограничения отдельно для каждого языка: заголовок 160, сообщение 8000, подпись кнопки 80 символов (`string.Length`). Неверные значения и типы кнопок вызывают `ArgumentException`; `null` options/callback — `ArgumentNullException`.

Параметры копируются при постановке, последующее изменение объекта options не меняет диалог. Методы можно вызвать из фонового потока; UI работает в главном. Очередь до 32 ожидающих диалогов; переполнение вызывает `InvalidOperationException`. Используйте `await`, не `.Result`/`.Wait()` в главном потоке. Это клиентский API; сервер UI не создаёт.

## Ввод и курсор

Все диалоги и [Alerts](LegacyAlerts_RU.md), включая hint, используют `LegacyUIInputBlockMode.Window`: блокируется ввод в области окна, а не весь экран. При показе включается игровой курсор. Он остаётся видимым до закрытия последнего такого окна, включая fade; если до их открытия был скрыт, восстанавливается скрытое состояние с учётом других штатных меню. Несколько одновременно показанных Alerts и Dialogs не отнимают курсор друг у друга.

## Перезапуск игры

`Task<bool> LegacyGame.RestartAsync(bool confirm = true)` предлагает перезагрузить игру для применения изменений. `confirm: false` пропускает диалог. `LegacyGame.IsRestarting` показывает принятую текущую попытку перезапуска.

```csharp
bool accepted = await LegacyGame.RestartAsync(); // Ask for confirmation.
bool acceptedQuietly = await LegacyGame.RestartAsync(confirm: false);
bool restarting = LegacyGame.IsRestarting;
```

`false` означает отказ от диалога. `true` означает, что помощник запущен и старому процессу отправлен запрос выхода; это не подтверждение успешного запуска нового процесса. Ошибка подготовки завершает Task исключением. Отказ игры завершаться отменяет ожидающий помощник и выводит Alert. Повторный параллельный перезапуск запрещён.

Хелпер предназначен для standalone-клиента, не для dedicated-сервера или Unity Editor. Он сохраняет исполняемый файл, рабочую папку и аргументы запуска; внешний помощник ждёт завершения старого процесса. На Windows сохраняется окружение загрузчика, сбрасываются служебные флаги Doorstop и после выхода удаляется только маркер `maybe_bad_settings`, чтобы штатная проверка аварийного запуска не сбрасывала графику. На Linux при наличии используется `run_bepinex.sh`; без него запускается исходный исполняемый файл. Профиль игрока не импортируется и не заменяется. Плагин отвечает за сохранение собственных настроек до вызова хелпера.

API активации плагинов сам вызывает один тихий перезапуск после согласия пользователя на всю группу изменений. Для обычного меню настройки можно непосредственно вызвать `await LegacyGame.RestartAsync()`.

[Активация плагинов](LegacyPluginActivation_RU.md) · [Разработчикам](README.md)
