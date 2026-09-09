using BigCityLegacy.UI;
using UnityEngine;

internal sealed class LegacyDirectConnectGui : OnGuiGlobal.IOnGuiNeed
{
    private readonly NetUI_ClinetChoiseServer owner;
    private readonly LegacyUIWindow window;
    private string ip;
    private string port;
    private string status;
    private string masterHost;
    private string masterPort;
    private bool autoMaster;
    private int tab;

    public LegacyDirectConnectGui(NetUI_ClinetChoiseServer owner)
    {
        this.owner = owner;

        ip = PlayerPrefs.GetString("DirectConnect_IP", "127.0.0.1");
        port = PlayerPrefs.GetString("DirectConnect_Port", "7800");
        masterHost = LegacyMasterClient.MasterHost;
        masterPort = LegacyMasterClient.MasterPort.ToString();
        autoMaster = PlayerPrefs.GetInt("ModMaster_AutoResolve", 1) == 1;
        tab = LegacyMasterClient.IsMasterListEnabled() ? 0 : 1;
        status = string.Empty;

        if (!string.IsNullOrEmpty(LegacyCommandLine.ConnectIP)) ip = LegacyCommandLine.ConnectIP;
        if (LegacyCommandLine.ConnectPort > 0) port = LegacyCommandLine.ConnectPort.ToString();

        window = new LegacyUIWindow(
            "Server Connection",
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
                RightHint = "",
                PlayerPrefsKey = "DirectConnect.Window"
            });

        window.Visible = PlayerPrefs.GetInt("DirectConnect_Visible", 1) == 1;
    }

    public bool isNeedOnGUI() => owner != null && owner.isActiveAndEnabled && owner.gameObject.activeInHierarchy;

    public void OnGUI_Manual()
    {
        using (new LegacyUIGuiScope(-10000))
        {
            if (window.Visible)
            {
                window.Draw(DrawContent);
            }
            else
            {
                GUIStyle showBtnStyle = new GUIStyle(LegacyUI.Styles.GreenButton);
                showBtnStyle.fontSize = 16;

                Rect showBtnRect = new Rect(Screen.width - 180f - 10f, Screen.height - 35f - 10f, 180f, 35f);
                if (GUI.Button(showBtnRect, "Connection menu", showBtnStyle))
                {
                    SetVisible(!window.Visible);
                };
            }
        }
    }

    private void SetVisible(bool visible)
    {
        window.Visible = visible;
        PlayerPrefs.SetInt("DirectConnect_Visible", visible ? 1 : 0);
        window.SavePrefs();
        PlayerPrefs.Save();
    }

    private Rect GetStartRect()
    {
        if (PlayerPrefs.HasKey("DirectConnect_WindowX") && PlayerPrefs.HasKey("DirectConnect_WindowY"))
            return new Rect(PlayerPrefs.GetFloat("DirectConnect_WindowX", 230f), PlayerPrefs.GetFloat("DirectConnect_WindowY", 90f), 345f, 225f);

        return (Screen.width <= 0 || Screen.height <= 0)
            ? new Rect(230f, 90f, 345f, 225f)
            : new Rect((Screen.width - 345f) * 0.5f, (Screen.height - 225f) * 0.5f, 345f, 225f);
    }

    private void DrawContent(Rect content)
    {
        float x = content.x;
        float y = content.y;
        float tabW = content.width / 2f;

        if (LegacyUI.TabButton(new Rect(x, y, tabW, 25f), "Master Server", tab == 0)) tab = 0;
        if (LegacyUI.TabButton(new Rect(x + tabW, y, tabW, 25f), "Direct Connect", tab == 1)) tab = 1;

        y += 40f;
        DrawTabContent(content, x, y);
    }

    private void DrawTabContent(Rect content, float x, float y)
    {
        float inputW = content.width - 95f;
        bool isMaster = tab == 0;

        LegacyUI.Label(new Rect(x, y, 90f, 24f), isMaster ? "Master Host" : "Server IP");
        string hostInput = LegacyUI.TextField(new Rect(x + 95f, y, inputW, 24f), (isMaster ? masterHost : ip) ?? string.Empty);
        if (isMaster) masterHost = hostInput; else ip = hostInput;

        y += 30f;
        LegacyUI.Label(new Rect(x, y, 90f, 24f), isMaster ? "Master Port" : "Server Port");
        string portInput = LegacyUI.TextField(new Rect(x + 95f, y, 70f, 24f), (isMaster ? masterPort : port) ?? string.Empty);
        if (isMaster) masterPort = portInput; else port = portInput;

        y += 32f;

        if (!string.IsNullOrEmpty(status))
            LegacyUI.Status(new Rect(x, y, content.width, 18f), status);

        Rect win = window.Rect;
        float btnY = win.y + win.height - 40f;
        float btnW = (win.width - 40f) / 2f;

        if (LegacyUI.GreenButton(new Rect(x, btnY, btnW, 26f), isMaster ? "Resolve List" : "Connect"))
        {
            if (isMaster) ResolveMaster(); else ConnectDirect();
        }

        if (LegacyUI.Button(new Rect(x + btnW + 10f, btnY, btnW, 26f), isMaster ? "Set Local Master" : "Set Localhost"))
        {
            if (isMaster)
            {
                masterHost = "127.0.0.1";
                masterPort = "35000";
                status = "Local master selected";
            }
            else
            {
                ip = "127.0.0.1";
                port = "7800";
                status = "Localhost selected";
            }
        }
    }

    private void ConnectDirect()
    {
        string ipText = (ip ?? string.Empty).Trim();
        string portText = (port ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ipText) || ipText.Contains(" ")) { status = string.IsNullOrEmpty(ipText) ? "IP is empty" : "IP contains spaces"; return; }
        if (!int.TryParse(portText, out int p) || p < 1 || p > 65535) { status = "Bad port"; return; }

        LegacyCommandLine.ConnectIP = ipText;
        LegacyCommandLine.ConnectPort = p;
        PlayerPrefs.SetString("DirectConnect_IP", ipText);
        PlayerPrefs.SetString("DirectConnect_Port", portText);
        PlayerPrefs.Save();
        LegacyMasterClient.SetMaster(masterHost, GetMasterPortValue(), autoMaster, false);
        NetUIPatches.RefreshServerListNow(owner);

        if (NetManager.me == null) { status = "NetManager is null"; return; }
        status = $"Connecting to {ipText}:{p}";
        NetManager.me.ConnectTo(ipText, p);
    }

    private void ResolveMaster()
    {
        int p = GetMasterPortValue();
        string host = (masterHost ?? string.Empty).Trim();
        if (p <= 0 || string.IsNullOrEmpty(host)) { status = p <= 0 ? "Bad master port" : "Master host is empty"; return; }

        LegacyMasterClient.SetMaster(host, p, autoMaster, true);
        if (LegacyMasterClient.GetMasterEndPoint() == null) { status = "Failed to resolve master"; return; }

        status = $"Resolving list from {host}:{p}";
        NetUIPatches.RefreshServerListNow(owner);
    }

    private int GetMasterPortValue() => int.TryParse((masterPort ?? string.Empty).Trim(), out int p) && p >= 1 && p <= 65535 ? p : 0;
}
