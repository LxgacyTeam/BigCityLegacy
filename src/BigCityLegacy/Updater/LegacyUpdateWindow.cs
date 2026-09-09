using BigCityLegacy.UI;
using UnityEngine;

internal sealed class LegacyUpdateWindow : MonoBehaviour
{
    private static LegacyUpdateWindow instance;

    private LegacyUIWindow window;
    private LegacyUpdateInfo update;

    internal static void Show(LegacyUpdateInfo update)
    {
        if (update == null)
        {
            return;
        }

        EnsureInstance();
        instance.update = update;
        instance.window.Visible = true;
        instance.enabled = true;
    }

    private static void EnsureInstance()
    {
        if (instance)
        {
            return;
        }

        GameObject go = new GameObject("BigCityLegacy_UpdateWindow");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LegacyUpdateWindow>();
    }

    private void Awake()
    {
        instance = this;
        window = new LegacyUIWindow(
            LegacyLocalizer.Text("BigCityLegacy update", "Обновление BigCityLegacy"),
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
                BackgroundAlpha = 0.9f,
                InputBlockMode = LegacyUIInputBlockMode.Window
            });

        window.Visible = false;
        window.Closed += () => enabled = false;
        enabled = false;
    }

    private void OnGUI()
    {
        if (window == null || !window.Visible || update == null)
        {
            return;
        }

        using (new LegacyUIGuiScope(-22000))
        {
            window.Draw(DrawContent);
        }
    }

    private void DrawContent(Rect content)
    {
        float x = content.x;
        float y = content.y;
        float w = content.width;

        GUIStyle updTitle = new GUIStyle(LegacyUI.Styles.Title);
        updTitle.fontStyle = FontStyle.Normal;

        GUI.Label(new Rect(x, y, w, 24f), LegacyLocalizer.Text("New version available", "Доступна новая версия"), updTitle);
        y += 32f;

        string versionLine = LegacyLocalizer.Text("<b>Current version:</b>", "<b>Текущая версия:</b>") + " " + update.CurrentVersion + "\n" +
                             LegacyLocalizer.Text("<b>Latest release:</b>", "<b>Последний релиз:</b>") + " " + update.LatestVersion;
        LegacyUI.HintBox(new Rect(x, y, w, 56f), versionLine);

        float btnY = content.yMax - 30f;
        float gap = 8f;
        float btnW = (w - gap * 2f) / 3f;

        if (LegacyUI.GreenButton(new Rect(x, btnY, btnW, 28f), LegacyLocalizer.Text("Download", "Скачать")))
        {
            Application.OpenURL(update.DownloadUrl);
        }

        if (LegacyUI.Button(new Rect(x + btnW + gap, btnY, btnW, 28f), LegacyLocalizer.Text("Changelog", "Изменения")))
        {
            Application.OpenURL(update.ReleaseUrl);
        }

        if (LegacyUI.Button(new Rect(x + (btnW + gap) * 2f, btnY, btnW, 28f), LegacyLocalizer.Text("Later", "Позже")))
        {
            Close();
        }
    }

    private void Close()
    {
        if (window != null)
        {
            window.Visible = false;
        }
        enabled = false;
    }

    private static Rect GetStartRect()
    {
        const float width = 460f;
        const float height = 190f;
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return new Rect(180f, 120f, width, height);
        }
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }
}
