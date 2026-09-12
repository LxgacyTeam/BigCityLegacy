using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class KillChatPatches
{
    [HarmonyPatch(typeof(PlayerControl), "LiveChanget")]
    [HarmonyPostfix]
    private static void PlayerControl_LiveChanget_Postfix(PlayerControl __instance, bool now_dead, WhoKill who)
    {
        if (!NetManager.isServer || !now_dead)
        {
            return;
        }

        NetInputControl victim = __instance.GetInputFromMinusLive() ? __instance.GetInputFromMinusLive().netInput : null;
        if (victim == null)
        {
            return;
        }

        Net_BaseEvent curEvent = victim.curEvent;
        if (curEvent == null || curEvent.state == Net_BaseEvent.CurState.Lobbi || curEvent.state == Net_BaseEvent.CurState.Complite)
        {
            return;
        }

        NetInputControl killer = who.netInputControl;
        string message;

        if (killer == null)
        {
            message = "died";
        }
        else if (killer == victim)
        {
            message = "killed themself";
        }
        else
        {
            message = "killed by " + killer.nikName;
        }

        ChatEventsPatches.SendChatMessage(victim, message);
    }
}