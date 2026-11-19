using UnityEngine;

internal static class LegacyCameraLookSync
{
    private static PlayerControl sender;
    private static float nextSend;
    private static Quaternion lastSent;

    internal static bool OwnsCamera(PlayerControl player)
    {
        return LegacyRemoteLook.Enabled && NetManager.isOnlineClient && !NetManager.isServer && player &&
            player.net_control && player.net_control.hasAuthority && !player.isUsedInMenu &&
            player.input && player.input.type == InputControl.Type.User;
    }

    internal static void Publish()
    {
        var input = InputControl.GetFirstUser();
        var player = input ? input.currentPlayer : null;
        if (!OwnsCamera(player) || !Cam.me) { sender = null; return; }
        Quaternion direction = Cam.me.GetCamDir();
        if (!LegacyLookPose.ValidRotation(direction)) return;
        player.cur_cam_dir = direction;
        float now = Time.unscaledTime;
        if (sender != player) { sender = player; nextSend = 0; lastSent = Quaternion.identity; }
        if (now < nextSend) return;
        nextSend = now + 1f / 30;
        if (Quaternion.Angle(lastSent, direction) < 0.5f && now - lastSendTime < 0.5f) return;
        lastSent = direction; lastSendTime = now;
        player.net_control.net_camDir = direction;
        player.net_control.CallCmd_CamDir(NetCompress.Rotation.Compress(direction));
    }

    private static float lastSendTime;
}
