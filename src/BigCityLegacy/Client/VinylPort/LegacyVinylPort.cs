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
        win.Visible = false;
    }

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
            win.Draw(DrawContent);
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

    private string carSaveKey
    {
        get { return "CarSaved_" + curCar; }
    }

    private void DrawContent(Rect content)
    {
        LegacyUILayout ui = new LegacyUILayout(content);

        // Allocate all screen rectangles first. The order in which controls are DRAWN below
        // is intentionally different from their visual order: the TextField is created before
        // the dynamic ScrollView so its IMGUI control ID stays stable when the file list changes.
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

        // IMPORTANT: create the keyboard-focusable control before the dynamic scroll contents.
        // GUI.TextField manages keyboardControl, hotControl, caret and selection on its own.
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

        // Draw the dynamic list last in IMGUI control order. Its variable number of buttons
        // can no longer shift the control ID of the TextField above.
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
                XmlDocument pack = new XmlDocument();
                pack.Load(file);

                XmlElement root = pack.DocumentElement;
                if (root == null)
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
            XmlDocument pack = new XmlDocument();
            pack.Load(path);

            XmlElement packRoot = pack.DocumentElement;
            if (packRoot == null)
            {
                Flash(LegacyLocalizer.Text("Invalid pack", "Некорректный пак"));
                return;
            }

            XmlNode paint = packRoot.SelectSingleNode("CarPaint");
            if (paint == null)
            {
                Flash(LegacyLocalizer.Text("No CarPaint", "Нет CarPaint"));
                return;
            }

            string packCar = packRoot.GetAttribute("car");
            if (!string.Equals(packCar, curCar, StringComparison.OrdinalIgnoreCase))
            {
                Flash(LegacyLocalizer.Text("This pack is for another car", "Пак не для этой машины"));
                return;
            }

            string xml = GetOrCreateCarSaveXml();
            if (string.IsNullOrEmpty(xml))
            {
                Flash(LegacyLocalizer.Text("Failed to create car save", "Не удалось создать сохранение машины"));
                return;
            }

            XmlDocument save = new XmlDocument();
            save.LoadXml(xml);
            if (save.DocumentElement == null)
            {
                Flash(LegacyLocalizer.Text("Invalid car save", "Некорректное сохранение машины"));
                return;
            }

            XmlNode oldPaint = save.DocumentElement.SelectSingleNode("CarPaint");
            XmlNode importedPaint = save.ImportNode(paint, true);
            if (oldPaint != null)
                save.DocumentElement.ReplaceChild(importedPaint, oldPaint);
            else
                save.DocumentElement.AppendChild(importedPaint);

            XmlNode mat = packRoot.SelectSingleNode("CarMaterial");
            if (mat != null)
            {
                XmlNode oldMat = save.DocumentElement.SelectSingleNode("CarMaterial");
                XmlNode importedMat = save.ImportNode(mat, true);
                if (oldMat != null)
                    save.DocumentElement.ReplaceChild(importedMat, oldMat);
                else
                    save.DocumentElement.AppendChild(importedMat);
            }

            PlayerPrefs.SetString(carSaveKey, save.OuterXml);
            PlayerPrefs.Save();

            CarSaveLoad targetSaveLoad = FindGarageSaveLoad();
            if (targetSaveLoad != null)
            {
                targetSaveLoad.LoadXml(save.OuterXml);

                SyncGarageMaterialControls(targetSaveLoad.car);
            }

            if (CarGarageUI.me)
                CarGarageUI.me.CarPaint_Finish();

            LegacyLocalizedText packApplied = new LegacyLocalizedText("Applied pack: ", "Применен пак: ");
            Flash(packApplied + Path.GetFileNameWithoutExtension(path));
        }
        catch (Exception ex)
        {
            LegacyLocalizedText importErr = new LegacyLocalizedText("Import error: ", "Ошибка импорта: ");
            Flash(importErr + ex.Message);
        }
    }

    private CarSaveLoad FindGarageSaveLoad()
    {
        if (CarGarageUI.me != null && CarGarageUI.me.car != null &&
            string.Equals(CarGarageUI.me.car.prefabName, curCar, StringComparison.OrdinalIgnoreCase))
        {
            CarSaveLoad direct = CarGarageUI.me.car.GetComponent<CarSaveLoad>();
            if (direct != null)
                return direct;
        }

        foreach (CarSaveLoad sl in Resources.FindObjectsOfTypeAll<CarSaveLoad>())
        {
            if (sl != null && sl.car != null &&
                string.Equals(sl.car.prefabName, curCar, StringComparison.OrdinalIgnoreCase))
            {
                return sl;
            }
        }

        return null;
    }

    private string GetOrCreateCarSaveXml()
    {
        string xml = CarSaveLoad.GetXmlForCar(curCar);
        if (!string.IsNullOrEmpty(xml))
            return xml;

        CarSaveLoad targetSaveLoad = FindGarageSaveLoad();
        if (targetSaveLoad == null)
            return string.Empty;

        XmlDocument current = targetSaveLoad.SaveXml();
        if (current == null || current.DocumentElement == null)
            return string.Empty;

        xml = current.OuterXml;

        // A never-edited car has no CarSaved_<prefab> key at all. Seed it from the
        // current runtime/default car state so there is a valid XML document into
        // which the imported CarPaint/CarMaterial nodes can be merged.
        PlayerPrefs.SetString(carSaveKey, xml);
        PlayerPrefs.Save();

        return xml;
    }

    private string CaptureCurrentGarageXml()
    {
        CarSaveLoad targetSaveLoad = FindGarageSaveLoad();
        if (targetSaveLoad == null)
            return string.Empty;

        XmlDocument current = targetSaveLoad.SaveXml();
        if (current == null || current.DocumentElement == null)
            return string.Empty;

        return current.OuterXml;
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
        bool playerPrefsSwapped = false;
        bool hadPreviousSave = false;
        string previousSaveXml = null;

        try
        {
            if (curCar == "")
            {
                Flash(LegacyLocalizer.Text("No car in garage", "Нет машины в гараже"));
                return;
            }

            string currentGarageXml = CaptureCurrentGarageXml();
            if (string.IsNullOrEmpty(currentGarageXml))
            {
                Flash(LegacyLocalizer.Text("Failed to get current car state", "Не удалось получить текущее состояние машины"));
                return;
            }

            // Export must use the state that is visible in the editor right now, not
            // the last state the player accepted/saved. Temporarily replace the car's
            // PlayerPrefs XML in memory, use the normal GetXmlForCar pipeline, then
            // restore the exact previous value in finally. Do NOT call PlayerPrefs.Save()
            // while the temporary value is installed: an export must not implicitly
            // commit the player's editor changes to disk.
            hadPreviousSave = PlayerPrefs.HasKey(carSaveKey);
            if (hadPreviousSave)
                previousSaveXml = PlayerPrefs.GetString(carSaveKey);

            PlayerPrefs.SetString(carSaveKey, currentGarageXml);
            playerPrefsSwapped = true;

            string xml = CarSaveLoad.GetXmlForCar(curCar);
            if (string.IsNullOrEmpty(xml))
            {
                Flash(LegacyLocalizer.Text("Failed to prepare car state for export", "Не удалось подготовить состояние машины для экспорта"));
                return;
            }

            XmlDocument save = new XmlDocument();
            save.LoadXml(xml);
            if (save.DocumentElement == null)
            {
                Flash(LegacyLocalizer.Text("Invalid car state", "Некорректное состояние машины"));
                return;
            }

            XmlNode paint = save.DocumentElement.SelectSingleNode("CarPaint");
            if (paint == null)
            {
                Flash(LegacyLocalizer.Text("No CarPaint", "Нет CarPaint"));
                return;
            }

            XmlDocument pack = new XmlDocument();
            XmlElement root = pack.CreateElement("VinylPack");
            root.SetAttribute("version", "3");
            root.SetAttribute("car", curCar);
            pack.AppendChild(root);
            root.AppendChild(pack.ImportNode(paint, true));

            XmlNode mat = save.DocumentElement.SelectSingleNode("CarMaterial");
            if (mat != null)
                root.AppendChild(pack.ImportNode(mat, true));

            Directory.CreateDirectory(carDir);

            string outputPath = Path.Combine(carDir, Sanitize(packName) + ".vinyl");
            pack.Save(outputPath);

            packName = "";
            RefreshPacks(false);
            Flash(LegacyLocalizer.Text("Export done!", "Экспорт готов!"));
        }
        catch (Exception ex)
        {
            LegacyLocalizedText exportErr = new LegacyLocalizedText("Export error: ", "Ошибка экспорта: ");
            Flash(exportErr + ex.Message);
        }
        finally
        {
            if (playerPrefsSwapped)
            {
                if (hadPreviousSave)
                    PlayerPrefs.SetString(carSaveKey, previousSaveXml ?? string.Empty);
                else
                    PlayerPrefs.DeleteKey(carSaveKey);
            }
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
