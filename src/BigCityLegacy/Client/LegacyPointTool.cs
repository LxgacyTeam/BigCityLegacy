using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BigCityLegacy.UI;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class LegacyPointTool : MonoBehaviour, OnGuiGlobal.IOnGuiNeed
{
	internal static void InitIfNeeded()
	{
        int PointToolToggle = PlayerPrefs.GetInt("BigCityLegacy.PointToolToggle");

        if (LegacyPointTool.instance && PointToolToggle == 0)
        {
            GameObject overlayGameObject = GameObject.Find("PointTool");
            Destroy(overlayGameObject);
        }

        if (LegacyPointTool.instance)
		{
			return;
		}

        if (PointToolToggle == 0)
        {
            return;
        }

        GameObject gameObject = new GameObject("PointTool");
		global::UnityEngine.Object.DontDestroyOnLoad(gameObject);
		LegacyPointTool.instance = gameObject.AddComponent<LegacyPointTool>();
        string text = getPosOutFilePath;
        if (File.Exists(text))
        {
            File.Delete(text);
        }
    }

	public bool isNeedOnGUI()
	{
		return true;
	}

    private bool ShowTeleportWindow = false;
    private bool jsonByPlayerPos = false;
    private bool UseVisualPoints = false;
    private string teleportX;
    private string teleportY;
    private string teleportZ;
    private string teleportJson;
    private int teleportParseMode = 0;
    private string[] teleportParseModes = { "RAW", "JSON" };
	private bool UseFade = false;
	private bool teleportWithoutCar = false;
    private bool isInteractive = false;
    private LegacyUIWindow pointWindow;

    public void OnGUI_Manual()
    {
        EnsurePointWindow();

        pointWindow.Visible = _isVisible;
        pointWindow.Rect = new Rect(
            pointWindow.Rect.x,
            pointWindow.Rect.y,
            490f,
            ShowTeleportWindow ? 322f : 227f);

        Vector3 cameraPos = hCamera.firstCamPos;
        Vector3 playerPos = Vector3.zero;
        Quaternion cameraRot = Quaternion.identity;

        if (CamCurrent.curSceneLoad && CamCurrent.curSceneLoad.unityCam)
        {
            Transform transform = CamCurrent.curSceneLoad.unityCam.transform;
            cameraPos = transform.position;
            cameraRot = transform.rotation;
        }

        InputControl firstUser = InputControl.firstUser;
        if (firstUser && firstUser.currentOrParent)
        {
            playerPos = firstUser.currentOrParent.body_pos;
        }

        Vector3 jsonPos = jsonByPlayerPos ? playerPos : cameraPos;

        jsonCopyText = "{ \"x\": " + FormatJsonFloat(jsonPos.x) +
                       ", \"y\": " + FormatJsonFloat(jsonPos.y) +
                       ", \"z\": " + FormatJsonFloat(jsonPos.z) +
                       ", \"yaw\": " + FormatJsonFloat(cameraRot.eulerAngles.y) +
                       " }";

        using (new LegacyUIGuiScope(-20000))
        {
            pointWindow.Draw(content => DrawPointToolContent(content, cameraPos, cameraRot, playerPos));
        }

        _isVisible = pointWindow.Visible;
    }

    private void EnsurePointWindow()
    {
        if (pointWindow != null)
        {
            return;
        }

        pointWindow = new LegacyUIWindow(
            "POINT TOOL v2",
            new Rect(10f, 10f, 490f, 237f),
            new LegacyUIWindowOptions
            {
                Draggable = true,
                ClampToScreen = true,
                ShowCloseButton = false,
                FadeWhileDragging = true,
                DragAlpha = 0.5f,
                HeaderHeight = 38f,
                ScreenPadding = 8f,
                RightHint = "[F8]",
                BackgroundAlpha = 0.6f
            });

        pointWindow.Visible = _isVisible;
    }

    private void DrawPointToolContent(Rect content, Vector3 cameraPos, Quaternion cameraRot, Vector3 playerPos)
    {
        float x = content.x;
        float y = content.y;
        float rectMainX = x + 10f;
        float rectLine1Y = y + 92f;
        float rectLine2Y = y + 130f;
        float rectButtonWidth = 75f;
        float rectHeight = 25f;

        LegacyUI.Label(new Rect(x + 10f, y, 440f, 18f), "Camera pos: " + FormatVector(cameraPos));
        LegacyUI.Label(new Rect(x + 10f, y + 17f, 440f, 18f), "Camera rot: " + FormatVector(cameraRot.eulerAngles));
        LegacyUI.Label(new Rect(x + 10f, y + 34f, 440f, 18f), "Player pos: " + FormatVector(playerPos));
        LegacyUI.Label(new Rect(x + 10f, y + 51f, 440f, 18f), "Point JSON: " + jsonCopyText);

        GUIStyle hotkeysHint = new GUIStyle(LegacyUI.Styles.Label);
        hotkeysHint.fontStyle = FontStyle.Italic;
        hotkeysHint.fontSize = 12;
        hotkeysHint.richText = true;
        hotkeysHint.normal.textColor = new Color32(255, 255, 255, 192);
        GUI.Label(new Rect(x + 125f, y + 57f, 300f, 50f), "<b>F9</b> - copy | <b>X</b> - remove last | <b>Z</b> - save", hotkeysHint);

        string teleportButtonLabel = ShowTeleportWindow ? "▲ Close" : "▼ Teleport";

        if (!NetManager.isOnlineClient)
        {
            if (LegacyUI.Button(new Rect(rectMainX, rectLine2Y, rectButtonWidth, rectHeight), teleportButtonLabel))
            {
                ShowTeleportWindow = !ShowTeleportWindow;
            }
        }

        if (LegacyUI.DangerButton(new Rect(rectMainX + 365f, rectLine2Y, rectButtonWidth, rectHeight), "Clear All"))
        {
            ClearAllPoints();
        }

        if (LegacyUI.Button(new Rect(rectMainX + 260f, rectLine2Y, rectButtonWidth + 25f, rectHeight), "Open out file"))
        {
            OpenPosOutFile();
        }

        GUIStyle separatorStyle = new GUIStyle(LegacyUI.Styles.Label);
        separatorStyle.fontSize = 20;
        separatorStyle.normal.textColor = new Color32(255, 255, 255, 128);

        float rectjsonByPlayerPosWidth = 135f;
        float rectToggleX = rectMainX + rectButtonWidth + 117f + rectjsonByPlayerPosWidth;

        string copyModeLabel;
        float rectjsonByPlayerPosX;

        if (!jsonByPlayerPos)
        {
            copyModeLabel = "JSON by Player pos";
            rectjsonByPlayerPosX = rectToggleX - rectjsonByPlayerPosWidth - 20f;
        }
        else
        {
            copyModeLabel = "JSON by Camera pos";
            rectjsonByPlayerPosX = rectToggleX - rectjsonByPlayerPosWidth - 28f;
        }

        float rectUseVisualX;

        if (UseVisualPoints)
        {
            _visPointsIsToggled = true;
            rectUseVisualX = rectToggleX - 87f;
            jsonByPlayerPos = LegacyUI.Toggle(new Rect(rectjsonByPlayerPosX - 75f, rectLine1Y + 4f, rectjsonByPlayerPosWidth + 10f, rectHeight), jsonByPlayerPos, copyModeLabel);

            GUI.Label(new Rect(rectUseVisualX - 10f, rectLine1Y + 2f, 10f, rectHeight), "|", separatorStyle);
            UseVisualPoints = LegacyUI.Toggle(new Rect(rectUseVisualX, rectLine1Y + 4f, 135f, rectHeight), UseVisualPoints, "Use visual points");

            GUI.Label(new Rect(rectToggleX + 29f, rectLine1Y + 2f, 10f, rectHeight), "|", separatorStyle);
            isInteractive = LegacyUI.Toggle(new Rect(rectToggleX + 38f, rectLine1Y + 4f, 135f, rectHeight), isInteractive, "Interactive");
        }
        else
        {
            rectUseVisualX = rectToggleX;
            jsonByPlayerPos = LegacyUI.Toggle(new Rect(rectjsonByPlayerPosX + 10f, rectLine1Y + 4f, rectjsonByPlayerPosWidth + 10f, rectHeight), jsonByPlayerPos, copyModeLabel);
            GUI.Label(new Rect(rectToggleX - 11f, rectLine1Y + 2f, 10f, rectHeight), "|", separatorStyle);
            UseVisualPoints = LegacyUI.Toggle(new Rect(rectUseVisualX, rectLine1Y + 4f, 135f, rectHeight), UseVisualPoints, "Use visual points");
            if (_visPointsIsToggled)
            {
                LegacyEventsConfig.ClearDebugRaceCheckpoints();
                _visPointsIsToggled = false;
            }
        }

        if (ShowTeleportWindow)
        {
            if (NetManager.isOnlineClient) { ShowTeleportWindow = !ShowTeleportWindow; }
            DrawTeleportContent(new Rect(x, y + 180f, 460f, 80f), cameraPos, rectHeight);
        }
    }

    private void DrawTeleportContent(Rect area, Vector3 cameraPos, float rectHeight)
    {
        LegacyUI.Separator(new Rect(area.x, area.y - 8f, area.width, 1f));

        float rectTeleportX = area.x + 10f;
        float rectTeleportY = area.y + 10f;
        float rectFieldWidth = 120f;

        float tabWidth = 62.5f;
        if (LegacyUI.TabButton(new Rect(rectTeleportX, area.y + 45f, tabWidth, rectHeight), teleportParseModes[0], teleportParseMode == 0))
            teleportParseMode = 0;
        if (LegacyUI.TabButton(new Rect(rectTeleportX + tabWidth, area.y + 45f, tabWidth, rectHeight), teleportParseModes[1], teleportParseMode == 1))
            teleportParseMode = 1;

        if (teleportParseMode == 0)
        {
            LegacyUI.Label(new Rect(rectTeleportX, rectTeleportY + 2f, 25f, rectHeight), "X:");
            teleportX = LegacyUI.TextField(new Rect(rectTeleportX + 20f, rectTeleportY, rectFieldWidth, rectHeight), teleportX);

            float rectFieldY_X = rectTeleportX + rectFieldWidth + 50f;
            LegacyUI.Label(new Rect(rectFieldY_X - 20f, rectTeleportY + 2f, 25f, rectHeight), "Y:");
            teleportY = LegacyUI.TextField(new Rect(rectFieldY_X, rectTeleportY, rectFieldWidth, rectHeight), teleportY);

            float rectFieldZ_X = rectFieldY_X + rectFieldWidth + 30f;
            LegacyUI.Label(new Rect(rectFieldZ_X - 20f, rectTeleportY + 2f, 25f, rectHeight), "Z:");
            teleportZ = LegacyUI.TextField(new Rect(rectFieldZ_X, rectTeleportY, rectFieldWidth, rectHeight), teleportZ);
        }
        else
        {
            LegacyUI.Label(new Rect(rectTeleportX, rectTeleportY + 2f, 50f, rectHeight), "JSON:");
            teleportJson = LegacyUI.TextField(new Rect(rectTeleportX + 45f, rectTeleportY, 395f, rectHeight), teleportJson);
        }

        UseFade = LegacyUI.Toggle(new Rect(rectTeleportX + 135f, area.y + 47f, 55f, rectHeight), UseFade, "Fade");
        teleportWithoutCar = LegacyUI.Toggle(new Rect(rectTeleportX + 195f, area.y + 47f, 85f, rectHeight), teleportWithoutCar, "w/o car");

        if (LegacyUI.Button(new Rect(rectTeleportX + 305f, area.y + 45f, 80f, rectHeight), "To CamPos"))
        {
            TeleportByPoint(cameraPos, UseFade, teleportWithoutCar);
        }

        if (LegacyUI.GreenButton(new Rect(rectTeleportX + 390f, area.y + 45f, 50f, rectHeight), "GO!"))
        {
            if (teleportParseMode == 0)
            {
                if (PosParser.TryParsePosFromString(teleportX, teleportY, teleportZ, out Vector3 teleportPos))
                {
                    TeleportByPoint(teleportPos, UseFade, teleportWithoutCar);
                }
            }
            else
            {
                if (PosParser.TryParsePosFromJson(teleportJson, out Vector3 teleportPos, out _))
                {
                    TeleportByPoint(teleportPos, UseFade, teleportWithoutCar);
                }
            }
        }
    }

	private bool RemoveLastPoint()
	{
		string lastPoint = PeekLastPointFromFile();

        if (lastPoint == null)
        {
            return false;
        }

        if (!PosParser.TryParsePosFromJson(lastPoint, out Vector3 pointPos, out _))
        {
            string warningMessage = "Failed to parse last point from pos_out.txt";
            Debug.LogWarning("[BigCityLegacy] " + warningMessage + ": " + lastPoint);

            if (GameUI.me)
            {
                GameUI.me.ShowPopup(warningMessage, 0.75f, true);
            }

            return false;
        }

		if (UseVisualPoints)
		{
            if (!LegacyEventsConfig.RemoveDebugRaceCheckpoint(pointPos))
            {
                return false;
            }
        }

        return RemoveLastPointFromFile();
    }

	private static string PeekLastPointFromFile()
	{
		string filePath = getPosOutFilePath;

        if (!File.Exists(filePath))
        {
            string warningMessage = "pos_out.txt is empty or not exists";
            Debug.LogWarning("[BigCityLegacy] " + warningMessage);

            if (GameUI.me)
            {
                GameUI.me.ShowPopup(warningMessage, 0.75f, true);
            }

            return null;
        }

        string[] lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
        {
            string warningMessage = "pos_out.txt is empty or not exists";
            Debug.LogWarning("[BigCityLegacy] " + warningMessage);

            if (GameUI.me)
            {
                GameUI.me.ShowPopup(warningMessage, 0.75f, true);
            }

            return null;
        }

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i];

            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim().TrimEnd(',');
            }
        }

        string emptyWarningMessage = "pos_out.txt contains no valid points";
        Debug.LogWarning("[BigCityLegacy] " + emptyWarningMessage);

        if (GameUI.me)
        {
            GameUI.me.ShowPopup(emptyWarningMessage, 0.75f, true);
        }

        return null;
    }

    private void OpenPosOutFile()
    {
        if (!CheckPosOutFileExistsOrNotEmpty())
        {
            return;
        }

        string filePath = getPosOutFilePath;

        Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    private static bool RemoveLastPointFromFile()
	{
		string filePath = getPosOutFilePath;

        if (!File.Exists(filePath))
        {
            return false;
        }

        var lines = File.ReadAllLines(filePath).ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            File.WriteAllText(filePath, string.Empty);
            return false;
        }

        lines.RemoveAt(lines.Count - 1);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count > 0)
        {
            string prevLast = lines[lines.Count - 1].TrimEnd();

            if (prevLast.EndsWith(",", StringComparison.Ordinal))
            {
                lines[lines.Count - 1] = prevLast.Substring(0, prevLast.Length - 1);
            }
        }

        File.WriteAllLines(filePath, lines);

        return true;
    }

    private bool CheckPosOutFileExistsOrNotEmpty()
    {
        string filePath = getPosOutFilePath;

        if (!File.Exists(filePath))
        {
            if (GameUI.me)
            {
                GameUI.me.ShowPopup("pos_out.txt is empty or not exists", 0.75f, true);
            }
            return false;
        }

        var lines = File.ReadAllLines(filePath).ToList();

        if (lines.Count == 0)
        {
            if (GameUI.me)
            {
                GameUI.me.ShowPopup("pos_out.txt is empty or not exists", 0.75f, true);
            }
            return false;
        }

        return true;
    }

    private void ClearAllPoints()
    {
        if (!CheckPosOutFileExistsOrNotEmpty())
        {
            return;
        }

        string filePath = getPosOutFilePath;

        AskBoxUI.Show("Clear ALL saved points?", delegate (AskBoxUI box)
        {
            if (box.isYes)
            {
                LegacyEventsConfig.ClearDebugRaceCheckpoints();
                string file = getPosOutFilePath;
                File.Delete(file);
                if (GameUI.me)
                {
                    GameUI.me.ShowPopup("All points cleared", 0.75f, true);
                }
            }
        });
    }

    public static class PosParser
    {
        public static bool TryParsePosFromJson(string json, out Vector3 returnPos, out float? returnYaw)
        {
            returnPos = Vector3.zero;
            float? yaw = null;
            returnYaw = yaw;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                PosJson data = JsonConvert.DeserializeObject<PosJson>(json);

                if (data == null)
                {
                    return false;
                }

                returnPos = new Vector3(data.x, data.y, data.z);
                yaw = data.yaw;
                returnYaw = yaw;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParsePosFromString(string x, string y, string z, out Vector3 teleportPos)
        {
            teleportPos = Vector3.zero;

            bool numX = TryParseFloat(x, out float xValue);
            bool numY = TryParseFloat(y, out float yValue);
            bool numZ = TryParseFloat(z, out float zValue);

            if (!numX || !numY || !numZ)
            {
                Debug.LogWarning("[BigCityLegacy] Teleport raw parse failed: x=" + x + ", y=" + y + ", z=" + z);
                return false;
            }

            teleportPos = new Vector3(xValue, yValue, zValue);
            return true;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(value))
            {
                value = value.Replace(',', '.');
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            }

            result = 0f;
            return false;
        }

        private class PosJson
        {
            public float x;
            public float y;
            public float z;
            public float yaw;
        }
    }

    private static PlayerTeleport activeTeleport;

    private static void TeleportByPoint(Vector3 point, bool fade, bool withoutCar)
    {
        InputControl input = InputControl.GetFirstUser();

        if (!input)
        {
            Debug.LogWarning("[BigCityLegacy] Teleport failed: first user input is null");
            return;
        }

        Control current = input.current;

        if (!current)
        {
            Debug.LogWarning("[BigCityLegacy] Teleport failed: current control is null");
            return;
        }

        if (activeTeleport)
        {
            UnityEngine.Object.Destroy(activeTeleport.gameObject);
            activeTeleport = null;
        }

        Quaternion rot = current.body_rot;

        activeTeleport = PlayerTeleport.CreatGo("BigCityLegacy_DebugTeleport");
        activeTeleport.transform.position = point;
        activeTeleport.transform.rotation = rot;

        activeTeleport.Init(
            fade,   // auto fade
            withoutCar,   // clear parent control
            current  // target control
        );

        Debug.Log("[BigCityLegacy] Teleport started: " + FormatVector(point));
    }

    private void OnGUI()
	{
        if (!LegacyHelpers.IsGameplayRunning)
        {
            return;
        }

        if (!_isVisible)
		{
			return;
		}

		OnGUI_Manual();
	}

    private static string FormatJsonFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatVector(Vector3 v)
	{
		return string.Concat(new string[]
		{
			v.x.ToString("0.###"),
			", ",
			v.y.ToString("0.###"),
			", ",
			v.z.ToString("0.###")
		});
	}

	private void Update()
	{
        if (Input.GetKeyDown(KeyCode.F8))
        {
            EnsurePointWindow();
            _isVisible = !_isVisible;
            pointWindow.Visible = _isVisible;
            pointWindow.SavePrefs();
        }

        if (!LegacyHelpers.IsGameplayRunning)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F9))
		{
            if (!_isVisible)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = this.jsonCopyText;
            if (GameUI.me)
			{
				GameUI.me.ShowPopup("JSON line copied into clipboard", 0.75f, true);
			}
		}

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (!_isVisible)
            {
                return;
            }

            if (GameUI.me && RemoveLastPoint())
            {
                GameUI.me.ShowPopup("Last point removed", 0.75f, true);
            }
        }

        if (Input.GetKeyDown(KeyCode.Z))
		{
            if (!_isVisible)
            {
                return;
            }

            if (UseVisualPoints)
			{
                Vector3 pos;
                float? yaw = 0f;
                PosParser.TryParsePosFromJson(jsonCopyText, out pos, out yaw);
                if (isInteractive)
                {
                    LegacyEventsConfig.SpawnDebugRaceCheckpoint(pos, true, yaw.GetValueOrDefault());
                }
                else
                {
                    LegacyEventsConfig.SpawnDebugRaceCheckpoint(pos, false, yaw.GetValueOrDefault());
                }
            }

            this.writeToPosOutFile();
			if (GameUI.me)
			{
				GameUI.me.ShowPopup("JSON line saved in pos_out.txt", 0.75f, true);
			}
		}
    }

	private void writeToPosOutFile()
	{
		string file = getPosOutFilePath;
		string dir = Path.GetDirectoryName(file);
		if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
		string text = this.jsonCopyText;
		string lb = ",\n";
		File.AppendAllText(file, string.Empty);

		if (new FileInfo(file).Length != 0)
		{
			File.AppendAllText(file, lb + text);
		}
		else
		{
			File.AppendAllText(file, text);
		}
	}

	private static string getPosOutFilePath
	{
        get
        {
            string path = Path.Combine(LegacyHelpers.ModDataPath, "pos_out.txt");
            return path;
        }
	}

	private static LegacyPointTool instance;

	private string jsonCopyText;

	private bool _isVisible = true;

    private bool _visPointsIsToggled = false;
}
