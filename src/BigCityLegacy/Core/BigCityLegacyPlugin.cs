using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BigCityLegacy.UI;

[BepInPlugin(PluginGuid, PluginName, VersionInfo.ModVersion)]
public sealed class BigCityLegacyPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.bigcitylegacy.baseplugin";
    public const string PluginName = "BigCityLegacy";
    public const int OnlineProtocolVersion = 94;
    public const bool IsPrerelease = false;

    internal static ManualLogSource Log;
    internal static Harmony Harmony;
    internal static BigCityLegacyPlugin Instance;

    private bool _serverProcess;

    private void Awake()
    {
        Log = Logger;
        Instance = this;
        LegacyUI.ScaleSettings.BaseScale = 1.1f;
        LegacyUI.ScaleSettings.MinScale = 0.8f;

        // Validate before patching or letting native server startup use its defaults.
        string[] rawArgs = LegacyCommandLine.GetRawProcessArgs();
        LegacyServerLog.Begin(rawArgs);
        try { LegacyServerLog.Configure(LegacyServerConfig.Apply(rawArgs)); }
        catch (System.Exception ex)
        {
            LegacyServerLog.Configure(rawArgs, startupFailed: true);
            Logger.LogError("Server configuration prevents startup: " + ex);
            enabled = false;
            UnityEngine.Application.Quit();
            return;
        }
        LegacyCompatibility.DetectFromRuntime();
        LegacyClientUI.ConfigureDiagnostics(Config, Logger);

        Harmony = new Harmony(PluginGuid);
        try
        {
            LegacyMigrationEvents.Init();
            LegacyRepository.Init();

            if (LegacyCompatibility.IsFullMode)
            {
                LegacyCommandLine.UpdateFromArgs();
                LegacyOnlineProtocol.Apply();
                _serverProcess = LegacyCommandLine.HasServerArg();
                LegacyCsDiagnostics.Initialize();

                if (_serverProcess)
                {
                    try { LegacyAdmissionConfig.Initialize(); }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("Admission configuration prevents server startup: " + ex);
                        enabled = false; UnityEngine.Application.Quit(); return;
                    }
                }
                LegacyServerConsole.PrepareForServerMode();
                LegacyServerShutdown.Initialize();
                LegacyPerformanceMonitor.Ensure(gameObject);

                // Do not instantiate/register client-only systems on a dedicated server.
                if (!_serverProcess)
                {
                    LegacyClientUI.Initialize(gameObject);
                    gameObject.AddComponent<LegacyAlertUI>();
                    LegacyRemoteLook.Configure(Config);
                    LegacyPointTool.InitIfNeeded();
                    LegacyCustomSettings.Register();
                    LegacyPublicMirror.Start(this);
                }
            }

            LegacyHelpers.ResetSubsStatus();
            LegacyPatchManager.PatchForCurrentMode(Harmony, Logger);
            LegacyUpdateChecker.StartIfNeeded(this);

            if (LegacyCompatibility.IsFullMode)
            {
                Logger.LogInfo(PluginName + " " + VersionInfo.ModVersionName + " loaded in full mode for game version " + LegacyCompatibility.CurrentGameVersion);
            }
            else
            {
                Logger.LogWarning(PluginName + " " + VersionInfo.ModVersionName + " loaded in compatibility mode for game version " + LegacyCompatibility.CurrentGameVersion + ". Only offline patches are enabled.");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError(PluginName + " Harmony patching failed: " + ex);
        }
    }

    private void Update()
    {
        LegacyPluginNetwork.Tick();
        if (!_serverProcess) LegacyWelcomeClient.Tick();
        LegacyCsDiagnostics.Tick();
        if (_serverProcess)
        {
            LegacyServerShutdown.Tick();
            LegacyChatEventQueue.Tick();
            LegacyServerDiagnostics.Tick();
            return;
        }

        LegacyHotkeys.Update();
        LegacyHudToggle.Tick();
        LegacyGarageVinylButton.Ensure();
        LegacyTypingIndicator.Ensure();
    }

    private void OnDestroy()
    {
        if (Harmony != null)
        {
            Harmony.UnpatchSelf();
            Harmony = null;
        }
        LegacyClientUI.Shutdown();
        LegacyServerLog.Shutdown();
        LegacyCsDiagnostics.Shutdown();
    }
}
