using System;
using BigCityLegacy.UI;
using UnityEngine;

internal sealed class LegacyAboutWindow : MonoBehaviour
{
    private static LegacyAboutWindow instance;
    private LegacyUIWindow window;

    internal static bool SuppressReporterGesture;

    internal static void Show()
    {
        SuppressReporterGesture = true;
        EnsureInstance();
        instance.window.Visible = true;
        instance.enabled = true;
    }

    private static void EnsureInstance()
    {
        if (instance)
        {
            return;
        }

        GameObject go = new GameObject("BigCityLegacy_AboutWindow");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LegacyAboutWindow>();
    }

    private void Awake()
    {
        instance = this;
        window = new LegacyUIWindow(
            LegacyLocalizer.Text("About", "О моде"),
            GetStartRect(),
            new LegacyUIWindowOptions
            {
                Draggable = true,
                ClampToScreen = true,
                ShowCloseButton = true,
                FadeWhileDragging = true,
                DragAlpha = 0.68f,
                HeaderHeight = 38f,
                ScreenPadding = 8f,
                BackgroundAlpha = 0.98f,
                InputBlockMode = LegacyUIInputBlockMode.Screen
            });

        window.Visible = false;
        window.Closed += () =>
        {
            enabled = false;
            SuppressReporterGesture = false;
        };
        enabled = false;
    }

    private void OnGUI()
    {
        if (window == null || !window.Visible)
        {
            return;
        }

        using (new LegacyUIGuiScope(-21000))
        {
            window.Draw(DrawContent);
        }
    }

    private static bool isUpdaterEnabled = PlayerPrefs.GetInt("BigCityLegacy.Updater.Enabled") == 0 ? false : true;

    private void DrawContent(Rect content)
    {
        float x = content.x;
        float y = content.y;
        float w = content.width;

        GUIStyle nameTitle = new GUIStyle(LegacyUI.Styles.Title);
        nameTitle.fontSize = 24;

        GUI.Label(new Rect(x, y, w, 24f), LegacyLocalizer.Text("BigCityLegacy", "BigCityLegacy"), nameTitle);
        y += 25f;

        GUIStyle author = new GUIStyle(LegacyUI.Styles.Title);
        author.fontStyle = FontStyle.Normal;

        GUI.Label(new Rect(x, y, w, 24f), LegacyLocalizer.Text("by LegacyTeam", "by LegacyTeam"), author);
        y += 32f;

        string versionsLine = LegacyLocalizer.Text("<b>Mod version:</b>", "<b>Версия мода:</b>") + $" {VersionInfo.ModVersionName}\n" +
                              LegacyLocalizer.Text("<b>Game version:</b>", "<b>Версия игры:</b>") + $" {Application.version} (build {LegacyHelpers.GetBuildVersion()})\n" +
                              LegacyLocalizer.Text("<b>UI Framework version:</b>", "<b>Версия UI Framework:</b>") + $" {LegacyUI.Version}";

        #if DEBUG
            versionsLine += $"\n<b>Debug Build Timestamp:</b> {VersionInfo.BuildTimestamp}";
        #endif

        LegacyUI.HintBox(new Rect(x, y, w, 72f), versionsLine);
        y += 80f;

        bool checkUpdates = LegacyUI.Toggle(new Rect(x, y, content.width, 20f), isUpdaterEnabled,
                            LegacyLocalizer.Text("Check updates on startup", "Проверять наличие обновлений при запуске"));

        if (checkUpdates != isUpdaterEnabled)
        {
            isUpdaterEnabled = checkUpdates;
            PlayerPrefs.SetInt("BigCityLegacy.Updater.Enabled", isUpdaterEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        float btnY = content.yMax - 30f;
        float gap = 8f;
        float chkBtnW = 155f;

        if (LegacyUI.GreenButton(new Rect(x, btnY, chkBtnW, 28f), LegacyLocalizer.Text("Check updates", "Проверить обновления")))
        {
            LegacyUpdateChecker.CheckNow(this);
        }

        if (LegacyUI.Button(new Rect(x + chkBtnW + gap, btnY, 75f, 28f), LegacyLocalizer.Text("GitHub", "GitHub")))
        {
            Application.OpenURL($"https://github.com/{BigCityLegacyPlugin.GitHubOwner}/{BigCityLegacyPlugin.GitHubRepo}");
        }
    }

    private static Rect GetStartRect()
    {
        const float width = 360f;
        const float height = 260f;
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return new Rect(180f, 120f, width, height);
        }
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }
}
