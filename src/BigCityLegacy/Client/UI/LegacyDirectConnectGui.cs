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
    private bool followOfficialAddress;
    private bool autoMaster;
    private int tab;

    public LegacyDirectConnectGui(NetUI_ClinetChoiseServer owner)
    {
        this.owner = owner;

        ip = PlayerPrefs.GetString("DirectConnect_IP", "127.0.0.1");
        port = PlayerPrefs.GetString("DirectConnect_Port", "7800");
        masterHost = LegacyMasterClient.MasterHost;
        masterPort = LegacyMasterClient.MasterPort.ToString();
        followOfficialAddress = LegacyMasterClient.IsOfficialSelected;
        autoMaster = LegacyMasterClient.StartupMode != 0;
        tab = LegacyMasterClient.IsMasterListEnabled() ? 0 : 1;
        status = string.Empty;

        if (!string.IsNullOrEmpty(LegacyCommandLine.ConnectIP)) ip = LegacyCommandLine.ConnectIP;
        if (LegacyCommandLine.ConnectPort > 0) port = LegacyCommandLine.ConnectPort.ToString();

        window = new LegacyUIWindow(
            LegacyLocalizer.Text("Server Connection", "Подключение к серверу"),
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
                showBtnStyle.fontSize = 18;
                
                float showBtnW = 220f;
                float showBtnH = 37f;

                Vector2 screenSize = LegacyUI.VirtualScreenSize;
                Rect showBtnRect = new Rect(screenSize.x - showBtnW - 15f, screenSize.y - showBtnH - 15f, showBtnW, showBtnH);
                if (GUI.Button(showBtnRect, LegacyLocalizer.Text("Connection menu", "Меню подключения"), showBtnStyle))
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
        const float width = 390f;
        if (PlayerPrefs.HasKey("DirectConnect_WindowX") && PlayerPrefs.HasKey("DirectConnect_WindowY"))
            return new Rect(PlayerPrefs.GetFloat("DirectConnect_WindowX", 230f), PlayerPrefs.GetFloat("DirectConnect_WindowY", 90f), width, 225f);

        return (Screen.width <= 0 || Screen.height <= 0)
            ? new Rect(230f, 90f, width, 225f)
            : new Rect((Screen.width - width) * 0.5f, (Screen.height - 225f) * 0.5f, width, 225f);
    }

    private void DrawContent(Rect content)
    {
        float x = content.x;
        float y = content.y;
        float tabW = content.width / 2f;

        if (LegacyUI.TabButton(new Rect(x, y, tabW, 25f), LegacyLocalizer.Text("Master Server", "Подключение к Master"), tab == 0)) tab = 0;
        if (LegacyUI.TabButton(new Rect(x + tabW, y, tabW, 25f), LegacyLocalizer.Text("Direct Connect", "Прямое подключение"), tab == 1)) tab = 1;

        y += 40f;
        DrawTabContent(content, x, y);
    }

    private void DrawTabContent(Rect content, float x, float y)
    {
        if (followOfficialAddress && LegacyMasterClient.IsOfficialSelected)
        {
            masterHost = LegacyMasterClient.MasterHost;
            masterPort = LegacyMasterClient.MasterPort.ToString();
        }
        float inputW = content.width - 95f;
        bool isMaster = tab == 0;

        LegacyUI.Label(new Rect(x, y, 90f, 24f), isMaster ? "Master Host" : "Server IP");
        string hostInput = LegacyUI.TextField(new Rect(x + 95f, y, inputW, 24f), (isMaster ? masterHost : ip) ?? string.Empty);
        bool masterEdited = isMaster && masterHost != hostInput;
        if (isMaster) masterHost = hostInput; else ip = hostInput;

        y += 30f;
        LegacyUI.Label(new Rect(x, y, 90f, 24f), isMaster ? "Master Port" : "Server Port");
        string portInput = LegacyUI.TextField(new Rect(x + 95f, y, 70f, 24f), (isMaster ? masterPort : port) ?? string.Empty);
        masterEdited |= isMaster && masterPort != portInput;
        if (isMaster) masterPort = portInput; else port = portInput;
        if (masterEdited)
        {
            followOfficialAddress = false;
            LegacyMasterClient.RememberMaster(masterHost, GetMasterPortValue());
        }

        y += 32f;

        if (!string.IsNullOrEmpty(status))
            LegacyUI.Status(new Rect(x, y, content.width, 18f), status);

        Rect win = window.Rect;
        float btnY = win.y + win.height - 40f;
        float btnW = (content.width - (isMaster ? 20f : 10f)) / (isMaster ? 3f : 2f);
        float mirrorResolveW = 119f;
        float mirrorBtnW = 93f;

        if (LegacyUI.GreenButton(new Rect(x, btnY, (isMaster ? mirrorResolveW : btnW), 26f), isMaster ? LegacyLocalizer.Text("Resolve List", "Получить список") : LegacyLocalizer.Text("Connect", "Подключиться")))
        {
            if (isMaster) ResolveMaster(); else ConnectDirect();
        }

        if (LegacyUI.Button(new Rect(isMaster ? x + mirrorResolveW + 5f : (x + btnW + 10f), btnY, (isMaster ? mirrorBtnW : btnW), 26f), "Localhost"))
        {
            if (isMaster)
            {
                masterHost = "127.0.0.1";
                followOfficialAddress = false;
                masterPort = "35000";
                LegacyMasterClient.RememberMaster(masterHost, 35000);
                status = LegacyLocalizer.Text("Local master selected", "Выбран локальный Master");
            }
            else
            {
                ip = "127.0.0.1";
                port = "7800";
                status = LegacyLocalizer.Text("Localhost selected", "Выбран localhost");
            }
        }
        if (isMaster && LegacyUI.Button(new Rect(x + mirrorResolveW + mirrorBtnW + 10f, btnY, mirrorBtnW, 26f),
            LegacyLocalizer.Text("Official", "Официальный")))
        {
            LegacyMasterClient.UseOfficial();
            followOfficialAddress = true;
            masterHost = LegacyMasterClient.MasterHost;
            masterPort = LegacyMasterClient.MasterPort.ToString();
            status = LegacyLocalizer.Text("Official mirror selected", "Выбран официальный mirror");
            NetUIPatches.RefreshServerListNow(owner);
        }
    }

    private void ConnectDirect()
    {
        string ipText = (ip ?? string.Empty).Trim();
        string portText = (port ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ipText) || ipText.Contains(" ")) { status = string.IsNullOrEmpty(ipText) ? LegacyLocalizer.Text("IP is empty", "IP не заполнен") : LegacyLocalizer.Text("IP contains spaces", "IP содержит пробелы"); return; }
        if (!int.TryParse(portText, out int p) || p < 1 || p > 65535) { status = LegacyLocalizer.Text("Invalid port", "Port указан неверно"); return; }

        LegacyCommandLine.ConnectIP = ipText;
        LegacyCommandLine.ConnectPort = p;
        PlayerPrefs.SetString("DirectConnect_IP", ipText);
        PlayerPrefs.SetString("DirectConnect_Port", portText);
        PlayerPrefs.Save();
        LegacyMasterClient.UseDirectConnection();
        NetUIPatches.RefreshServerListNow(owner);

        if (NetManager.me == null) { status = "NetManager is null"; return; }
        status = $"{LegacyLocalizer.Text("Connecting to", "Подключение к")} {ipText}:{p}";
        NetManager.me.ConnectTo(ipText, p);
    }

    private void ResolveMaster()
    {
        int p = GetMasterPortValue();
        string host = (masterHost ?? string.Empty).Trim();
        if (p <= 0 || string.IsNullOrEmpty(host)) { status = p <= 0 ? LegacyLocalizer.Text("Invalid master port", "Master Port указан неверно") : LegacyLocalizer.Text("Master host is empty", "Master host не заполнен"); return; }

        LegacyMasterClient.SetMaster(host, p, autoMaster, true);
        if (LegacyMasterClient.GetMasterEndPoint() == null) { status = LegacyLocalizer.Text("Failed to resolve master", "Не удалось получить список"); return; }

        status = $"{LegacyLocalizer.Text("Resolving list from", "Получение списка с")} {host}:{p}";
        NetUIPatches.RefreshServerListNow(owner);
    }

    private int GetMasterPortValue() => int.TryParse((masterPort ?? string.Empty).Trim(), out int p) && p >= 1 && p <= 65535 ? p : 0;
}
