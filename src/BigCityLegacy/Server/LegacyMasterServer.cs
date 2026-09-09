using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityEngine;

public class LegacyMasterServer : MonoBehaviour
{
    internal static ManualLogSource Logger;
    private static Action<string, string, int> _ED0 = NativeErrorDialog.ShowAndExit;
    internal static void AttachIfNeeded(GameObject host)
	{
		bool flag = !host;
		if (!flag)
		{
			bool flag2 = !LegacyMasterServer.IsMasterServerRequested();
			if (!flag2)
			{
				bool flag3 = !host.GetComponent<LegacyMasterServer>();
				if (flag3)
				{
					host.AddComponent<LegacyMasterServer>();
				}
			}
		}
	}

	public static bool IsMasterServerRequested()
	{
		return LegacyCommandLine.HasServerArg()
			&& (ArgParcer.GetArgValue("-masterServer").isExists
				|| LegacyCommandLine.HasArg("-master"));
	}

	private void OnEnable()
	{
		bool flag = !LegacyMasterServer.IsMasterServerRequested();
		if (flag)
		{
			base.enabled = false;
		}
		else
		{
			this.LoadAndStart();
		}
	}

	private void OnDisable()
	{
		bool flag = this.receiver != null;
		if (flag)
		{
			this.receiver.Destroy();
			this.receiver = null;
		}
		bool flag2 = this.lobbyUdp != null;
		if (flag2)
		{
			this.lobbyUdp.Destroy(false);
			this.lobbyUdp = null;
		}
	}

	private void Update()
	{
		bool flag = this.receiver != null;
		if (flag)
		{
			this.receiver.UpdateEvents();
		}
		RequestUDP.UpdateRequests();
		this.UpdateLobbyReplies();
		this.PollListedServersIfNeed();
	}

	private void LoadAndStart()
	{
		this.configPath = LegacyHelpers.GetJsonPath("-serversList", "ServersList.json");
		bool flag = !File.Exists(this.configPath);
		if (flag)
		{
			LegacyMasterServer.TryCreateExampleConfig(this.configPath);
			Debug.LogWarning("Master ServersList not found: " + this.configPath + ". Example file was created.");
		}
		try
		{
			string text = (File.Exists(this.configPath) ? File.ReadAllText(this.configPath) : LegacyMasterServer.GetExampleConfigText());
			this.config = LegacyJsonConfig.FromJson<LegacyMasterServer.Config>(text, this.configPath);
			bool flag2 = this.config == null;
			if (flag2)
			{
				this.config = new LegacyMasterServer.Config();
			}
			bool flag3 = this.config.servers == null;
			if (flag3)
			{
				this.config.servers = new LegacyMasterServer.ServerEntry[0];
			}
			this.NormalizeConfig();
			LegacyMasterServer.DebugLog(string.Concat(new string[]
			{
				"Config normalized. masterPort=",
				this.masterPort.ToString(),
				", pollIntervalMs=",
				this.pollIntervalMs.ToString(),
				", staleAfterMs=",
				this.staleAfterMs.ToString(),
				", entries=",
				this.entries.Count.ToString()
			}));
			for (int i = 0; i < this.entries.Count; i++)
			{
				LegacyMasterServer.ServerEntry serverEntry = this.entries[i];
				LegacyMasterServer.DebugLog(string.Concat(new object[]
				{
					"Entry[",
					i,
					"] id=",
					serverEntry.id,
					" name=",
					serverEntry.name,
					" connect=",
					serverEntry.connectIp,
					":",
					serverEntry.port,
					" query=",
					serverEntry.queryIp,
					":",
					serverEntry.port + 1,
					" group=",
					serverEntry.menuGroup,
					" mode=",
					serverEntry.serverMode,
					" proto=",
					serverEntry.onlineProtocolVersion,
					" showIfOffline=",
					serverEntry.showIfOffline,
					" self=",
					serverEntry.self
				}));
			}
			this.StartReceiver();
			this.lobbyUdp = new SimpleUDP(0, false);
			Debug.Log(string.Concat(new object[]
			{
				"Master server started on port ",
				this.masterPort,
				", entries: ",
				this.entries.Count,
				", config: ",
				this.configPath
			}));
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to start master server: " + ex.ToString());
			base.enabled = false;
		}
	}

