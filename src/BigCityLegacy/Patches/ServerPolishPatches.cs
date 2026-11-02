using HarmonyLib;

[HarmonyPatch]
internal static class ServerPolishPatches
{
    [HarmonyPatch(typeof(Net_EventStartManager), "StartEventIfNeed")]
    [HarmonyPrefix]
    private static bool Events() { return !NetManager.isServer || !UNet_Settings.me || UNet_Settings.me.lockMode != UNet_Settings.Mode.RP; }

    // This is reached once per accepted outgoing chat message (including server/system messages),
    // after the native duplicate filter. Muted/rejected text never reaches the log.
    [HarmonyPatch(typeof(NetChat), "CallRpc_NewMessage")]
    [HarmonyPrefix]
    private static void Chat(NetChat.ChatMessageStruct add) { LegacyChatLog.Record(add); }
}
