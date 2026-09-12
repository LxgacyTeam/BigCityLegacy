using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public static class LegacyHelpers
{
    public static string GameRoot
    {
        get
        {
            string root = Directory.GetCurrentDirectory();
            try
            {
                if (!string.IsNullOrEmpty(Application.dataPath))
                {
                    DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                    if (parent != null)
                    {
                        root = parent.FullName;
                    }
                }
            }
            catch { }
            return root;
        }
    }

    public static void OpenFolder(string folderPath)
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            folderPath = folderPath.Replace("/", "\\");
        }

        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"[OpenFolder] Path not exists: {folderPath}");
            return;
        }

        try
        {
            OpenFolderNatively(folderPath);
        }
        catch
        {
            try
            {
                Application.OpenURL("file://" + folderPath);
            }
            catch (Exception fallbackEx)
            {
                Debug.LogError($"[OpenFolder] Failed to open folder: {fallbackEx.Message}");
            }
        }
    }

    private static void OpenFolderNatively(string folderPath)
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            Process.Start("explorer.exe", folderPath);
        }
        else if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            Process.Start("open", folderPath);
        }
        else if (Application.platform == RuntimePlatform.LinuxPlayer)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{folderPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
    }

    internal static string VersionLine
    {
        get { return $"BigCityLegacy v{VersionInfo.ModVersionName} | Game ver: {Application.version}"; }
    }

    public static void SafeInvoke(object instance, string methodName)
    {
        if (instance is Type)
        {
            SafeInvoke((Type)instance, null, methodName);
            return;
        }
        SafeInvoke(instance.GetType(), instance, methodName);
    }

    public static void SafeInvoke(Type type, object instance, string methodName)
    {
        try
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(instance, null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] SafeInvoke failed: " + methodName + " -> " + ex.Message);
        }
    }

    internal static void EnsureServerNetChat(NetManager manager)
    {
        if (!manager || !NetManager.isServer || !NetworkServer.active)
        {
            return;
        }
        if (NetChat.me)
        {
            return;
        }
        if (!manager.chat)
        {
            Debug.LogError("Cannot spawn server NetChat: NetManager.chat is null");
            return;
        }
        GameObject go = manager.chat.InstanciateSafe();
        if (!go)
        {
            Debug.LogError("Cannot spawn server NetChat: InstanciateSafe returned null");
            return;
        }
        go.name = "NetChat_Server";
        go.SetActive(true);
        NetChat chat = go.GetComponent<NetChat>();
        if (!chat || !go.GetComponent<NetworkIdentity>())
        {
            Debug.LogError("Cannot spawn server NetChat: invalid prefab instance", go);
            UnityEngine.Object.Destroy(go);
            return;
        }
        NetChat.me = chat;
        try
        {
            NetworkServer.Spawn(go);
        }
        catch (Exception ex)
        {
            Debug.LogError("Cannot spawn server NetChat: " + ex.Message, go);
            if (NetChat.me == chat)
            {
                NetChat.me = null;
            }
            UnityEngine.Object.Destroy(go);
        }
    }

    internal static void EnsureServerUnetSettings(NetManager manager)
    {
        if (!manager || !NetManager.isServer || !NetworkServer.active)
        {
            return;
        }
        if (!manager.unet_settings)
        {
            return;
        }
        UNet_Settings existing = UnityEngine.Object.FindObjectOfType<UNet_Settings>();
        if (existing)
        {
            return;
        }
        GameObject go = UnityEngine.Object.Instantiate(manager.unet_settings.gameObject);
        go.name = "UNet_Settings_Server";
        NetworkServer.Spawn(go);
    }

    public static string GetJsonPath(string arg, string json)
    {
        string text = LegacyCommandLine.GetArgValue(arg);

        if (string.IsNullOrEmpty(text))
        {
            string text2 = LegacyHelpers.GameRoot;
            try
            {
                bool flag2 = !string.IsNullOrEmpty(Application.dataPath);
                if (flag2)
                {
                    DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                    bool flag3 = parent != null;
                    if (flag3)
                    {
                        text2 = parent.FullName;
                    }
                }
            }
            catch
            {
            }
            text = Path.Combine(Path.Combine(text2, "BigCityLegacy"), json);
        }

        return LegacyCommandLine.StripQuotes(text);
    }

    public static bool CheckFileExistence(string path)
    {
        if (File.Exists(path))
        {
            return true;
        }
        else
        {
            Debug.LogError($"Failed to load {path}: File not found");
            return false;
        }
    }

    public static bool IsGameplayRunning()
    {
        if (NetManager.isServer)
        {
            return false;
        }

        if (!Nuligine.me || !Nuligine.RealGame)
        {
            return false;
        }

        if (!GameUI.me || !GameUI.me.isUsed())
        {
            return false;
        }

        if (Loading.me)
        {
            return false;
        }

        if (Scenes.nowBusy())
        {
            return false;
        }

        if (FadeUI.me && FadeUI.fadeState != FadeUI.FadeState.Unfaded)
        {
            return false;
        }

        if (MenuEsc.me || MenuEsc.needSelectWhere)
        {
            return false;
        }

        if (GamePhone.me && GamePhone.me.gameObject.activeSelf)
        {
            return false;
        }

        InputControl input = InputControl.GetFirstUser();

        if (!input)
        {
            return false;
        }

        if (!input.current)
        {
            return false;
        }

        if (!input.currentOrParent)
        {
            return false;
        }

        return true;
    }

    public static bool IsCsOrSurvivalMatchRunning()
    {
        if (!NetManager.isOnlineClient)
        {
            return false;
        }

        if (IsLocalPlayerInActiveCsEvent(Net_BaseEvent.curUserInRaceInst))
        {
            return true;
        }

        return IsLocalPlayerInActiveCsEvent(Net_BaseEvent.isCurUserAddedToPlayersListG());
    }

    private static bool IsLocalPlayerInActiveCsEvent(Net_BaseEvent currentEvent)
    {
        Net_CS csEvent = currentEvent as Net_CS;
        if (!csEvent)
        {
            return false;
        }

        return csEvent.state == Net_BaseEvent.CurState.Spawn_AfterLobby ||
               csEvent.state == Net_BaseEvent.CurState.BeginRace ||
               csEvent.state == Net_BaseEvent.CurState.Race;
    }

    internal static void CheckOldPluginExist()
    {
        string PluginGuid = "com.alonso.madoutlegacy";

        if (Chainloader.PluginInfos.ContainsKey(PluginGuid))
        {
            string BiePath = "BepInEx/plugins";
            if (Application.platform == RuntimePlatform.WindowsPlayer)
                BiePath = BiePath.Replace("/", "\\");

            NativeErrorDialog.Show("BigCityLegacy Startup Error",
                                    "Outdated MadOutLegacy plugin detected!\n" +
                                    "Game launch blocked to prevent a version conflict.\n\n" +
                                    $"Please delete the \'MadOutLegacy\' folder from \'{BiePath}\'.");

            OpenFolder(GameRoot + "/" + BiePath);
            Application.Quit();
        }
    }

    public static int GetBuildVersion()
    {
        string text = AlwaysOnline.me.buildVersionAsset.text;
        AlwaysOnline.Verions buildVersionFromXML = AlwaysOnline.GetBuildVersionFromXML(text, false);
        int buildVersion = buildVersionFromXML.curVersion;
        return buildVersion;
    }
}

internal static class LegacyServerShutdown
{
    private static int requested;
    private static int quitIssued;
    private static string reason = string.Empty;
    private static bool initialized;

    internal static bool IsRequested
    {
        get { return Interlocked.CompareExchange(ref requested, 0, 0) != 0; }
    }

    internal static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Application.quitting += OnApplicationQuitting;
    }

    internal static void Request(string requestReason)
    {
        if (Interlocked.CompareExchange(ref requested, 1, 0) != 0)
        {
            return;
        }

        reason = string.IsNullOrEmpty(requestReason)
            ? "unspecified reason"
            : requestReason;
        LegacyServerConsole.SignalCommandReaderShutdown();
    }

    internal static void Tick()
    {
        if (!IsRequested || Interlocked.CompareExchange(ref quitIssued, 1, 0) != 0)
        {
            return;
        }

        if (NetManager.isServer)
        {
            LegacyServerConsole.WriteAdminLine(
                "[Status] Server shutdown requested: " +
                (string.IsNullOrEmpty(reason) ? "unspecified reason" : reason) +
                "."
            );
        }

        Application.Quit();
    }

    private static void OnApplicationQuitting()
    {
        LegacyServerConsole.SignalCommandReaderShutdown();
    }
}