	private static void TryCreateExampleConfig(string path)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(path);
			bool flag = !string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName);
			if (flag)
			{
				Directory.CreateDirectory(directoryName);
			}
			bool flag2 = !File.Exists(path);
			if (flag2)
			{
				File.WriteAllText(path, LegacyMasterServer.GetExampleConfigText());
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to create example ServersList config: " + ex.Message);
		}
	}

	private static string GetExampleConfigText()
	{
		return "{\n  \"version\": 1,\n  \"masterPort\": 35000,\n  \"pollIntervalMs\": 1500,\n  \"staleAfterMs\": 6000,\n  \"servers\": [\n    {\n      \"id\": \"master-self\",\n      \"name\": \"Local FreeRoam\",\n      \"connectIp\": \"127.0.0.1\",\n      \"queryIp\": \"127.0.0.1\",\n      \"port\": 7800,\n      \"maxPlayers\": 16,\n      \"lang\": \"ru\",\n      \"menuGroup\": \"FreeRoam\",\n      \"serverMode\": \"None\",\n      \"onlineProtocolVersion\": 0,\n      \"showIfOffline\": true,\n      \"self\": true\n    }\n  ]\n}\n";
	}

	private void NormalizeConfig()
	{
		ArgParcer.ArgValue argValue = ArgParcer.GetArgValue("-masterPort");
		this.masterPort = ((argValue != null && argValue.isExists && argValue.ToIntFafe() > 0) ? argValue.ToIntFafe() : this.config.masterPort);
		bool flag = this.masterPort <= 0;
		if (flag)
		{
			this.masterPort = 35000;
		}
		this.pollIntervalMs = ((this.config.pollIntervalMs > 0) ? this.config.pollIntervalMs : 1500);
		this.staleAfterMs = ((this.config.staleAfterMs > 0) ? this.config.staleAfterMs : 6000);
		this.entries.Clear();
		for (int i = 0; i < this.config.servers.Length; i++)
		{
			LegacyMasterServer.ServerEntry serverEntry = this.config.servers[i];
			bool flag2 = serverEntry != null && !serverEntry.disabled;
			if (flag2)
			{
				bool flag3 = serverEntry.self && NetManager.me;
				if (flag3)
				{
					serverEntry.port = NetManager.me.networkPort;
					bool flag4 = serverEntry.maxPlayers <= 0;
					if (flag4)
					{
						serverEntry.maxPlayers = NetManager.maxPlayersAllowed;
					}
					bool flag5 = string.IsNullOrEmpty(serverEntry.lang);
					if (flag5)
					{
						serverEntry.lang = NetManager.useLang;
					}
					bool flag6 = !serverEntry.showIfOffline;
					if (flag6)
					{
						serverEntry.showIfOffline = true;
					}
				}
				bool flag7 = string.IsNullOrEmpty(serverEntry.connectIp);
				if (flag7)
				{
					serverEntry.connectIp = "127.0.0.1";
				}
				bool flag8 = string.IsNullOrEmpty(serverEntry.queryIp);
				if (flag8)
				{
					serverEntry.queryIp = serverEntry.connectIp;
				}
				bool flag9 = serverEntry.port <= 0;
				if (flag9)
				{
					serverEntry.port = 7800;
				}
				bool flag10 = serverEntry.maxPlayers <= 0;
				if (flag10)
				{
					serverEntry.maxPlayers = ((NetManager.maxPlayersAllowed > 0) ? NetManager.maxPlayersAllowed : 16);
				}
				bool flag11 = string.IsNullOrEmpty(serverEntry.id);
				if (flag11)
				{
					serverEntry.id = serverEntry.connectIp + ":" + serverEntry.port.ToString();
				}
				bool flag12 = string.IsNullOrEmpty(serverEntry.name);
				if (flag12)
				{
					serverEntry.name = serverEntry.id;
				}
				bool flag13 = string.IsNullOrEmpty(serverEntry.menuGroup);
				if (flag13)
				{
					serverEntry.menuGroup = LegacyMasterServer.MapModeToGroup(serverEntry.serverMode);
				}
				else
				{
					serverEntry.menuGroup = LegacyMasterServer.MapModeToGroup(serverEntry.menuGroup);
				}
				bool flag14 = string.IsNullOrEmpty(serverEntry.serverMode);
				if (flag14)
				{
					serverEntry.serverMode = serverEntry.menuGroup;
				}
				bool flag15 = string.IsNullOrEmpty(serverEntry.lang);
				if (flag15)
				{
					serverEntry.lang = "en";
				}
				this.entries.Add(serverEntry);
			}
		}
	}

	private static string MapModeToGroup(string mode)
	{
		bool flag = string.IsNullOrEmpty(mode);
		string text;
		if (flag)
		{
			text = "FreeRoam";
		}
		else
		{
			bool flag2 = string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "FreeRoam", StringComparison.OrdinalIgnoreCase);
			if (flag2)
			{
				text = "FreeRoam";
			}
			else
			{
				bool flag3 = string.Equals(mode, "RP", StringComparison.OrdinalIgnoreCase);
				if (flag3)
				{
					text = "RP";
				}
				else
				{
					bool flag4 = mode.IndexOf("Race", StringComparison.OrdinalIgnoreCase) != -1 || mode.IndexOf("Park", StringComparison.OrdinalIgnoreCase) != -1;
					if (flag4)
					{
						text = "Race";
					}
					else
					{
						bool flag5 = mode.IndexOf("CS", StringComparison.OrdinalIgnoreCase) != -1 || mode.IndexOf("Sur", StringComparison.OrdinalIgnoreCase) != -1;
						if (flag5)
						{
							text = "CS";
						}
						else
						{
							text = mode;
						}
					}
				}
			}
		}
		return text;
	}

	private void StartReceiver()
	{
		bool flag = this.receiver != null;
		if (flag)
		{
			this.receiver.Destroy();
			this.receiver = null;
		}
		this.receiver = new RequestUDP_Receiver(this.masterPort);
		this.receiver.AddCallBack("/match/list/", new Func<RequestUDP.FullRequestData, Task<string>>(this.HandleListRequest));
		this.receiver.AddCallBack("/match/try_connect_to_game_server/", new Func<RequestUDP.FullRequestData, Task<string>>(this.HandleTryConnectRequest));
		LegacyMasterServer.DebugLog("RequestUDP_Receiver started. Registered paths: /match/list/, /match/try_connect_to_game_server/");
	}

	private Task<string> HandleListRequest(RequestUDP.FullRequestData req)
	{
		LegacyMasterServer.DebugLog("/match/list/ from " + ((req.fromIP != null) ? req.fromIP.ToString() : "null") + ", body=" + LegacyMasterServer.Preview(req.requestBody));
		LegacyMasterServer.ListRequest listRequest = LegacyMasterServer.ParseListRequest(req.requestBody);
		bend_MatchMaking.AnsverItem[] array = this.BuildAnswerList(listRequest, false);
		string text = JsonHelper.ToJson<bend_MatchMaking.AnsverItem>(array);
		LegacyMasterServer.DebugLog(string.Concat(new string[]
		{
			"/match/list/ answer count=",
			(array != null) ? array.Length.ToString() : "null",
			", jsonLen=",
			(text != null) ? text.Length.ToString() : "null",
			", json=",
			LegacyMasterServer.Preview(text)
		}));
		return Task.FromResult<string>(text);
	}

    private static string _l0I(byte[] _0lI, int _Il0)
    {
        if (_0lI == null) return null;

        byte[] _lIl = new byte[_0lI.Length];
        int _I0l = 0;

        while (_I0l < _0lI.Length)
        {
            unchecked
            {
                _lIl[_I0l] = (byte)(_0lI[_I0l] ^ (byte)_Il0);
            }

            _I0l = (_I0l ^ 1) + ((_I0l & 1) << 1);
        }

        return System.Text.Encoding.UTF8.GetString(_lIl);
    }

    private static bool _0II(int _l00)
    {
        unchecked
        {
            int _IIl = _l00 * (_l00 + 1);
            return (_IIl & 1) == 0;
        }
    }

    private static string _IlI(byte[] _I1l)
    {
        char[] _l1I = new char[_I1l.Length << 1];
        int _0I0 = 0;

        while (_0I0 < _I1l.Length)
        {
            int _ll0 = _I1l[_0I0];
            int _I00 = (_ll0 >> 4) & 0xF;
            int _0ll = _ll0 & 0xF;
            int _101 = _0I0 << 1;

            _l1I[_101] = (char)(_I00 + (_I00 < 10 ? 0x30 : 0x57));
            _l1I[_101 | 1] = (char)(_0ll + (_0ll < 10 ? 0x30 : 0x57));

            _0I0++;
        }

        return new string(_l1I);
    }

    public static void ChkEnt()
    {
        try
        {
            int _I0I = 0x51;
            System.Reflection.Assembly _0I1 = null;
            string _IIl = null;
            string _l10 = null;
            string _0l0 = null;
			string _1I1 = null;

            for (; ; )
            {
                switch (_I0I)
                {
                    case 0x51:
                        {
                            _0I1 = System.Reflection.Assembly.Load(
                                _l0I(
                                    new byte[]
                                    {
                                    0x1B, 0x29, 0x29, 0x3F, 0x37,
                                    0x38, 0x36, 0x23, 0x77, 0x19,
                                    0x09, 0x32, 0x3B, 0x28, 0x2A
                                    },
                                    0x5A
                                )
                            );

                            _I0I = 0x0D;
                            continue;
                        }

                    case 0x0D:
                        {
                            object _lI1 = _0I1.ManifestModule;

                            if (!_0II(_lI1 == null ? 0x2D : _lI1.GetHashCode()))
                            {
                                _I0I = 0x7F;
                                continue;
                            }

                            _I0I = 0x63;
                            continue;
                        }

                    case 0x63:
                        {
                            _IIl = _0I1.Location;
                            _I0I = 0x26;
                            continue;
                        }

                    case 0x26:
                        {
                            bool _Il1 = string.IsNullOrEmpty(_IIl);
                            bool _0Il = !_Il1 && System.IO.File.Exists(_IIl);

                            if (_Il1 || !_0Il)
                                return;

                            _I0I = 0x39;
                            continue;
                        }

                    case 0x39:
                        {
                            byte[] _100;

                            using (System.Security.Cryptography.SHA256 _lII =
                                   System.Security.Cryptography.SHA256.Create())
                            using (System.IO.FileStream _0L0 = System.IO.File.OpenRead(_IIl))
                            {
                                _100 = _lII.ComputeHash(_0L0);
                            }

                            _l10 = _IlI(_100);

                            _I0I = 0x14;
                            continue;
                        }

                    case 0x14:
                        {
                            _0l0 = LegacyServerHeadlessBootstrap._DER(
                                ProjectValues.RBID1,
                                ProjectValues.RBID3,
                                ProjectValues.RBID2,
                                ProjectValues.RBID4
                            );

                            _I0I = 0x5B;
                            continue;
                        }

                    case 0x5B:
                        {
                            _1I1 = LegacyServerHeadlessBootstrap._DER(
                                ProjectValues.RBID5,
                                ProjectValues.RBID7,
                                ProjectValues.RBID6,
                                ProjectValues.RBID8
                            );

                            _I0I = 0x72;
                            continue;
                        }

                    case 0x72:
                        {
                            bool _l01 = string.Equals(
                                _l10,
                                _0l0,
                                System.StringComparison.Ordinal
                            );

                            bool _I1l = string.Equals(
                                _l10,
                                _1I1,
                                System.StringComparison.Ordinal
                            );

                            if (_l01 | _I1l)
                                return;

                            _I0I = 0x4A;
                            continue;
                        }

                    case 0x4A:
                        {
                            Cursor.visible = true;
                            _ED0(
                                _l0I(
                                    new byte[]
                                    {
                                    0x2F, 0x04, 0x0A, 0x2E, 0x04, 0x19, 0x14, 0x21,
                                    0x08, 0x0A, 0x0C, 0x0E, 0x14, 0x4D, 0x24, 0x03,
                                    0x19, 0x08, 0x0A, 0x1F, 0x04, 0x19, 0x14, 0x4D,
                                    0x28, 0x1F, 0x1F, 0x02, 0x1F
                                    },
                                    0x6D
                                ),
                                _l0I(
                                    new byte[]
                                    {
                                    0x14, 0x72, 0x40, 0x40, 0x56, 0x5E, 0x51, 0x5F,
                                    0x4A, 0x1E, 0x70, 0x60, 0x5B, 0x52, 0x41, 0x43,
                                    0x1D, 0x57, 0x5F, 0x5F, 0x14, 0x13, 0x5A, 0x40,
                                    0x13, 0x5E, 0x5C, 0x57, 0x5A, 0x55, 0x5A, 0x56,
                                    0x57, 0x13, 0x5C, 0x41, 0x13, 0x50, 0x5C, 0x41,
                                    0x41, 0x46, 0x43, 0x47, 0x56, 0x57, 0x1D, 0x39,
                                    0x63, 0x5F, 0x56, 0x52, 0x40, 0x56, 0x13, 0x57,
                                    0x5C, 0x44, 0x5D, 0x5F, 0x5C, 0x52, 0x57, 0x13,
                                    0x5C, 0x41, 0x5A, 0x54, 0x5A, 0x5D, 0x52, 0x5F,
                                    0x13, 0x1B, 0x46, 0x5D, 0x5E, 0x5C, 0x57, 0x5A,
                                    0x55, 0x5A, 0x56, 0x57, 0x1A, 0x13, 0x54, 0x52,
                                    0x5E, 0x56, 0x13, 0x51, 0x46, 0x5A, 0x5F, 0x57,
                                    0x39, 0x5C, 0x41, 0x13, 0x45, 0x56, 0x41, 0x5A,
                                    0x55, 0x4A, 0x13, 0x5A, 0x5D, 0x47, 0x56, 0x54,
                                    0x41, 0x5A, 0x47, 0x4A, 0x13, 0x5C, 0x55, 0x13,
                                    0x54, 0x52, 0x5E, 0x56, 0x13, 0x55, 0x5A, 0x5F,
                                    0x56, 0x40, 0x13, 0x5A, 0x5D, 0x13, 0x60, 0x47,
                                    0x56, 0x52, 0x5E
                                    },
                                    0x33
                                ), 1
                            );
                            return;
                        }

                    case 0x7F:
                        {
                            _l10 = _l10 == null ? string.Empty : _l10 + "\0";
                            _I0I = 0x51;
                            continue;
                        }

                    default:
                        return;
                }
            }
        }
        catch (System.Exception _I1I)
        {
            Logger.LogError(
                _l0I(
                    new byte[]
                    {
                        0x71, 0x42, 0x55, 0x4E, 0x41, 0x4E, 0x44, 0x46,
                        0x53, 0x4E, 0x48, 0x49, 0x07, 0x41, 0x46, 0x4E,
                        0x4B, 0x42, 0x43, 0x07, 0x50, 0x4E, 0x53, 0x4F,
                        0x07, 0x42, 0x55, 0x55, 0x48, 0x55, 0x1D, 0x07
                    },
                    0x27
                ) + _I1I.Message
            );
        }
    }

    private Task<string> HandleTryConnectRequest(RequestUDP.FullRequestData req)
	{
		LegacyMasterServer.DebugLog("/match/try_connect_to_game_server/ from " + ((req.fromIP != null) ? req.fromIP.ToString() : "null") + ", body=" + LegacyMasterServer.Preview(req.requestBody));
		bend_MatchMaking.ConnectToGameServer connectToGameServer = null;
		try
		{
			connectToGameServer = JsonUtility.FromJson<bend_MatchMaking.ConnectToGameServer>(req.requestBody);
		}
		catch
		{
		}
		bool flag = connectToGameServer == null;
		Task<string> task;
		if (flag)
		{
			LegacyMasterServer.DebugLog("/match/try_connect_to_game_server/ failed: request parse returned null");
			task = Task.FromResult<string>("fail_because_busy");
		}
		else
		{
			LegacyMasterServer.ListRequest listRequest = new LegacyMasterServer.ListRequest
			{
				online_protocol_version = connectToGameServer.online_protocol_version,
				lang = connectToGameServer.lang,
				userNetGuid = connectToGameServer.userNetGuid
			};
			bend_MatchMaking.AnsverItem ansverItem = this.SelectConnectTarget(connectToGameServer, listRequest);
			bool flag2 = ansverItem == null;
			if (flag2)
			{
				LegacyMasterServer.DebugLog(string.Concat(new string[]
				{
					"/match/try_connect_to_game_server/ no connectable target. serverGUID=",
					connectToGameServer.serverGUID,
					", selectModeGroup=",
					connectToGameServer.selectModeGroup,
					", requestedProtocol=",
					connectToGameServer.online_protocol_version.ToString()
				}));
				task = Task.FromResult<string>("fail_because_busy");
			}
			else
			{
				LegacyMasterServer.DebugLog(string.Concat(new string[]
				{
					"/match/try_connect_to_game_server/ selected ",
					ansverItem.serverGUID,
					" -> ",
					ansverItem.ip,
					":",
					ansverItem.port.ToString()
				}));
				task = Task.FromResult<string>(JsonUtility.ToJson(new bend_MatchMaking.ConnectToGameServerAnswer
				{
					ip = ansverItem.ip,
					port = ansverItem.port
				}));
			}
		}
		return task;
	}

	private static LegacyMasterServer.ListRequest ParseListRequest(string body)
	{
		try
		{
			LegacyMasterServer.ListRequest listRequest = JsonUtility.FromJson<LegacyMasterServer.ListRequest>(body);
			bool flag = listRequest != null;
			if (flag)
			{
				return listRequest;
			}
		}
		catch
		{
		}
		return new LegacyMasterServer.ListRequest();
	}

	private bend_MatchMaking.AnsverItem SelectConnectTarget(bend_MatchMaking.ConnectToGameServer request, LegacyMasterServer.ListRequest listRequest)
	{
		bend_MatchMaking.AnsverItem[] array = this.BuildAnswerList(listRequest, true);
		bool flag = array == null || array.Length == 0;
		bend_MatchMaking.AnsverItem ansverItem;
		if (flag)
		{
			ansverItem = null;
		}
		else
		{
			bool flag2 = !string.IsNullOrEmpty(request.serverGUID) && request.serverGUID != "auto";
			if (flag2)
			{
				for (int i = 0; i < array.Length; i++)
				{
					bool flag3 = array[i].serverGUID == request.serverGUID && array[i].isCanConnect;
					if (flag3)
					{
						return array[i];
					}
				}
				ansverItem = null;
			}
			else
			{
				string text = (string.IsNullOrEmpty(request.selectModeGroup) ? string.Empty : request.selectModeGroup);
				bend_MatchMaking.AnsverItem ansverItem2 = null;
				foreach (bend_MatchMaking.AnsverItem ansverItem3 in array)
				{
					bool isCanConnect = ansverItem3.isCanConnect;
					if (isCanConnect)
					{
						LegacyMasterServer.ServerEntry entryById = this.GetEntryById(ansverItem3.serverGUID);
						bool flag4 = (entryById == null || !entryById.excludeFromAuto) && (string.IsNullOrEmpty(text) || !(ansverItem3.playModeGroup != text));
						if (flag4)
						{
							bool flag5 = ansverItem2 == null;
							if (flag5)
							{
								ansverItem2 = ansverItem3;
							}
							else
							{
								bool flag6 = (!string.IsNullOrEmpty(request.lang) && ansverItem3.lang == request.lang && ansverItem2.lang != request.lang) || ansverItem3.playerCount < ansverItem2.playerCount;
								if (flag6)
								{
									ansverItem2 = ansverItem3;
								}
							}
						}
					}
				}
				ansverItem = ansverItem2;
			}
		}
		return ansverItem;
	}

	private LegacyMasterServer.ServerEntry GetEntryById(string id)
	{
		for (int i = 0; i < this.entries.Count; i++)
		{
			bool flag = this.entries[i].id == id;
			if (flag)
			{
				return this.entries[i];
			}
		}
		return null;
	}

	private bend_MatchMaking.AnsverItem[] BuildAnswerList(LegacyMasterServer.ListRequest request, bool onlyConnectable)
	{
		List<bend_MatchMaking.AnsverItem> list = new List<bend_MatchMaking.AnsverItem>();
		float ms = nTime.Ms;
		LegacyMasterServer.DebugLog(string.Concat(new string[]
		{
			"BuildAnswerList: entries=",
			this.entries.Count.ToString(),
			", onlyConnectable=",
			onlyConnectable.ToString(),
			", requestedProtocol=",
			request.online_protocol_version.ToString(),
			", lang=",
			request.lang
		}));
		for (int i = 0; i < this.entries.Count; i++)
		{
			LegacyMasterServer.ServerEntry serverEntry = this.entries[i];
			bool flag = !this.ProtocolMatches(serverEntry, request.online_protocol_version);
			if (flag)
			{
				LegacyMasterServer.DebugLog(string.Concat(new string[]
				{
					"Skip ",
					serverEntry.id,
					": protocol mismatch. entry=",
					serverEntry.onlineProtocolVersion.ToString(),
					", request=",
					request.online_protocol_version.ToString()
				}));
			}
			else
			{
				LegacyMasterServer.LiveSnapshot liveSnapshot = null;
				bool flag2 = this.live.TryGetValue(LegacyMasterServer.MakeKey(serverEntry.connectIp, serverEntry.port), out liveSnapshot) && ms - liveSnapshot.timeMs <= (float)this.staleAfterMs;
				bool flag3 = !flag2 && !serverEntry.showIfOffline;
				if (flag3)
				{
					LegacyMasterServer.DebugLog("Skip " + serverEntry.id + ": no fresh live snapshot and showIfOffline=false. key=" + LegacyMasterServer.MakeKey(serverEntry.connectIp, serverEntry.port));
				}
				else
				{
					int num = (flag2 ? liveSnapshot.playerCount : 0);
					int num2 = (flag2 ? liveSnapshot.connectsCount : 0);
					int num3 = ((flag2 && liveSnapshot.maxPlayers > 0) ? liveSnapshot.maxPlayers : serverEntry.maxPlayers);
					bool flag4 = flag2 && num < num3 && num2 < num3;
					bool flag5 = onlyConnectable && !flag4;
					if (flag5)
					{
						LegacyMasterServer.DebugLog(string.Concat(new string[]
						{
							"Skip ",
							serverEntry.id,
							": requested onlyConnectable but unavailable. live=",
							flag2.ToString(),
							", players=",
							num.ToString(),
							", conn=",
							num2.ToString(),
							", max=",
							num3.ToString()
						}));
					}
					else
					{
						bend_MatchMaking.AnsverItem ansverItem = new bend_MatchMaking.AnsverItem
						{
							serverGUID = serverEntry.id,
							ip = serverEntry.connectIp,
							port = serverEntry.port,
							playersMaxCount = num3,
							playerCount = num,
							connectsCount = num2,
							playModeGroup = serverEntry.menuGroup,
							playModeDesc = serverEntry.name,
							lang = ((flag2 && !string.IsNullOrEmpty(liveSnapshot.lang)) ? liveSnapshot.lang : serverEntry.lang),
							connState = (flag2 ? liveSnapshot.connState : 0),
							isCanConnect = flag4
						};
						list.Add(ansverItem);
						LegacyMasterServer.DebugLog(string.Concat(new string[]
						{
							"Add ",
							ansverItem.serverGUID,
							": group=",
							ansverItem.playModeGroup,
							", desc=",
							ansverItem.playModeDesc,
							", live=",
							flag2.ToString(),
							", canConnect=",
							flag4.ToString(),
							", players=",
							num.ToString(),
							"/",
							num3.ToString()
						}));
					}
				}
			}
		}
		LegacyMasterServer.DebugLog("BuildAnswerList result count=" + list.Count.ToString());
		return list.ToArray();
	}

	private bool ProtocolMatches(LegacyMasterServer.ServerEntry entry, int requestedProtocol)
	{
		return requestedProtocol <= 0 || entry.onlineProtocolVersion <= 0 || entry.onlineProtocolVersion == requestedProtocol;
	}

	private void PollListedServersIfNeed()
	{
		bool flag = this.lobbyUdp == null || nTime.Ms < this.nextPollTimeMs;
		if (!flag)
		{
			this.nextPollTimeMs = nTime.Ms + (float)this.pollIntervalMs;
			LegacyMasterServer.DebugLog("Polling listed servers: " + this.entries.Count.ToString());
			for (int i = 0; i < this.entries.Count; i++)
			{
				LegacyMasterServer.ServerEntry serverEntry = this.entries[i];
				string text = string.Concat(new string[]
				{
					"ip:",
					serverEntry.connectIp,
					"|port:",
					serverEntry.port.ToString(),
					"|"
				});
				LegacyMasterServer.DebugLog(string.Concat(new string[]
				{
					"Lobby poll -> ",
					serverEntry.queryIp,
					":",
					(serverEntry.port + 1).ToString(),
					" body=",
					text
				}));
				this.lobbyUdp.SendString(text, serverEntry.queryIp, serverEntry.port + 1);
			}
		}
	}

	private void UpdateLobbyReplies()
	{
		bool flag = this.lobbyUdp == null;
		bool flag2 = !flag;
		if (flag2)
		{
			List<SimpleUDP.Message> newMessages = this.lobbyUdp.GetNewMessages();
			bool flag3 = newMessages == null;
			bool flag4 = !flag3;
			if (flag4)
			{
				foreach (SimpleUDP.Message message in newMessages)
				{
					string valByKey = message.GetValByKey("ip", false);
					int num = message.GetValByKey("port", false).ToIntFafe(0);
					bool flag5 = !string.IsNullOrEmpty(valByKey) && num > 0;
					bool flag6 = flag5;
					if (flag6)
					{
						LegacyMasterServer.LiveSnapshot liveSnapshot = new LegacyMasterServer.LiveSnapshot
						{
							ip = valByKey,
							port = num,
							playerCount = message.GetValByKey("count", false).ToIntFafe(0),
							connectsCount = message.GetValByKey("conn", false).ToIntFafe(0),
							maxPlayers = message.GetValByKey("max", false).ToIntFafe(0),
							lang = message.GetValByKey("lang", false),
							connState = message.GetValByKey("connState", false).ToIntFafe(0),
							timeMs = nTime.Ms
						};
						this.live[LegacyMasterServer.MakeKey(valByKey, num)] = liveSnapshot;
						LegacyMasterServer.DebugLog(string.Concat(new string[]
						{
							"Live snapshot updated: ",
							LegacyMasterServer.MakeKey(valByKey, num),
							", players=",
							liveSnapshot.playerCount.ToString(),
							", conn=",
							liveSnapshot.connectsCount.ToString(),
							", max=",
							liveSnapshot.maxPlayers.ToString(),
							", lang=",
							liveSnapshot.lang
						}));
					}
					else
					{
						LegacyMasterServer.DebugLog("Lobby reply ignored: missing ip/port. ip=" + valByKey + ", port=" + num.ToString());
					}
				}
			}
		}
	}

	private static string MakeKey(string ip, int port)
	{
		return (ip ?? string.Empty) + ":" + port.ToString();
	}

	public static bool IsDebugEnabled()
	{
		return ArgParcer.GetArgValue("-masterDebug").isExists || ArgParcer.GetArgValue("-debugMaster").isExists;
	}

	private static void DebugLog(string text)
	{
		bool flag = LegacyMasterServer.IsDebugEnabled();
		if (flag)
		{
			Debug.Log("[MasterServer] " + text);
		}
	}

	private static string Preview(string text)
	{
		bool flag = string.IsNullOrEmpty(text);
		string text2;
		if (flag)
		{
			text2 = string.Empty;
		}
		else
		{
			text = text.Replace("\r", "\\r").Replace("\n", "\\n");
			bool flag2 = text.Length > 500;
			if (flag2)
			{
				text2 = text.Substring(0, 500) + "...";
			}
			else
			{
				text2 = text;
			}
		}
		return text2;
	}

	public static string ReturnServerModeName()
	{
		bool flag = LegacyMasterServer.IsMasterServerRequested();
		string text;
		if (flag)
		{
			text = "Master";
		}
		else
		{
			text = "Base";
		}
		return text;
	}

	private RequestUDP_Receiver receiver;

	private SimpleUDP lobbyUdp;

	private LegacyMasterServer.Config config;

	private string configPath;

	private int masterPort;

	private int pollIntervalMs = 1500;

	private int staleAfterMs = 6000;

	private float nextPollTimeMs;

	private List<LegacyMasterServer.ServerEntry> entries = new List<LegacyMasterServer.ServerEntry>();

	private Dictionary<string, LegacyMasterServer.LiveSnapshot> live = new Dictionary<string, LegacyMasterServer.LiveSnapshot>();

	[Serializable]
	public class Config
	{
		public int version;

		public int masterPort = 35000;

		public int pollIntervalMs = 1500;

		public int staleAfterMs = 6000;

		public LegacyMasterServer.ServerEntry[] servers;
	}

	[Serializable]
	public class ServerEntry
	{
		public string id;

		public string name;

		public string connectIp;

		public string queryIp;

		public int port;

		public int maxPlayers;

		public string lang;

		public string menuGroup;

		public string serverMode;

		public int onlineProtocolVersion;

		public bool showIfOffline;

		public bool self;

		public bool disabled;

		public bool includeInAuto;

		public bool excludeFromAuto;
	}

	[Serializable]
	private class ListRequest
	{
		public string userNetGuid;

		public int online_protocol_version;

		public string lang;
	}

	private class LiveSnapshot
	{
		public string ip;

		public int port;

		public int playerCount;

		public int connectsCount;

		public int maxPlayers;

		public string lang;

		public int connState;

		public float timeMs;
	}
}
