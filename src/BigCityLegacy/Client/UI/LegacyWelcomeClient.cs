using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using BigCityLegacy.Api;
using UnityEngine;

internal static class LegacyWelcomeClient
{
    private static LegacyAlertMessage pending;
    private static int readyFrame = -1;
    private static string currentServer;
    internal static void Clear() { pending = null; readyFrame = -1; currentServer = null; }
    internal static void Receive(LegacyAlertMessage message, string address, int port)
    {
        string server = LegacyWelcomePreferences.Endpoint(address, port);
        if (server == null) return;
        currentServer = server; message.ServerKey = server;
        pending = LegacyWelcomePreferences.ShouldShow(server, message.UpdatedAt) ? message : null; readyFrame = -1;
    }
    internal static bool IsCurrent(LegacyAlertMessage message)
    { return message != null && message.ServerKey == currentServer && NetManager.isOnlineClient; }
    internal static bool WorldReady()
    {
        if (!NetManager.isOnlineClient || !Nuligine.me || !Nuligine.RealGame || !GameUI.me || Loading.me || Initializer.isBusy || Scenes.nowBusy()) return false;
        if (GroundLoader.me && !GroundLoader.isAllLoaded || MapProLoader.me && !MapProLoader.canControlsLoad()) return false;
        if (!FadeUI.me || FadeUI.fadeState != FadeUI.FadeState.Unfaded ||
            FadeUI.me.imgFade && FadeUI.me.imgFade.gameObject.activeInHierarchy && FadeUI.me.imgFade.color.a > .001f) return false;
        InputControl user = InputControl.GetFirstUser();
        return user && user.currentPlayer && user.netInput && user.netInput.spawn && !user.netInput.spawn.nowRespawn;
    }
    internal static void Tick()
    {
        if (pending == null) return;
        if (!NetManager.isOnlineClient) { Clear(); return; }
        if (!WorldReady()) { readyFrame = -1; return; }
        if (readyFrame < 0) { readyFrame = Time.frameCount; return; }
        if (Time.frameCount <= readyFrame) return;
        var message = pending; pending = null;
        if (LegacyWelcomePreferences.ShouldShow(message.ServerKey, message.UpdatedAt)) LegacyAlerts.Enqueue(message);
    }
}
