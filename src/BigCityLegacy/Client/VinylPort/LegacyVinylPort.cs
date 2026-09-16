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
    private const float WinHeight = 380f;
    private const float ListHeight = 220f;
    private const float PosXPercent = 0.156f;
    private const float PosYPercent = 0.15f;
    private const int MaxNameLength = 60;
    private const string PackNameControl = "VinylPort_PackName";

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
    private string packNmae = "my_vinyl";
    private string curCar = "";
    private string flashMsg = "";
    private float flashTime = 0f;
    private bool nameFocused;

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
                Draggable = false,
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
        }
        if (!win.Visible || !nameFocused) return;

        if (Input.GetMouseButtonDown(0))
        {
            nameFocused = IsMouseInsideNameField();
            if (!nameFocused) return;
        }

        string typed = Input.inputString;
        if (!string.IsNullOrEmpty(typed))
        {
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (c == '\b' || c == '\n' || c == '\r') continue;
                packNmae += c;
            }
        }
        if (Input.GetKeyDown(KeyCode.Backspace) && packNmae.Length > 0)
        {
            packNmae = packNmae.Substring(0, packNmae.Length - 1);
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
        {
            nameFocused = false;
        }
        if (packNmae.Length > MaxNameLength)
        {
            packNmae = packNmae.Substring(0, MaxNameLength);
        }
    }

    private bool IsMouseInsideNameField()
    {
        if (win == null || !win.Visible) return false;
        Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        Rect winRect = win.Rect;
        if (!winRect.Contains(mp)) return false;
        float padding = 8f;
        Rect content = new Rect(winRect.x + padding, winRect.y + padding + 22f, winRect.width - padding * 2f, winRect.height - padding * 2f - 22f);
        // имя + hint + list = 20+18+220 = 258; поле на позиции 258 от верха контента, высота 24
        float fieldY = content.y + 20f + 18f + ListHeight;
        Rect fieldRect = new Rect(content.x, fieldY, content.width * 0.6f - 4f, 24f);
        return fieldRect.Contains(mp);
    }

    public void Toggle()
    {
        if (win.Visible) Close();
        else Open();
    }

    public void Open()
    {
        curCar = CurrentGarageCar();
        choises = ScanPacksForCar(curCar);
        scroll.ResetScroll();
        win.Visible = true;
        nameFocused = false;
    }

    public void Close()
    {
        win.Visible = false;
        nameFocused = false;
    }

    private string CurrentGarageCar()
    {
        if (CarGarageUI.me && CarGarageUI.me.car) return CarGarageUI.me.car.prefabName;
        foreach (CarPaintItems p in Resources.FindObjectsOfTypeAll<CarPaintItems>())
        {
            if (p != null && p.gameObject.activeInHierarchy && p.car != null) return p.car.prefabName;
        }
        return "";
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

        LegacyUI.Label(ui.Row(20f), "Машина: " + (curCar == "" ? "<нет>" : curCar));
        LegacyUI.MiniHint(ui.Row(18f), "Паков: " + choises.Count);

        scroll.ViewRect = ui.Row(ListHeight);
        scroll.Draw(listContent =>
        {
            LegacyUILayout list = new LegacyUILayout(listContent, 5f);
            for (int i = 0; i < choises.Count; i++)
            {
                PackInfo info = choises[i];
                if (LegacyUI.Button(list.Row(26f), info.name))
                {
                    ApplyPack(info.path);
                }
            }
            if (choises.Count == 0)
            {
                LegacyUI.MiniHint(list.Row(18f), "Нет паков для этой машины");
            }
        });

        Rect row = ui.Row(24f);
        float nameWidth = row.width * 0.6f;
        Rect nameRect = new Rect(row.x, row.y, nameWidth - 4f, row.height);

        if (Event.current.type == EventType.MouseDown)
        {
            bool inside = nameRect.Contains(Event.current.mousePosition);
            if (inside)
            {
                nameFocused = true;
            }
            else if (nameFocused)
            {
                nameFocused = false;
            }
        }

        GUI.SetNextControlName(PackNameControl);
        packNmae = LegacyUI.TextField(nameRect, packNmae);

        if (nameFocused)
        {
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            float cursorX = nameRect.x + 4f + GUI.skin.textField.CalcSize(new GUIContent(packNmae)).x;
            cursorX = Mathf.Min(cursorX, nameRect.xMax - 4f);
            GUI.DrawTexture(new Rect(cursorX, nameRect.y + 3f, 1f, nameRect.height - 6f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        if (LegacyUI.GreenButton(new Rect(row.x + nameWidth, row.y, row.width - nameWidth, row.height), "Export")) Export();

        LegacyUI.Status(ui.Row(20f), Time.unscaledTime < flashTime ? flashMsg : "");
    }

    private List<PackInfo> ScanPacksForCar(string car)
    {
        List<PackInfo> result = new List<PackInfo>();
        if (car == "") return result;
        string packsDir = Path.Combine(Paths.GameRootPath, "VinylPacks");
        if (!Directory.Exists(packsDir)) return result;
        foreach (string file in Directory.GetFiles(packsDir, "*.vinyl", SearchOption.AllDirectories))
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
            catch { }
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
                Flash("нет CarPaint");
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
            if (old != null) save.DocumentElement.ReplaceChild(imp, old);
            else save.DocumentElement.AppendChild(imp);
            XmlNode mat = pack.DocumentElement.SelectSingleNode("CarMaterial");
            if (mat != null)
            {
                XmlNode oldMat = save.DocumentElement.SelectSingleNode("CarMaterial");
                XmlNode impMat = save.ImportNode(mat, true);
                if (oldMat != null) save.DocumentElement.ReplaceChild(impMat, oldMat);
                else save.DocumentElement.AppendChild(impMat);
            }
            PlayerPrefs.SetString("CarSaved_" + curCar, save.OuterXml);
            PlayerPrefs.Save();
            foreach (CarSaveLoad sl in Resources.FindObjectsOfTypeAll<CarSaveLoad>())
            {
                if (sl != null && sl.car != null && sl.car.prefabName == curCar)
                {
                    sl.LoadXml(save.OuterXml);
                    break;
                }
            }
            if (CarGarageUI.me) CarGarageUI.me.CarPaint_Finish();
            Flash("Применен пак: " + Path.GetFileNameWithoutExtension(path));
        }
        catch { }
    }

    private void Export()
    {
        try
        {
            if (curCar == "")
            {
                Flash("Нет машины в гараже");
                return;
            }
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
            if (mat != null) root.AppendChild(pack.ImportNode(mat, true));
            string carDir = Path.Combine(Paths.GameRootPath, "VinylPacks", Sanitze(curCar));
            Directory.CreateDirectory(carDir);
            pack.Save(Path.Combine(carDir, Sanitze(packNmae) + ".vinyl"));
            packNmae = "";
            Flash("экспорт готов");
        }
        catch { }
    }

    private void Flash(string text)
    {
        flashMsg = text;
        flashTime = Time.unscaledTime + 2f;
    }

    private string Sanitze(string s)
    {
        if (s == "") return "pack";
        string result = s;
        foreach (char c in Path.GetInvalidFileNameChars()) result = result.Replace(c, '_');
        return result;
    }
}