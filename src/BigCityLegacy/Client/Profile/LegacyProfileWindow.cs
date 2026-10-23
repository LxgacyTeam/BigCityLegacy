using System;
using System.IO;
using System.Linq;
using BigCityLegacy.UI;
using UnityEngine;

internal sealed class LegacyProfileWindow : MonoBehaviour
{
    private static LegacyProfileWindow instance;
    private LegacyUIWindow window;
    private string path = "", exportPassword = "", importPassword = "", status = "";
    private string slashInPath = Application.platform == RuntimePlatform.WindowsPlayer ? "\\" : "/";
    private string[] files = new string[0];
    private bool encrypt, busy, pendingEncrypted;
    private LegacyUIScrollView scroll;
    private string pendingPath;
    private float windowW = 420f;
    private float windowH = 420f;
    private string profilesPath = Path.Combine(LegacyHelpers.ModDataPath, "profiles");

    internal static void Show()
    {
        if (!instance)
        {
            var go = new GameObject("BigCityLegacy_PlayerProfile"); DontDestroyOnLoad(go);
            instance = go.AddComponent<LegacyProfileWindow>();
        }
        instance.Refresh(); instance.window.Visible = true; instance.enabled = true;
    }
    private void Awake()
    {
        if (!Directory.Exists(profilesPath))
        {
            Directory.CreateDirectory(profilesPath);
        }

        scroll = new LegacyUIScrollView(new Rect(0f, 0f, 100f, 100f))
        {
            Padding = 4f,
            ShowHorizontalScrollbar = false,
            ShowVerticalScrollbar = true
        };
        window = new LegacyUIWindow(LegacyLocalizer.Text("Player profile", "Профиль игрока"),
            new Rect((Screen.width - windowW) * .5f, (Screen.height - windowH) * .5f, windowW, windowH),
            new LegacyUIWindowOptions { Draggable = true, ClampToScreen = true, ShowCloseButton = true,
                InputBlockMode = LegacyUIInputBlockMode.Screen, HeaderHeight = 36f, ScreenPadding = 8f, BackgroundAlpha = 0.9f });
        window.Closed += () => { enabled = false; exportPassword = ""; importPassword = ""; pendingPath = null; pendingEncrypted = false; };
        window.Visible = false; enabled = false;
    }
    private void OnGUI()
    {
        if (window == null || !window.Visible) return;
        window.Title = LegacyLocalizer.Text("Player profile", "Профиль игрока");
        using (new LegacyUIGuiScope(-21500)) window.Draw(Draw);
    }
    private void Refresh()
    {
        try { files = Directory.GetFiles(profilesPath, "*.profile").OrderByDescending(File.GetLastWriteTimeUtc).ToArray(); }
        catch (Exception ex) { status = ex.Message; }
    }
    private void Draw(Rect area)
    {
        float x = area.x, y = area.y, width = area.width;
        if (pendingPath != null)
        {
            LegacyUI.HintBox(new Rect(x, y, width, 95), LegacyLocalizer.Text(
                "Replace the current profile and restart the game?\nA backup will be saved.\n\nSelected: ",
                "Заменить текущий профиль и перезапустить игру?\nРезервная копия будет сохранена.\n\nВыбрано: ") + Path.GetFileName(pendingPath));
            float buttonsY = y + 106f;
            if (pendingEncrypted)
            {
                LegacyUI.Label(new Rect(x, y + 105, width, 22), LegacyLocalizer.Text("Password", "Пароль"));
                importPassword = GUI.PasswordField(new Rect(x, y + 131, width, 25), importPassword, '*', 1024, LegacyUI.Styles.TextField);
                buttonsY = y + 167f;
            }
            if (LegacyUI.DangerButton(new Rect(x, buttonsY, width * .5f - 4, 30), LegacyLocalizer.Text("Import and restart", "Импорт и перезапуск"))) ImportConfirmed();
            if (LegacyUI.Button(new Rect(x + width * .5f + 4, buttonsY, width * .5f - 4, 30), LegacyLocalizer.Text("Cancel", "Отмена")))
            { pendingPath = null; pendingEncrypted = false; importPassword = ""; status = ""; }
            LegacyUI.Status(new Rect(x, area.yMax - 15f, width, 15f), status);
            return;
        }
        LegacyUI.HintBox(new Rect(x, y, width, 45), LegacyLocalizer.Text(
            "Import replaces the current profile,\ncreates a backup and restarts the game.",
            "Импорт заменит текущий профиль,\nсоздаст резервную копию и перезапустит игру."));
        y += 51;
        bool previous = GUI.enabled;
        GUI.enabled = previous && !busy;
        encrypt = LegacyUI.Toggle(new Rect(x, y, width, 25), encrypt, LegacyLocalizer.Text("Encrypt exported profile", "Зашифровать экспортируемый профиль"));
        y += 30;
        if (encrypt)
        {
            LegacyUI.Label(new Rect(x, y, 105, 24), LegacyLocalizer.Text("Password", "Пароль"));
            exportPassword = GUI.PasswordField(new Rect(x + 110, y, width - 110, 25), exportPassword, '*', 1024, LegacyUI.Styles.TextField);
        }
        y += 32;
        if (LegacyUI.GreenButton(new Rect(x, y, width * .70f - 4, 27), LegacyLocalizer.Text("Export profile", "Экспорт профиля"))) Export();
        if (LegacyUI.Button(new Rect(x + width * .70f, y, width * .30f, 27), LegacyLocalizer.Text("Open folder", "Открыть папку"))) LegacyHelpers.OpenFolder(profilesPath);
        y += 38;
        LegacyUI.Separator(new Rect(x, y, width, 1f));
        y += 9;
        LegacyUI.Label(new Rect(x, y, width - 88, 22), LegacyLocalizer.Text($"Profiles in BigCityLegacy{slashInPath}profiles", $"Профили в BigCityLegacy{slashInPath}profiles"));
        if (LegacyUI.Button(new Rect(x + width - 88, y, 88, 22), LegacyLocalizer.Text("Refresh", "Обновить"))) Refresh();
        y += 32;
        scroll.ViewRect = new Rect(x, y, width, 108);
        scroll.Draw(content =>
        {
            for (int i = 0; i < files.Length; i++)
                if (LegacyUI.Button(new Rect(content.x, content.y + i * 26f, content.width, 24f), Path.GetFileName(files[i]))) path = files[i];
        });
        path = LegacyUI.TextField(new Rect(x, area.yMax - 50f, width - 102, 26), Path.GetFileName(path));
        if (LegacyUI.GreenButton(new Rect(x + width - 98, area.yMax - 50f, 98, 26), LegacyLocalizer.Text("Import", "Импорт"))) ConfirmImport();
        GUI.enabled = previous;
        LegacyUI.Status(new Rect(x, area.yMax - 15f, width, 15f), status);
    }
    private void Export()
    {
        try
        {
            if (encrypt && string.IsNullOrEmpty(exportPassword)) throw new InvalidOperationException(LegacyLocalizer.Text("Enter a password.", "Введите пароль."));
            LegacyProfileData data = LegacyProfilePrefs.Capture();
            string name = NetManagerTools.RemoveRitchText(data.Nickname ?? "Player");
            path = Path.Combine(profilesPath, LegacyProfileFiles.Filename(name));
            LegacyProfileFiles.Write(path, LegacyProfileCodec.Encode(data, encrypt ? exportPassword : null));
            status = LegacyLocalizer.Text("Exported: ", "Сохранено: ") + Path.GetFileName(path);
            Refresh(); LegacyHelpers.OpenFolder(profilesPath);
        }
        catch (Exception ex) { status = ex.Message; }
    }
    private void ConfirmImport()
    {
        try
        {
            string selected = Path.GetFullPath(Path.Combine(profilesPath, path));
            if (!string.Equals(Path.GetExtension(selected), ".profile", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Select a .profile file.");
            var file = new FileInfo(selected);
            if (!file.Exists) throw new FileNotFoundException("Player profile was not found.", selected);
            if (file.Length == 0 || file.Length > LegacyProfileCodec.MaxBytes) throw new InvalidDataException("Invalid player profile size.");
            using (var input = File.OpenRead(selected)) pendingEncrypted = LegacyProfileCodec.IsEncrypted(input);
            pendingPath = selected;
            importPassword = ""; status = "";
        }
        catch (Exception ex) { status = ex.Message; }
    }
    private void ImportConfirmed()
    {
        try
        {
            string password = pendingEncrypted ? importPassword : "";
            LegacyProfileData data = LegacyProfileFiles.Read(pendingPath, password);
            if (data.Company != Application.companyName || data.Product != Application.productName) throw new InvalidDataException("The profile belongs to another game.");
            pendingPath = null; pendingEncrypted = false; importPassword = "";
            busy = true; status = LegacyLocalizer.Text("Importing profile. Restarting...", "Импорт профиля. Перезапуск...");
            StartCoroutine(LegacyProfileRestart.Import(data, password, error => { busy = false; status = error; }));
        }
        catch (Exception ex) { busy = false; status = ex.Message; }
    }
}
