using System;
using System.Collections.Generic;

namespace BigCityLegacy.Api
{
    public enum LegacyAlertDisplay { Small, Large, Hint }

    /// <summary>Client-side dialogs. Calls may be made from worker threads; Unity draws them on its main thread.</summary>
    public static class LegacyAlerts
    {
        private static readonly Queue<LegacyAlertMessage> pending = new Queue<LegacyAlertMessage>();
        public static void Show(string message, LegacyAlertDisplay display = LegacyAlertDisplay.Small, string title = "", float duration = 6f)
        {
            var alert = new LegacyAlertMessage { Message = message ?? "", Title = title ?? "", Display = display, Duration = duration };
            Enqueue(alert);
        }
        public static void ShowLocalized(string message, string messageRu, LegacyAlertDisplay display = LegacyAlertDisplay.Small, string title = "", string titleRu = null, float duration = 6f)
        { Enqueue(new LegacyAlertMessage { Message = message ?? "", MessageRu = messageRu, Title = title ?? "", TitleRu = titleRu, Display = display, Duration = duration }); }
        internal static void Enqueue(LegacyAlertMessage alert)
        {
            alert.Validate();
            lock (pending) { if (pending.Count >= 32) pending.Dequeue(); pending.Enqueue(alert); }
        }
        public static void Small(string message, string title = "") { Show(message, LegacyAlertDisplay.Small, title); }
        public static void Large(string message, string title = "") { Show(message, LegacyAlertDisplay.Large, title); }
        public static void Hint(string message, float duration = 6f) { Show(message, LegacyAlertDisplay.Hint, "", duration); }
        internal static LegacyAlertMessage Take() { lock (pending) return pending.Count == 0 ? null : pending.Dequeue(); }
    }
}

internal sealed class LegacyAlertMessage
{
    internal string Title = "", Message = "", TitleRu, MessageRu;
    internal bool IsWelcome;
    internal long UpdatedAt;
    internal string ServerKey;
    internal string Text(bool russian) { return russian && !string.IsNullOrEmpty(MessageRu) ? MessageRu : Message; }
    internal string Caption(bool russian) { return russian && !string.IsNullOrEmpty(TitleRu) ? TitleRu : Title; }
    internal BigCityLegacy.Api.LegacyAlertDisplay Display;
    internal float Duration = 6f;
    internal void Validate()
    {
        if (Title == null || Title.Length > 160 || Message == null || Message.Length > 8000 ||
            (TitleRu ?? "").Length > 160 || (MessageRu ?? "").Length > 8000 || UpdatedAt < 0 || UpdatedAt > 253402300799L)
            throw new ArgumentException("Alert title/body limit is 160/8000 characters.");
        if (!Enum.IsDefined(typeof(BigCityLegacy.Api.LegacyAlertDisplay), Display) || float.IsNaN(Duration) ||
            float.IsInfinity(Duration) || Duration < .1f || Duration > 3600f) throw new ArgumentException("Invalid alert display or duration (0.1..3600 seconds).");
    }
}
