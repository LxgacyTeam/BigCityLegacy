using System;
using BigCityLegacy.UI;
using UnityEngine;

internal sealed class LegacyAboutWindow : MonoBehaviour
{
    private static LegacyAboutWindow instance;
    private LegacyUIWindow window;
    private LegacyUIDropdown repositoryDropdown;
    private string sourceError;

    internal static bool SuppressReporterGesture;

    private Texture2D logo = LegacyHelpers.LoadEmbeddedTexture("bcl-logo.png");

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
        repositoryDropdown = new LegacyUIDropdown(new[] { "GitHub", "Codeberg" }, (int)LegacyRepository.Current);
        repositoryDropdown.SelectionChanged += index =>
        {
            try { LegacyRepository.SetSource((LegacyRepositorySource)index); sourceError = null; }
            catch (Exception ex) { sourceError = ex.Message; repositoryDropdown.SelectedIndex = (int)LegacyRepository.Current; }
        };
        window = new LegacyUIWindow(
            LegacyLocalizer.Text("About", "О моде"),
            GetStartRect(),
            new LegacyUIWindowOptions
            {
                BackdropBlur = true,
                BlurTintAlpha = 0.75f,
                Draggable = true,
                ClampToScreen = true,
                ShowCloseButton = true,
                FadeWhileDragging = true,
                DragAlpha = 0.68f,
                HeaderHeight = 38f,
                ScreenPadding = 8f,
                InputBlockMode = LegacyUIInputBlockMode.Screen
            });

        window.Visible = false;
        window.Closed += () =>
        {
            SuppressReporterGesture = false;
            repositoryDropdown.IsOpen = false;
        };
        enabled = false;
    }

    private void OnGUI()
    {
        if (window == null || !window.IsRendered)
        {
            enabled = false;
            return;
        }

        using (LegacyClientUI.Scope(-21000))
        {
            repositoryDropdown.HandlePopupInput();
            window.Draw(DrawContent);
            repositoryDropdown.DrawPopup();
        }
    }

    private void DrawContent(Rect content)
    {
        float x = content.x;
        float y = content.y;
        float w = content.width;

        GUIStyle nameTitle = new GUIStyle(LegacyUI.Styles.Title);
        nameTitle.fontSize = 24;

        GUI.Label(new Rect(x, y, w, 24f), LegacyLocalizer.Text("BigCityLegacy", "BigCityLegacy"), nameTitle);

        float logoW = 62.5f;
        if (logo != null)
        {
            GUI.DrawTexture(new Rect(x + w - logoW, y - 2f, logoW, 52.5f), logo);
        }

        y += 25f;

        GUIStyle author = new GUIStyle(LegacyUI.Styles.Title);
        author.fontStyle = FontStyle.Normal;

        GUI.Label(new Rect(x, y, w, 24f), LegacyLocalizer.Text("by LegacyTeam", "by LegacyTeam"), author);
        y += 32f;

        string versionsLine = LegacyLocalizer.Text("<b>Mod version:</b>", "<b>Версия мода:</b>") + $" {VersionInfo.ModVersionName}\n" +
                              LegacyLocalizer.Text("<b>Game version:</b>", "<b>Версия игры:</b>") + $" {Application.version} (build {LegacyHelpers.GetBuildVersion})\n" +
                              LegacyLocalizer.Text("<b>UI Framework version:</b>", "<b>Версия UI Framework:</b>") + $" {LegacyUI.Version}";

        #if DEBUG
            versionsLine += $"\n<b>Debug Build Timestamp:</b> {VersionInfo.BuildTimestamp}";
        #endif

        LegacyUI.HintBox(new Rect(x, y, w, 72f), versionsLine);
        y += 85f;

        LegacyUI.Separator(new Rect(x, y, w, 1f));
        y += 15f;

        bool checkUpdates = LegacyUI.Toggle(new Rect(x, y, content.width, 20f), LegacyUpdateChecker.IsEnabled,
                            LegacyLocalizer.Text("Check updates on startup", "Проверять наличие обновлений при запуске"));

        if (checkUpdates != LegacyUpdateChecker.IsEnabled)
        {
            LegacyUpdateChecker.IsEnabled = checkUpdates;
        }
        y += 28f;
        LegacyUI.Label(new Rect(x, y, w, 20f), LegacyLocalizer.Text("Repository source", "Источник данных"));
        y += 23f;
        repositoryDropdown.SelectedIndex = (int)LegacyRepository.Current;
        repositoryDropdown.Draw(new Rect(x, y, w, 28f));
        if (!string.IsNullOrEmpty(sourceError)) LegacyUI.Status(new Rect(x, y + 31f, w, 28f), sourceError);

        float btnY = content.yMax - 30f;
        float gap = 8f;
        float chkBtnW = 157f;
        float lnkBtnW = 78f;
        float btnH = 28f;

        if (LegacyUI.GreenButton(new Rect(x, btnY, chkBtnW, btnH), LegacyLocalizer.Text("Check updates", "Проверить обновления")))
        {
            LegacyUpdateChecker.CheckNow(this);
        }

        if (LegacyUI.Button(new Rect(x + chkBtnW + gap, btnY, lnkBtnW, btnH), LegacyRepository.Name))
        {
            Application.OpenURL(LegacyRepository.WebUrl(LegacyRepository.ModRepo));
        }

        if (LegacyUI.Button(new Rect(x + chkBtnW + lnkBtnW + gap * 2, btnY, lnkBtnW, btnH), "Telegram"))
        {
            Application.OpenURL($"https://t.me/{LegacyRepository.ModRepo}");
        }
    }

    private static Rect GetStartRect()
    {
        const float width = 360f;
        const float height = 340f;
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return new Rect(180f, 120f, width, height);
        }
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }
}
