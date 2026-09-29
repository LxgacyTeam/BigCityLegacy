using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGuid, PluginName, VersionInfo.ModVersion)]
public sealed class BigCityLegacyPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.bigcitylegacy.baseplugin";
    public const string PluginName = "BigCityLegacy";

    public const bool IsPrerelease = true;
    public const string GitHubOwner = "LxgacyTeam";
    public const string GitHubRepo = "BigCityLegacy";

    
    internal static ManualLogSource Log;
    internal static Harmony Harmony;

    private bool _serverProcess;

    private void Awake()
    {
        Log = Logger;
        LegacyCompatibility.DetectFromRuntime();

        Harmony = new Harmony(PluginGuid);
        try
        {
            LegacyMigrationEvents.Init();

            if (LegacyCompatibility.IsFullMode)
            {
                LegacyCommandLine.UpdateFromArgs();
                _serverProcess = LegacyCommandLine.HasServerArg();

                LegacyServerConsole.PrepareForServerMode();
                LegacyServerShutdown.Initialize();
                LegacyPerformanceMonitor.Ensure(gameObject);

                // Do not instantiate/register client-only systems on a dedicated server.
                if (!_serverProcess)
                {
                    LegacyPointTool.InitIfNeeded();
                    LegacyCustomSettings.Register();
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
        if (_serverProcess)
        {
            LegacyServerShutdown.Tick();
            LegacyChatEventQueue.Tick();
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
    }
}
