using Unity;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyPlayerHealth
{
    private static float nextSync;
    internal static float Sanitize(float value, float current)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) value = float.IsNaN(current) || float.IsInfinity(current) ? 0f : current;
        return Mathf.Clamp(value, 0f, 100f);
    }
    internal static void Tick()
    {
        if (!NetworkServer.active || Time.realtimeSinceStartup < nextSync) return;
        nextSync = Time.realtimeSinceStartup + 1f;
        for (int i = 0; i < NetManager.curCount; i++)
        {
            NetInputControl input = NetManager.netControlsByIndex[i];
            if (!input || !input.isConnected || input.connect2player == null || !input.input || !input.input.currentPlayer) continue;
            PlayerControl player = input.input.currentPlayer;
            if (!player.net_control) continue;
            float value = Sanitize(player.live, 0f);
            if (value != player.live) player.SetLive(value, false, default(WhoKill));
            // Reuse the stock health RPC, addressed only to its owner. It changes
            // neither respawn state nor position/inventory and also works on vanilla.
            var writer = new NetworkWriter();
            writer.StartMessage(2);
            writer.WritePackedUInt32(unchecked((uint)NetControl.kRpcRpc_SetLive));
            writer.Write(player.net_control.netId); writer.Write(value); writer.Write(false);
            GeneratedNetworkCode._WriteNetWersion_WhoKill(writer, default(WhoKill.NetWersion));
            writer.FinishMessage();
            input.connect2player.SendWriter(writer, 0);
        }
    }
}
