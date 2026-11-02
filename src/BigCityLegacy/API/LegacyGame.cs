using System;
using System.Threading.Tasks;

namespace BigCityLegacy.Api
{
    public static class LegacyGame
    {
        public static bool IsRestarting { get { return LegacyGameRestart.Running; } }
        public static async Task<bool> RestartAsync(bool confirm=true)
        {
            if(confirm && !await LegacyDialogs.ShowAsync(new LegacyDialogOptions {
                Title="Restart required",TitleRu="Требуется перезапуск",
                Message="Restart the game to apply changes?",MessageRu="Перезагрузить игру для применения изменений?",
                YesBtnText="Restart",YesBtnTextRu="Перезапустить",NoBtnText="Later",NoBtnTextRu="Позже",YesBtnType=LegacyDialogButtonType.Green }))return false;
            var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await LegacyClientThread.Invoke(()=> { LegacyGameRestart.Start(completion);return true; });
            return await completion.Task;
        }
    }
}
