using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using BepInEx;
using BigCityLegacy.UI;
using UnityEngine;

public class LegacyVinylPort : MonoBehaviour
{
    private const float WinWidth = 430f;
    private const float WinHeight = 390f;
    private const float ListHeight = 220f;
    private const float PosXPercent = 0.156f;
    private const float PosYPercent = 0.15f;
    private const int MaxNameLength = 60;
    private const float PackRefreshInterval = 0.5f;

    private static LegacyVinylPort instance;
    public static LegacyVinylPort Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("LegacyVinylPort");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<LegacyVinylPort>();
            }
            return instance;
        }
    }

    private LegacyUIWindow win;
    private LegacyUIWindow warning;
    private LegacyVinylPack pendingPack;
    private string pendingPath;
    private CarControl pendingCar;
    private LegacyUIScrollView scroll;
    private List<PackInfo> choises = new List<PackInfo>();
    private string packName = "my_vinyl";
    private string curCar = "";

    private string flashMsg = "";
    private float flashTime;
    private float nextPackRefreshTime;

    private struct PackInfo
    {
        public string path;
        public string name;
    }

    private void Awake()
    {
        win = new LegacyUIWindow(
            "VinylPort",
            new Rect(
                Mathf.Round(Screen.width * PosXPercent),
                Mathf.Round(Screen.height * PosYPercent),
                WinWidth,
                WinHeight),
            new LegacyUIWindowOptions
            {
                Draggable = true,
                ClampToScreen = true,
                ShowCloseButton = true,
                InputBlockMode = LegacyUIInputBlockMode.Window
            });
        win.Visible = false;
        win.Closed += CancelPending;
        warning = new LegacyUIWindow("VinylPort", new Rect(0, 0, 550, 340),
            new LegacyUIWindowOptions { Draggable = true, ClampToScreen = true, ShowCloseButton = true,
                InputBlockMode = LegacyUIInputBlockMode.Screen, BackgroundAlpha = 0.98f });
        warning.Visible = false;
        warning.Closed += ClearPending;

        scroll = new LegacyUIScrollView(new Rect(0f, 0f, 100f, 100f));
        scroll.Padding = 8f;
        scroll.ShowHorizontalScrollbar = false;
        scroll.ShowVerticalScrollbar = true;
    }

    private void Update()
    {
        if (win.Visible && CarGarageUI.me == null)
        {
            Close();
            return;
        }

        if (!win.Visible)
            return;

        if (pendingPack != null && (!CarGarageUI.me || CarGarageUI.me.car != pendingCar))
            CancelPending();

        string garageCar = CurrentGarageCar();
        if (!string.Equals(garageCar, curCar, StringComparison.OrdinalIgnoreCase))
        {
            curCar = garageCar;
            RefreshPacks(true);
            return;
        }

        if (Time.unscaledTime >= nextPackRefreshTime)
            RefreshPacks(false);
    }

    public void Toggle()
    {
        if (win.Visible) Close();
        else Open();
    }

    public void Open()
    {
        curCar = CurrentGarageCar();
        RefreshPacks(true);
        win.Visible = true;
    }

    public void Close()
    {
        CancelPending();
        win?.Close();
    }

    private void OnDestroy() { Close(); if (instance == this) instance = null; }
    private void ClearPending() { pendingPack = null; pendingPath = null; pendingCar = null; }
    private void CancelPending() { warning?.Close(); ClearPending(); }

    private string CurrentGarageCar()
    {
        if (CarGarageUI.me && CarGarageUI.me.car)
            return CarGarageUI.me.car.prefabName;

        foreach (CarPaintItems p in Resources.FindObjectsOfTypeAll<CarPaintItems>())
        {
            if (p != null && p.gameObject.activeInHierarchy && p.car != null)
                return p.car.prefabName;
        }

        return "";
    }

    private void OnGUI()
    {
        if (win == null || !win.Visible)
            return;

        using (new LegacyUIGuiScope(-10000))
        {
            if (warning.Visible) warning.Draw(DrawWarning);
            else win.Draw(DrawContent);
        }
    }

    private string carDir
    {
        get
        {
            string carDir = Path.Combine(LegacyHelpers.ModDataPath, "vinylpacks", Sanitize(curCar));
            return carDir;
        }
    }

    private void DrawContent(Rect content)
    {
        LegacyUILayout ui = new LegacyUILayout(content);

        Rect labelRect = ui.Row(20f);
        Rect hintRect = ui.Row(18f);
        Rect listRect = ui.Row(ListHeight);
        Rect inputRow = ui.Row(24f);
        Rect statusRect = ui.Row(20f);

        LegacyLocalizedText carLabel = new LegacyLocalizedText("Car: ", "Машина: ");
        LegacyLocalizedText emptyCar = new LegacyLocalizedText("<not selected>", "<не выбрана>");

        LegacyUI.Label(labelRect, carLabel + (curCar == "" ? $"{emptyCar}" : curCar));

        float folderButtonWidth = 110f;
        Rect folderButtonRect = new Rect(
            labelRect.xMax - folderButtonWidth,
            labelRect.y,
            folderButtonWidth,
            25f);

        if (Directory.Exists(carDir))
        {
            if (LegacyUI.Button(folderButtonRect, LegacyLocalizer.Text("Open folder", "Открыть папку")))
                LegacyHelpers.OpenFolder(carDir);
        }

        LegacyLocalizedText numOfPacks = new LegacyLocalizedText("Packs: ", "Паков: ");

        LegacyUI.MiniHint(hintRect, $"{numOfPacks}" + choises.Count);

        float nameWidth = inputRow.width * 0.6f;
        Rect nameRect = new Rect(inputRow.x, inputRow.y, nameWidth - 4f, inputRow.height);

        string newPackName = LegacyUI.TextField(nameRect, packName);
        if (newPackName == null)
            newPackName = "";
        if (newPackName.Length > MaxNameLength)
            newPackName = newPackName.Substring(0, MaxNameLength);
        packName = newPackName;

        if (LegacyUI.GreenButton(
            new Rect(inputRow.x + nameWidth, inputRow.y, inputRow.width - nameWidth, inputRow.height),
            LegacyLocalizer.Text("Export", "Экспорт")))
        {
            Export();
        }

        LegacyUI.Status(statusRect, Time.unscaledTime < flashTime ? flashMsg : "");

        scroll.ViewRect = listRect;
        scroll.Draw(listContent =>
        {
            LegacyUILayout list = new LegacyUILayout(listContent, 5f);
            for (int i = 0; i < choises.Count; i++)
            {
                PackInfo info = choises[i];
                if (LegacyUI.Button(list.Row(26f), info.name))
                    ApplyPack(info.path);
            }

            if (choises.Count == 0)
                LegacyUI.MiniHint(list.Row(18f), LegacyLocalizer.Text("No packs for this car", "Нет паков для этой машины"));
        });
    }

    private void RefreshPacks(bool resetScroll)
    {
        choises = ScanPacksForCar(curCar);
        nextPackRefreshTime = Time.unscaledTime + PackRefreshInterval;

        if (resetScroll && scroll != null)
            scroll.ResetScroll();
    }

    private List<PackInfo> ScanPacksForCar(string car)
    {
        List<PackInfo> result = new List<PackInfo>();
        if (car == "")
            return result;

        string packsDir = Path.Combine(LegacyHelpers.ModDataPath, "vinylpacks");
        if (!Directory.Exists(packsDir))
            return result;

        string[] files;
        try
        {
            files = Directory.GetFiles(packsDir, "*.vinyl", SearchOption.AllDirectories);
        }
        catch
        {
            return result;
        }

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            try
            {
                if (new FileInfo(file).Length > LegacyVinylPackCodec.MaxCharacters * 4L) continue;
                XmlDocument pack = LegacyVinylPackCodec.ReadXml(File.ReadAllText(file));

                XmlElement root = pack.DocumentElement;
                if (root == null || root.Name != "VinylPack")
                    continue;

                string packCar = root.GetAttribute("car");
                if (!string.Equals(packCar, car, StringComparison.OrdinalIgnoreCase))
                    continue;

                PackInfo info = new PackInfo();
                info.path = file;
                info.name = Path.GetFileNameWithoutExtension(file);
                result.Add(info);
            }
            catch
            {
            }
        }

        result.Sort(delegate (PackInfo a, PackInfo b)
        {
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private void ApplyPack(string path)
    {
        try
        {
            if (FindGarageSaveLoad() == null) throw new InvalidOperationException(
                LegacyLocalizer.Text("No car in garage", "Нет машины в гараже"));
            var pack = LegacyVinylPackCodec.ReadFile(path, curCar);
            CheckPaintCompatibility(pack);
            if (pack.TextCount > 0 && !LegacyVinylPortRuntime.SupportsText ||
                pack.PartCount > 0 && !LegacyVinylPortRuntime.SupportsParts)
            {
                pendingPack = pack; pendingPath = path; pendingCar = CarGarageUI.me.car;
                warning.Title = LegacyLocalizer.Text("VinylPort — compatibility", "VinylPort — совместимость");
                warning.Rect = new Rect((Screen.width - 550f) / 2f, (Screen.height - 340f) / 2f, 550, 340);
                warning.Visible = true;
                GUI.FocusControl(null);
                return;
            }
            ApplyValidatedPack(pack, path, false);
        }
        catch (Exception error) { ImportError(error); }
    }

    private void DrawWarning(Rect content)
    {
        if (pendingPack == null) { CancelPending(); return; }
        var ui = new LegacyUILayout(content, 8f);
        LegacyUI.Title(ui.Row(24f), LegacyLocalizer.Text("EnhancedCarTuning is required", "Нужен EnhancedCarTuning"));
        bool textFallback = pendingPack.TextCount > 0 && !LegacyVinylPortRuntime.SupportsText;
        bool partsFallback = pendingPack.PartCount > 0 && !LegacyVinylPortRuntime.SupportsParts;
        string text = textFallback ? LegacyLocalizer.Text(
            "Text layers: ", "Текстовых слоёв: ") + pendingPack.TextCount + LegacyLocalizer.Text(
            ". The tuning plugin is unavailable.\nLoad stock primitives instead?\nPosition, size and color will be preserved.",
            ". Плагин тюнинга недоступен.\nЗагрузить вместо текста штатные примитивы?\nПоложение, размер и цвет сохранятся.") : LegacyLocalizer.Text(
            "This pack contains individual body part colors.\nInstall or update EnhancedCarTuning to display them.",
            "В паке есть цвета отдельных деталей кузова.\nДля их отображения установи или обнови EnhancedCarTuning.");
        if (partsFallback) text += LegacyLocalizer.Text(
            "\nWithout the plugin, only the stock body color is visible.",
            "\nБез плагина виден только штатный цвет кузова.");
        LegacyUI.HintBox(ui.Row(132f), text);
        LegacyUI.HintBoxAlt(ui.Row(38f), LegacyLocalizer.Text(
            "The original .vinyl file will remain unchanged.", "Исходный файл .vinyl останется без изменений."));
        Rect buttons = ui.Row(34f);
        if (LegacyUI.GreenButton(new Rect(buttons.x, buttons.y, buttons.width - 118, buttons.height),
            textFallback ? LegacyLocalizer.Text("Load with primitives", "Загрузить с примитивами") :
            LegacyLocalizer.Text("Load stock color", "Загрузить общий цвет")))
        {
            var pack = pendingPack; string path = pendingPath; var car = pendingCar;
            CancelPending();
            if (!CarGarageUI.me || CarGarageUI.me.car != car) return;
            ApplyValidatedPack(pack, path, true);
        }
        if (LegacyUI.Button(new Rect(buttons.xMax - 110, buttons.y, 110, buttons.height),
            LegacyLocalizer.Text("Cancel", "Отмена"))) CancelPending();
    }

    private void ApplyValidatedPack(LegacyVinylPack pack, string path, bool allowFallback)
    {
        var garage = CarGarageUI.me;
        var saveLoad = FindGarageSaveLoad();
        string original = null, previous = null;
        bool applied = false, wrote = false, hadSave = false;
        string key = "CarSaved_" + curCar;
        try
        {
            if (!garage || saveLoad == null || !string.Equals(pack.Car, garage.car.prefabName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(LegacyLocalizer.Text("Garage car changed", "Машина в гараже изменилась"));
            bool missingText = pack.TextCount > 0 && !LegacyVinylPortRuntime.SupportsText;
            bool missingParts = pack.PartCount > 0 && !LegacyVinylPortRuntime.SupportsParts;
            CheckPaintCompatibility(pack);
            if ((missingText || missingParts) && !allowFallback) throw new InvalidOperationException(
                LegacyLocalizer.Text("Tuning plugin is unavailable", "Плагин тюнинга недоступен"));
            string primitive = missingText ? LegacyVinylPortRuntime.Primitive() : null;
            if (missingText && primitive == null) throw new LegacyVinylPackException("Primitive");
            hadSave = PlayerPrefs.HasKey(key);
            previous = hadSave ? PlayerPrefs.GetString(key) : null;
            original = CaptureCurrentGarageXml();
            if (string.IsNullOrEmpty(original)) throw new InvalidOperationException(
                LegacyLocalizer.Text("Failed to capture car state", "Не удалось получить состояние машины"));
            string xml = LegacyVinylPackCodec.Merge(pack, string.IsNullOrEmpty(previous) ? original : previous, primitive).OuterXml;
            applied = true;
            LegacyVinylPortRuntime.Apply(garage, saveLoad, xml);
            SyncGarageMaterialControls(saveLoad.car);
            wrote = true;
            PlayerPrefs.SetString(key, xml); PlayerPrefs.Save();
            Flash(LegacyLocalizer.Text("Applied pack: ", "Применён пак: ") + Path.GetFileNameWithoutExtension(path));
        }
        catch (Exception error)
        {
            if (applied && !string.IsNullOrEmpty(original) && garage && saveLoad && garage.car == saveLoad.car)
            {
                try { LegacyVinylPortRuntime.Apply(garage, saveLoad, original); SyncGarageMaterialControls(saveLoad.car); }
                catch (Exception rollback) { Debug.LogError("VinylPort: runtime rollback failed: " + rollback.GetType().Name); }
            }
            if (wrote)
            {
                try { if (hadSave) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
                catch (Exception rollback) { Debug.LogError("VinylPort: save rollback failed: " + rollback.GetType().Name); }
            }
            ImportError(error);
        }
    }

    private static void CheckPaintCompatibility(LegacyVinylPack pack)
    {
        if (pack.Document.DocumentElement["CarMaterial"] != null && LegacyVinylPortRuntime.NeedsPaintUpdate)
            throw new InvalidOperationException(LegacyLocalizer.Text(
                "Update EnhancedCarTuning to import paint colors safely", "Для переноса цветов обнови EnhancedCarTuning"));
    }

    private void ImportError(Exception error)
    {
        var invalid = error as LegacyVinylPackException;
        string message = invalid == null ? error is XmlException ? LegacyLocalizer.Text("Invalid XML", "Некорректный XML") :
            error.Message : PackError(invalid.Code);
        Flash(LegacyLocalizer.Text("Import error: ", "Ошибка импорта: ") + message);
    }

    private static string PackError(string code)
    {
        switch (code)
        {
            case "MissingVersion": return LegacyLocalizer.Text("Pack version is missing", "В паке не указана версия");
            case "Version": return LegacyLocalizer.Text("Invalid pack version", "Некорректная версия пака");
            case "UnsupportedVersion": return LegacyLocalizer.Text("Unsupported pack version (supported: 1–4)", "Версия пака не поддерживается (доступны 1–4)");
            case "Car": return LegacyLocalizer.Text("Pack or save is for another car", "Пак или сохранение от другой машины");
            case "Text": return LegacyLocalizer.Text("Invalid text, font, style or symmetry fields", "Некорректные поля текста, шрифта, стиля или симметрии");
            case "Parts": return LegacyLocalizer.Text("Invalid body part colors", "Некорректные цвета деталей кузова");
            case "Geometry": return LegacyLocalizer.Text("Invalid vinyl coordinates, size or color", "Некорректные координаты, размер или цвет винила");
            case "Material": return LegacyLocalizer.Text("Invalid stock paint fields", "Некорректные поля штатной покраски");
            case "Primitive": return LegacyLocalizer.Text("Stock primitive is unavailable; pack was not loaded", "Штатный примитив недоступен; пак не загружен");
            case "Size": return LegacyLocalizer.Text("Pack is too large", "Пак слишком большой");
            default: return LegacyLocalizer.Text("Invalid vinyl pack structure", "Некорректная структура пака винилов");
        }
    }

    private CarSaveLoad FindGarageSaveLoad()
    {
        var garage = CarGarageUI.me;
        return garage && garage.car && string.Equals(garage.car.prefabName, curCar, StringComparison.OrdinalIgnoreCase)
            ? garage.car.GetComponent<CarSaveLoad>() : null;
    }

    private string CaptureCurrentGarageXml()
    {
        var saveLoad = FindGarageSaveLoad();
        return saveLoad == null ? string.Empty : LegacyVinylPortRuntime.Capture(CarGarageUI.me, saveLoad);
    }

    private void SyncGarageMaterialControls(CarControl car)
    {
        if (CarGarageUI.me == null || car == null)
            return;

        CarMaterial carMaterial = car.GetComponent<CarMaterial>();
        if (carMaterial == null)
            return;

        if (CarGarageUI.me.bodyColor != null)
        {
            CarGarageUI.me.bodyColor.SetColor(carMaterial.body.color);
            CarGarageUI.me.bodyColor.SetMettalic(carMaterial.body.Mettalic);
            CarGarageUI.me.bodyColor.SetGloss(carMaterial.body.Gloss);
        }

        if (CarGarageUI.me.wheelColor != null)
        {
            CarGarageUI.me.wheelColor.SetColor(carMaterial.wheel.color);
            CarGarageUI.me.wheelColor.SetMettalic(carMaterial.wheel.Mettalic);
            CarGarageUI.me.wheelColor.SetGloss(carMaterial.wheel.Gloss);
        }
    }

    private void Export()
    {
        try
        {
            string xml = CaptureCurrentGarageXml();
            if (curCar == "" || string.IsNullOrEmpty(xml))
                throw new InvalidOperationException(LegacyLocalizer.Text("No car in garage", "Нет машины в гараже"));
            XmlDocument pack = LegacyVinylPackCodec.Export(LegacyVinylPackCodec.ReadXml(xml), curCar);
            Directory.CreateDirectory(carDir);
            string outputPath = Path.Combine(carDir, Sanitize(packName) + ".vinyl");
            pack.Save(outputPath);
            packName = "";
            RefreshPacks(false);
            Flash(LegacyLocalizer.Text("Export done!", "Экспорт готов!"));
        }
        catch (Exception error)
        {
            string message = error is LegacyVinylPackException invalid ? PackError(invalid.Code) : error.Message;
            Flash(LegacyLocalizer.Text("Export error: ", "Ошибка экспорта: ") + message);
        }
    }

    private void Flash(string text)
    {
        flashMsg = text;
        flashTime = Time.unscaledTime + 5f;
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "pack";

        string result = s;
        foreach (char c in Path.GetInvalidFileNameChars())
            result = result.Replace(c, '_');

        return result;
    }
}
