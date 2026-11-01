using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BigCityLegacy.Api
{
    public enum LegacyDialogButtonType { Normal, Green, Danger }
    public sealed class LegacyDialogOptions
    {
        public string Title = "", Message = "", NoBtnText, YesBtnText;
        public string TitleRu, MessageRu, NoBtnTextRu, YesBtnTextRu;
        public LegacyDialogButtonType NoBtnType = LegacyDialogButtonType.Normal, YesBtnType = LegacyDialogButtonType.Normal;
        internal LegacyDialogOptions Copy()
        {
            if ((Title ?? "").Length > 160 || (TitleRu ?? "").Length > 160 || (Message ?? "").Length > 8000 || (MessageRu ?? "").Length > 8000 ||
                (NoBtnText ?? "").Length > 80 || (YesBtnText ?? "").Length > 80 || (NoBtnTextRu ?? "").Length > 80 || (YesBtnTextRu ?? "").Length > 80 ||
                !Enum.IsDefined(typeof(LegacyDialogButtonType),NoBtnType) || !Enum.IsDefined(typeof(LegacyDialogButtonType),YesBtnType)) throw new ArgumentException("Invalid dialog text or button type.");
            return (LegacyDialogOptions)MemberwiseClone();
        }
    }
    public static class LegacyDialogs
    {
        private static readonly Queue<Request> queue = new Queue<Request>();
        internal sealed class Request
        {
            internal LegacyDialogOptions Options;
            internal readonly TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public static Task<bool> ShowAsync(LegacyDialogOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var request = new Request { Options=options.Copy() };
            lock(queue) { if(queue.Count>=32) throw new InvalidOperationException("Dialog queue is full.");queue.Enqueue(request); }
            return request.Completion.Task;
        }
        public static void Show(LegacyDialogOptions options, Action<bool> completed)
        {
            if(completed==null) throw new ArgumentNullException(nameof(completed));
            ShowAsync(options).ContinueWith(t => LegacyClientThread.Post(() => completed(t.Result)),TaskScheduler.Default);
        }
        internal static Request Take() { lock(queue) return queue.Count==0 ? null : queue.Dequeue(); }
        internal static void CancelPending() { lock(queue) while(queue.Count>0) queue.Dequeue().Completion.TrySetResult(false); }
    }
}
