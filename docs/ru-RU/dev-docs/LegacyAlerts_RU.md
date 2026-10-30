# BigCityLegacy.Api.LegacyAlerts

Клиентский API BigCityLegacy 1.4.2 для диалогов и уведомлений на LegacyUIFramework в MadOut2 9.4. Добавьте ссылку на `BigCityLegacy.dll` и зависимость BepInEx от `com.bigcitylegacy.baseplugin` версии 1.4.2 или выше. Классы находятся в пространстве имён `BigCityLegacy.Api`.

## Публичные сигнатуры

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


| Тип | Отображение |
| --- | --- |
| `Small` | Перетаскиваемое окно с HintBox. Длинный текст автоматически получает прокрутку; сообщение длиннее 1000 символов в любой из языковых версий переводится в Large. |
| `Large` | Перетаскиваемое окно со ScrollView, подходящее для правил и длинных текстов. |
| `Hint` | HintBoxAlt сверху экрана; длительность задаётся в секундах. В BCL используется `ScaleMultiplier = 1.5f`. Новый hint заменяет предыдущий. |

Окна Small/Large показывают курсор и блокируют клики в игру. Обычный Hint не захватывает ввод. Закрываемые диалоги используют анимации LegacyUIFramework; HintBoxAlt также поддерживает фон и backdrop blur в рамках визуальных настроек BCL. Публичные методы LegacyAlerts не принимают отдельные настройки стиля.

## Пример плагина

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


`Show` — общий метод; `Small`, `Large` и `Hint` являются сокращениями. Для hint с заголовком используйте `Show` или `ShowLocalized`. У модального окна пустой заголовок заменяется локализованным «Уведомление».

## Текст, локализация и ограничения

`ShowLocalized` принимает основной текст и необязательный русский вариант. Для аргумента `messageRu` нужно явно передать строку или `null`; `titleRu` можно опустить. Русские поля выбираются по текущему языку игры, в том числе при смене языка уже открытого сообщения. Отсутствующий или пустой русский текст/заголовок заменяется соответствующим основным полем. Автоматического перевода нет.

Локальный API принимает обычные C#-строки: `"\n"` — перенос, `"\t"` — табуляция. Строка `"\\n"` содержит обратную косую черту и букву n и повторно не разбирается. Подстановки `{nickname}`, `{reason}` и прочие серверные шаблоны здесь не выполняются. Текст отображается без rich text.

Заголовок ограничен 160 символами, текст — 8000 символами; ограничения проверяются отдельно для обоих языков в единицах `string.Length`. `duration` должен быть конечным числом в диапазоне `0.1..3600` секунд, по умолчанию `6f`; он проверяется для всех типов, но задаёт срок показа только Hint. Недопустимый тип, длина или длительность синхронно вызывают `ArgumentException`. Основные `null`-строки заменяются пустыми.

Вызовы разрешены из фоновых потоков: API проверяет параметры и помещает сообщение в защищённую очередь, а UI создаётся в главном потоке Unity. Очередь передачи содержит до 32 сообщений; при переполнении удаляется самое старое. Клиентская очередь диалогов также ограничена 32. Длительность Hint отсчитывается при показе. Это клиентский интерфейс; сервер UI не создаёт.

## Связь с серверными Alerts

Локальный вызов не отправляет сообщение по сети и не читает Alerts.toml. Серверные шаблоны, переменные, команда `alert` и welcome описаны в [документации сервера](../user-docs/server_RU.md). Настройки welcome `updatedAt` и «Не показывать больше» относятся к серверному welcome, а не к публичным методам локального API.

Исходник: [LegacyAlerts.cs](../../../src/BigCityLegacy/API/LegacyAlerts.cs). [Документация разработчика](README.md).
