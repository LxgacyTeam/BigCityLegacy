using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using BepInEx;
using BigCityLegacy.UI;
using UnityEngine;

public class LegacyVinylPort : MonoBehaviour
{
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
    private bool wasInGarage = false;
    private bool showList = false;
    private List<PackInfo> choises = new List<PackInfo>();
    private string packNmae = "my_vinyl";
    private string curCar = "";
    private float nextScane = 0f;
    private string flashMsg = "";
    private float flashTme = 0f;

    private struct PackInfo
    {
        public string path;
        public string name;
    }

    private void Awake()
    {
        win = new LegacyUIWindow(
            "VinylPort",
            new Rect(40f, 40f, 430f, 215f),
            new LegacyUIWindowOptions
            {
                Draggable = true,
                ClampToScreen = true,
                ShowCloseButton = false,
                RightHint = "[F8]",
                InputBlockMode = LegacyUIInputBlockMode.Window
            }
        );
        win.Visible = false;

        scroll = new LegacyUIScrollView(new Rect(0f, 0f, 100f, 100f));
        scroll.Padding = 8f;
        scroll.ShowHorizontalScrollbar = false;
        scroll.ShowVerticalScrollbar = true;
    }

    public void Toggle()
    {
        win.Visible = !win.Visible;
        if (!win.Visible) showList = false;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScane)
        {
            nextScane = Time.unscaledTime + 0.5f;
            string car = FindGarageCar();
            bool inGarage = !string.IsNullOrEmpty(car);

            if (inGarage != wasInGarage)
            {
                wasInGarage = inGarage;
                if (inGarage)
                {
                    win.Visible = true;
                }
                else
                {
                    win.Visible = false;
                    showList = false;
                }
            }
            curCar = car;
        }
    }

    private void OnGUI()
    {
        if (win == null || !win.Visible) return;
        using (new LegacyUIGuiScope(-10000))
        {
            win.Draw(DrawContent);
        }
    }

    private void DrawContent(Rect content)
    {
        LegacyUILayout ui = new LegacyUILayout(content);

        if (showList)
        {
            LegacyUI.MiniHint(ui.Row(18f), "Паки для " + curCar + ": " + choises.Count);
            scroll.ViewRect = ui.Row(100f);
            scroll.Draw(listContent => {
                LegacyUILayout list = new LegacyUILayout(listContent, 5f);
                for (int i = 0; i < choises.Count; i++)
                {
                    PackInfo info = choises[i];
                    if (LegacyUI.Button(list.Row(26f), info.name))
                    {
                        ApplyPack(info.path);
                        showList = false;
                    }
                }
            });
            if (LegacyUI.DangerButton(ui.Row(22f), "Отмена")) showList = false;
        }
        else
        {
            LegacyUI.Label(ui.Row(20f), "Машина в гараже: " + (string.IsNullOrEmpty(curCar) ? "<нет>" : curCar));
            LegacyUI.Label(ui.Row(20f), "Имя пака (для экспорта):");
            packNmae = LegacyUI.TextField(ui.Row(24f), packNmae);

            Rect row = ui.Row(26f);
            float btnWidth = (row.width - 6f) * 0.5f;
            Rect leftBtn = new Rect(row.x, row.y, btnWidth, row.height);
            Rect rightBtn = new Rect(row.x + btnWidth + 6f, row.y, btnWidth, row.height);

            if (LegacyUI.GreenButton(leftBtn, "Export")) Export();
            if (LegacyUI.Button(rightBtn, "Import")) Import();

            if (LegacyUI.Button(ui.Row(24f), "Открыть папку с винилами")) OpenPacksFolder();
        }

        string status = Time.unscaledTime < flashTme ? flashMsg : "";
        LegacyUI.Status(ui.Row(20f), status);
    }

    private string FindGarageCar()
    {
        CarPaintItems[] items = Resources.FindObjectsOfTypeAll<CarPaintItems>();
        foreach (CarPaintItems p in items)
        {
            if (p != null && p.gameObject.activeInHierarchy && p.car != null)
            {
                return p.car.prefabName;
            }
        }
        return "";
    }

    private void Import()
    {
        if (string.IsNullOrEmpty(curCar))
        {
            Flash("Нет машниы в гараже");
            return;
        }

        choises = ScanPacksForCar(curCar);
        if (choises.Count == 0)
        {
            Flash("Нет паков для " + curCar);
            return;
        }

        if (choises.Count == 1)
        {
            ApplyPack(choises[0].path);
            return;
        }

        scroll.ResetScroll();
        showList = true;
    }

    private List<PackInfo> ScanPacksForCar(string car)
    {
        List<PackInfo> result = new List<PackInfo>();
        string packsDir = Path.Combine(Paths.GameRootPath, "VinylPacks");

        if (!Directory.Exists(packsDir)) return result;

        string[] files = Directory.GetFiles(packsDir, "*.vinyl", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            try
            {
                XmlDocument pack = new XmlDocument();
                pack.Load(file);

                string packCar = pack.DocumentElement.GetAttribute("car");
                if (!string.Equals(packCar, car, StringComparison.OrdinalIgnoreCase)) continue;

                PackInfo info = new PackInfo();
                info.path = file;
                info.name = Path.GetFileNameWithoutExtension(file);
                result.Add(info);
            }
            catch
            {
            }
        }

        return result;
    }

    private void ApplyPack(string path)
    {
        try
        {
            XmlDocument pack = new XmlDocument();
            pack.Load(path);

            XmlNode paint = pack.DocumentElement.SelectSingleNode("CarPaint");
            if (paint == null)
            {
                Flash("В паке нет CarPaint");
                return;
            }

            string packCar = pack.DocumentElement.GetAttribute("car");
            if (!string.Equals(packCar, curCar, StringComparison.OrdinalIgnoreCase))
            {
                Flash("Пак не для этой машины");
                return;
            }

            string xml = CarSaveLoad.GetXmlForCar(curCar);
            if (string.IsNullOrEmpty(xml)) return;

            XmlDocument save = new XmlDocument();
            save.LoadXml(xml);

            XmlNode old = save.DocumentElement.SelectSingleNode("CarPaint");
            XmlNode imp = save.ImportNode(paint, true);

            if (old != null)
            {
                save.DocumentElement.ReplaceChild(imp, old);
            }
            else
            {
                save.DocumentElement.AppendChild(imp);
            }

            XmlNode mat = pack.DocumentElement.SelectSingleNode("CarMaterial");
            if (mat != null)
            {
                XmlNode oldMat = save.DocumentElement.SelectSingleNode("CarMaterial");
                XmlNode impMat = save.ImportNode(mat, true);

                if (oldMat != null)
                {
                    save.DocumentElement.ReplaceChild(impMat, oldMat);
                }
                else
                {
                    save.DocumentElement.AppendChild(impMat);
                }
            }

            PlayerPrefs.SetString("CarSaved_" + curCar, save.OuterXml);
            PlayerPrefs.Save();

            CarSaveLoad[] loaders = Resources.FindObjectsOfTypeAll<CarSaveLoad>();
            foreach (CarSaveLoad sl in loaders)
            {
                if (sl != null && sl.car != null && sl.car.prefabName == curCar)
                {
                    sl.LoadXml(save.OuterXml);
                    break;
                }
            }

            Flash("Применен пак: " + Path.GetFileNameWithoutExtension(path));
        }
        catch
        {
            Flash("Ошибка приминения пака");
        }
    }

    private void Flash(string text)
    {
        flashMsg = text;
        flashTme = Time.unscaledTime + 2f;
    }

    private void Export()
    {
        try
        {
            string xml = CarSaveLoad.GetXmlForCar(curCar);
            if (string.IsNullOrEmpty(xml)) return;

            XmlDocument save = new XmlDocument();
            save.LoadXml(xml);

            XmlNode paint = save.DocumentElement.SelectSingleNode("CarPaint");
            if (paint == null) return;

            XmlDocument pack = new XmlDocument();
            XmlElement root = pack.CreateElement("VinylPack");
            root.SetAttribute("version", "3");
            root.SetAttribute("car", curCar);
            pack.AppendChild(root);

            root.AppendChild(pack.ImportNode(paint, true));

            XmlNode mat = save.DocumentElement.SelectSingleNode("CarMaterial");
            if (mat != null)
            {
                root.AppendChild(pack.ImportNode(mat, true));
            }

            string packsDir = Path.Combine(Paths.GameRootPath, "VinylPacks");
            string carDir = Path.Combine(packsDir, Sanitze(curCar));
            Directory.CreateDirectory(carDir);

            string fileName = Sanitze(packNmae) + ".vinyl";
            pack.Save(Path.Combine(carDir, fileName));

            packNmae = "";
            Flash("Экспорт готов");
        }
        catch
        {
        }
    }

    private void OpenPacksFolder()
    {
        try
        {
            string packsDir = Path.Combine(Paths.GameRootPath, "VinylPacks");
            Directory.CreateDirectory(packsDir);

            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                System.Diagnostics.Process.Start("explorer.exe", packsDir.Replace('/', '\\'));
            }
            else if (Application.platform == RuntimePlatform.LinuxPlayer ||
                       Application.platform == RuntimePlatform.LinuxEditor)
            {
                System.Diagnostics.Process.Start("xdg-open", packsDir);
            }
            else if (Application.platform == RuntimePlatform.OSXPlayer ||
                       Application.platform == RuntimePlatform.OSXEditor)
            {
                System.Diagnostics.Process.Start("open", packsDir);
            }
            else
            {
                System.Diagnostics.Process.Start(packsDir);
            }
        }
        catch
        {
        }
    }

    private string Sanitze(string s)
    {
        if (string.IsNullOrEmpty(s)) return "pack";

        string result = s;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(c, '_');
        }
        return result;
    }
}