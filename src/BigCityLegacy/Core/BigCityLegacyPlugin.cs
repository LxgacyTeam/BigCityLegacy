using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BigCityLegacy.UI;

[BepInPlugin(PluginGuid, PluginName, VersionInfo.ModVersion)]
public sealed class BigCityLegacyPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.bigcitylegacy.baseplugin";
    public const string PluginName = "BigCityLegacy";
    public const int OnlineProtocolVersion = 95;
    public const bool IsPrerelease = false;

    internal static ManualLogSource Log;
    internal static Harmony Harmony;
    internal static BigCityLegacyPlugin Instance;

    private bool _serverProcess;

#if BCL_BOOTSTRAP_DIAGNOSTICS
    private void Awake()
    {
        LegacyBootstrapTrace.Mark("awake.enter", "bcl=" + VersionInfo.ModVersionName + " build=" + VersionInfo.BuildTimestamp);
        try
        {
            InitializePlugin();
            LegacyBootstrapTrace.Mark("awake.return");
        }
        catch (System.Exception ex)
        {
            LegacyBootstrapTrace.Failure("awake.uncaught", ex);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializePlugin()
#else
    private void Awake()
#endif
    {
        LegacyBootstrapTrace.Mark("logger.bind");
        Log = Logger;
        Instance = this;
        LegacyBootstrapTrace.Mark("ui.scale");
        ConfigureUiScale();

        // Validate before patching or letting native server startup use its defaults.
        LegacyBootstrapTrace.Mark("command-line.read");
        string[] rawArgs = LegacyCommandLine.GetRawProcessArgs();
        LegacyBootstrapTrace.Mark("server-log.begin");
        LegacyServerLog.Begin(rawArgs);
        LegacyBootstrapTrace.Mark("server-config.apply");
        try { LegacyServerLog.Configure(LegacyServerConfig.Apply(rawArgs)); }
        catch (System.Exception ex)
        {
            LegacyBootstrapTrace.Failure("server-config.failed", ex);
            LegacyServerLog.Configure(rawArgs, startupFailed: true);
            Logger.LogError("Server configuration prevents startup: " + ex);
            enabled = false;
            UnityEngine.Application.Quit();
            return;
        }
        LegacyBootstrapTrace.Mark("compatibility.detect");
        LegacyCompatibility.DetectFromRuntime();
        LegacyBootstrapTrace.Mark("compatibility.selected", "game=" + LegacyCompatibility.CurrentGameVersion + " mode=" + LegacyCompatibility.Mode);

        LegacyBootstrapTrace.Mark("harmony.create");
        Harmony = new Harmony(PluginGuid);
        try
        {
            LegacyBootstrapTrace.Mark("migration.init");
            LegacyMigrationEvents.Init();
            LegacyBootstrapTrace.Mark("repository.init");
            LegacyRepository.Init();

            if (LegacyCompatibility.IsFullMode)
            {
                LegacyBootstrapTrace.Mark("command-line.apply");
                LegacyCommandLine.UpdateFromArgs();
                LegacyBootstrapTrace.Mark("online-protocol.apply");
                LegacyOnlineProtocol.Apply();
                _serverProcess = LegacyCommandLine.HasServerArg();
                LegacyBootstrapTrace.Mark("process.role", _serverProcess ? "server" : "client");
                LegacyBootstrapTrace.Mark("cs-diagnostics.init");
                LegacyCsDiagnostics.Initialize();

                if (_serverProcess)
                {
                    LegacyBootstrapTrace.Mark("admission-config.init");
                    try { LegacyAdmissionConfig.Initialize(); }
                    catch (System.Exception ex)
                    {
                        LegacyBootstrapTrace.Failure("admission-config.failed", ex);
                        Logger.LogError("Admission configuration prevents server startup: " + ex);
                        enabled = false; UnityEngine.Application.Quit(); return;
                    }
                }
                LegacyBootstrapTrace.Mark("server-console.prepare");
                LegacyServerConsole.PrepareForServerMode();
                LegacyBootstrapTrace.Mark("server-shutdown.init");
                LegacyServerShutdown.Initialize();
                LegacyBootstrapTrace.Mark("performance-monitor.init");
                LegacyPerformanceMonitor.Ensure(gameObject);

                // Do not instantiate/register client-only systems on a dedicated server.
                if (!_serverProcess)
                {
                    LegacyBootstrapTrace.Mark("client-fingerprint.begin");
                    LegacyRuntimeStamp.Begin();
                    LegacyBootstrapTrace.Mark("client-ui.init");
                    LegacyClientUI.Initialize(gameObject);
                    LegacyBootstrapTrace.Mark("client-thread.init");
                    LegacyClientThread.Initialize();
                    LegacyBootstrapTrace.Mark("dialog-ui.add");
                    gameObject.AddComponent<LegacyDialogUI>();
                    LegacyBootstrapTrace.Mark("alert-ui.add");
                    gameObject.AddComponent<LegacyAlertUI>();
                    LegacyBootstrapTrace.Mark("remote-look.configure");
                    LegacyRemoteLook.Configure(Config);
                    LegacyBootstrapTrace.Mark("point-tool.init");
                    LegacyPointTool.InitIfNeeded();
                    LegacyBootstrapTrace.Mark("settings.register");
                    LegacyCustomSettings.Register();
                    LegacyBootstrapTrace.Mark("public-mirror.start");
                    LegacyPublicMirror.Start(this);
                }
            }

            LegacyBootstrapTrace.Mark("subscriptions.reset");
            LegacyHelpers.ResetSubsStatus();
            LegacyBootstrapTrace.Mark("harmony.patch");
            LegacyPatchManager.PatchForCurrentMode(Harmony, Logger);
            LegacyBootstrapTrace.Mark("updater.start");
            LegacyUpdateChecker.StartIfNeeded(this);
            LegacyBootstrapTrace.Mark("startup.ready");

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
            LegacyBootstrapTrace.Failure("startup.failed", ex);
            Logger.LogError(PluginName + " Harmony patching failed: " + ex);
        }
    }

#if BCL_BOOTSTRAP_DIAGNOSTICS
    [MethodImpl(MethodImplOptions.NoInlining)]
#endif
    private static void ConfigureUiScale()
    {
        LegacyUI.ScaleSettings.BaseScale = 1.1f;
        LegacyUI.ScaleSettings.MinScale = 0.8f;
    }

#if BCL_BOOTSTRAP_DIAGNOSTICS
    private void Start() => LegacyBootstrapTrace.Mark("unity.start");
#endif

    private void Update()
    {
        LegacyPluginNetwork.Tick();
        if (!_serverProcess) { LegacyWelcomeClient.Tick(); LegacyPlayerProperties.TickClient(); }
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
        LegacyBootstrapTrace.Mark("unity.destroy");
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
