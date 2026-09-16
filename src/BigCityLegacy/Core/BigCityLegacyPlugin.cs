using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGuid, PluginName, VersionInfo.ModVersion)]
public sealed class BigCityLegacyPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.bigcitylegacy.baseplugin";
    public const string PluginName = "BigCityLegacy";

    public const bool IsPrerelease = false;
    public const string GitHubOwner = "LxgacyTeam";
    public const string GitHubRepo = "BigCityLegacy";

    
    internal static ManualLogSource Log;
    internal static Harmony Harmony;

    private void Awake()
    {
        Log = Logger;
        LegacyCompatibility.DetectFromRuntime();

        Harmony = new Harmony(PluginGuid);
        try
        {

            if (LegacyCompatibility.IsFullMode)
            {
                LegacyCommandLine.UpdateFromArgs();
                LegacyServerConsole.PrepareForServerMode();
                LegacyServerShutdown.Initialize();
                LegacyPointTool.InitIfNeeded();
                LegacyCustomSettings.Register();
            }

            LegacyHelpers.CheckOldPluginExist();
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
        LegacyHotkeys.Update();
        LegacyServerShutdown.Tick();
        LegacyChatEventQueue.Tick();
        LegacyHudToggle.Tick();
        LegacyGarageVinylButton.Ensure();
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
