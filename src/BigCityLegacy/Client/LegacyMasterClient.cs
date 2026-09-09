using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class LegacyMasterClient
{
	internal static void LoadFromPrefsAndArgs()
	{
		if (LegacyMasterClient.loaded)
		{
			return;
		}
		LegacyMasterClient.loaded = true;
		LegacyMasterClient.masterHost = PlayerPrefs.GetString("ModMaster_Host", "127.0.0.1");
		LegacyMasterClient.masterPort = PlayerPrefs.GetInt("ModMaster_Port", 35000);
		LegacyMasterClient.autoResolve = PlayerPrefs.GetInt("ModMaster_AutoResolve", 1) == 1;
		LegacyMasterClient.useMasterList = LegacyMasterClient.autoResolve;
		ArgParcer.ArgValue argValue = ArgParcer.GetArgValue("-masterHost");
		if (argValue != null && argValue.isExists && !string.IsNullOrEmpty(argValue.strValue))
		{
			LegacyMasterClient.masterHost = argValue.strValue;
			LegacyMasterClient.useMasterList = true;
		}
		argValue = ArgParcer.GetArgValue("-masterIP");
		if (argValue != null && argValue.isExists && !string.IsNullOrEmpty(argValue.strValue))
		{
			LegacyMasterClient.masterHost = argValue.strValue;
			LegacyMasterClient.useMasterList = true;
		}
		argValue = ArgParcer.GetArgValue("-masterPort");
		if (argValue != null && argValue.isExists && argValue.ToIntFafe() > 0)
		{
			LegacyMasterClient.masterPort = argValue.ToIntFafe();
			LegacyMasterClient.useMasterList = true;
		}
		if (ArgParcer.GetArgValue("-useMaster").isExists)
		{
			LegacyMasterClient.useMasterList = true;
		}
		LegacyMasterClient.DebugLog(string.Concat(new string[]
		{
			"Loaded settings: host=",
			LegacyMasterClient.masterHost,
			", port=",
			LegacyMasterClient.masterPort.ToString(),
			", autoResolve=",
			LegacyMasterClient.autoResolve.ToString(),
			", useMasterList=",
			LegacyMasterClient.useMasterList.ToString()
		}));
	}

	internal static bool IsMasterListEnabled()
	{
		LegacyMasterClient.LoadFromPrefsAndArgs();
		return LegacyMasterClient.useMasterList;
	}

	internal static void SetMaster(string host, int port, bool auto, bool useNow)
	{
		LegacyMasterClient.LoadFromPrefsAndArgs();
		LegacyMasterClient.masterHost = (host ?? string.Empty).Trim();
		LegacyMasterClient.masterPort = port;
		LegacyMasterClient.autoResolve = auto;
		LegacyMasterClient.useMasterList = useNow;
		LegacyMasterClient.cachedEndPoint = null;
		PlayerPrefs.SetString("ModMaster_Host", LegacyMasterClient.masterHost);
		PlayerPrefs.SetInt("ModMaster_Port", LegacyMasterClient.masterPort);
		PlayerPrefs.SetInt("ModMaster_AutoResolve", LegacyMasterClient.autoResolve ? 1 : 0);
		PlayerPrefs.Save();
		LegacyMasterClient.DebugLog(string.Concat(new string[]
		{
			"SetMaster: host=",
			LegacyMasterClient.masterHost,
			", port=",
			LegacyMasterClient.masterPort.ToString(),
			", auto=",
			auto.ToString(),
			", useNow=",
			useNow.ToString()
		}));
	}

	internal static string MasterHost
	{
		get
		{
			LegacyMasterClient.LoadFromPrefsAndArgs();
			return LegacyMasterClient.masterHost;
		}
	}

	internal static int MasterPort
	{
		get
		{
			LegacyMasterClient.LoadFromPrefsAndArgs();
			return LegacyMasterClient.masterPort;
		}
	}

	internal static bool AutoResolve
	{
		get
		{
			LegacyMasterClient.LoadFromPrefsAndArgs();
			return LegacyMasterClient.autoResolve;
		}
	}

	internal static IPEndPoint GetMasterEndPoint()
	{
		LegacyMasterClient.LoadFromPrefsAndArgs();
		if (LegacyMasterClient.cachedEndPoint != null)
		{
			return LegacyMasterClient.cachedEndPoint;
		}
		if (string.IsNullOrEmpty(LegacyMasterClient.masterHost) || LegacyMasterClient.masterPort <= 0)
		{
			LegacyMasterClient.DebugLog("GetMasterEndPoint failed: empty host or invalid port. host=" + LegacyMasterClient.masterHost + ", port=" + LegacyMasterClient.masterPort.ToString());
			return null;
		}
		IPAddress ipaddress;
		if (IPAddress.TryParse(LegacyMasterClient.masterHost, out ipaddress))
		{
			LegacyMasterClient.cachedEndPoint = new IPEndPoint(ipaddress, LegacyMasterClient.masterPort);
			LegacyMasterClient.DebugLog("Master endpoint: " + LegacyMasterClient.cachedEndPoint.ToString());
			return LegacyMasterClient.cachedEndPoint;
		}
		try
		{
			IPHostEntry hostEntry = Dns.GetHostEntry(LegacyMasterClient.masterHost);
			for (int i = 0; i < hostEntry.AddressList.Length; i++)
			{
				if (hostEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
				{
					LegacyMasterClient.cachedEndPoint = new IPEndPoint(hostEntry.AddressList[i], LegacyMasterClient.masterPort);
					LegacyMasterClient.DebugLog("Master endpoint resolved: " + LegacyMasterClient.masterHost + " -> " + LegacyMasterClient.cachedEndPoint.ToString());
					return LegacyMasterClient.cachedEndPoint;
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to resolve master host: " + LegacyMasterClient.masterHost + ", " + ex.Message);
		}
		return null;
	}

	internal static void ResetListRequestTime()
	{
		IPEndPoint masterEndPoint = LegacyMasterClient.GetMasterEndPoint();
		if (masterEndPoint != null)
		{
			RequestUDP.ResetRequestTime(masterEndPoint, "/match/list/");
		}
	}

	internal static async Task<bend_MatchMaking.AnsverItem[]> RequestServerList()
	{
		IPEndPoint masterEndPoint = LegacyMasterClient.GetMasterEndPoint();
		bend_MatchMaking.AnsverItem[] array;
		if (masterEndPoint == null)
		{
			LegacyMasterClient.DebugLog("RequestServerList aborted: masterEndPoint is null");
			array = null;
		}
		else if (RequestUDP.isInRequest(masterEndPoint, "/match/list/", 2000f))
		{
			LegacyMasterClient.DebugLog("RequestServerList skipped: same request is active or throttled for " + masterEndPoint.ToString());
			array = null;
		}
		else
		{
			LegacyMasterClient.ListRequest listRequest = new LegacyMasterClient.ListRequest
			{
				online_protocol_version = AlwaysOnline.onlineVersion,
				userNetGuid = NetConnect.GetPrivateID(),
				lang = Application.systemLanguage.ToString()
			};
			string text = JsonUtility.ToJson(listRequest);
			LegacyMasterClient.DebugLog("RequestServerList -> " + masterEndPoint.ToString() + ", body=" + text);
			RequestUDP requestUDP = await RequestUDP.AsyncRequestObj2Json(masterEndPoint, "/match/list/", listRequest, 10f);
			if (requestUDP == null)
			{
				LegacyMasterClient.DebugLog("RequestServerList result: requestUDP is null");
			}
			else
			{
				LegacyMasterClient.DebugLog(string.Concat(new string[]
				{
					"RequestServerList result: error=",
					requestUDP.isError.ToString(),
					", complete=",
					requestUDP.isComplite.ToString(),
					", len=",
					(requestUDP.result != null) ? requestUDP.result.Length.ToString() : "null",
					", body=",
					LegacyMasterClient.Preview(requestUDP.result)
				}));
			}
			if (requestUDP != null && requestUDP.isError == 0 && !string.IsNullOrEmpty(requestUDP.result))
			{
				try
				{
					bend_MatchMaking.AnsverItem[] array2 = JsonHelper.FromJson<bend_MatchMaking.AnsverItem>(requestUDP.result);
					LegacyMasterClient.DebugLog("RequestServerList parsed count=" + ((array2 != null) ? array2.Length.ToString() : "null"));
					if (array2 != null)
					{
						for (int i = 0; i < array2.Length; i++)
						{
							LegacyMasterClient.DebugLog(string.Concat(new object[]
							{
								"Server[",
								i,
								"] guid=",
								array2[i].serverGUID,
								" group=",
								array2[i].playModeGroup,
								" desc=",
								array2[i].playModeDesc,
								" ip=",
								array2[i].ip,
								":",
								array2[i].port,
								" players=",
								array2[i].playerCount,
								"/",
								array2[i].playersMaxCount,
								" canConnect=",
								array2[i].isCanConnect
							}));
						}
					}
					return array2;
				}
				catch (Exception ex)
				{
					Debug.LogError("Failed to parse master server list: " + ex.ToString() + "\n" + requestUDP.result);
				}
			}
			array = null;
		}
		return array;
	}

	internal static async Task<bend_MatchMaking.ConnectToGameServerAnswer> RequestConnect(string serverGUID, string selectModeGroup)
	{
		IPEndPoint masterEndPoint = LegacyMasterClient.GetMasterEndPoint();
		LegacyMasterClient.DebugLog(string.Concat(new string[]
		{
			"RequestConnect begin: serverGUID=",
			serverGUID,
			", selectModeGroup=",
			selectModeGroup,
			", endpoint=",
			(masterEndPoint != null) ? masterEndPoint.ToString() : "null"
		}));
		bend_MatchMaking.ConnectToGameServerAnswer connectToGameServerAnswer;
		if (masterEndPoint == null)
		{
			LegacyMasterClient.DebugLog("RequestConnect aborted: masterEndPoint is null");
			connectToGameServerAnswer = null;
		}
		else if (RequestUDP.isInRequest(masterEndPoint, "/match/try_connect_to_game_server/", 0f))
		{
			LegacyMasterClient.DebugLog("RequestConnect skipped: same request already active");
			connectToGameServerAnswer = null;
		}
		else
		{
			bend_MatchMaking.ConnectToGameServer connectToGameServer = new bend_MatchMaking.ConnectToGameServer
			{
				token = bend_Client.getAcessToken,
				serverGUID = (string.IsNullOrEmpty(serverGUID) ? "auto" : serverGUID),
				userNetGuid = NetConnect.GetPrivateID(),
				selectModeGroup = selectModeGroup,
				online_protocol_version = AlwaysOnline.onlineVersion,
				lang = Application.systemLanguage.ToString()
			};
			LegacyMasterClient.DebugLog("RequestConnect -> " + masterEndPoint.ToString() + ", body=" + JsonUtility.ToJson(connectToGameServer));
			RequestUDP requestUDP = await RequestUDP.AsyncRequestObj2Json(masterEndPoint, "/match/try_connect_to_game_server/", connectToGameServer, 5f);
			if (requestUDP != null)
			{
				LegacyMasterClient.DebugLog(string.Concat(new string[]
				{
					"RequestConnect result: error=",
					requestUDP.isError.ToString(),
					", len=",
					(requestUDP.result != null) ? requestUDP.result.Length.ToString() : "null",
					", body=",
					LegacyMasterClient.Preview(requestUDP.result)
				}));
			}
			if (requestUDP != null && requestUDP.isError == 0 && requestUDP.result != "fail_because_busy" && !string.IsNullOrEmpty(requestUDP.result))
			{
				try
				{
					return JsonUtility.FromJson<bend_MatchMaking.ConnectToGameServerAnswer>(requestUDP.result);
				}
				catch (Exception ex)
				{
					Debug.LogError("Failed to parse master connect answer: " + ex.ToString() + "\n" + requestUDP.result);
				}
			}
			connectToGameServerAnswer = null;
		}
		return connectToGameServerAnswer;
	}

	private static bool IsDebugEnabled()
	{
		return ArgParcer.GetArgValue("-masterDebug").isExists || ArgParcer.GetArgValue("-debugMaster").isExists;
	}

	private static void DebugLog(string text)
	{
		if (LegacyMasterClient.IsDebugEnabled())
		{
			Debug.Log("[ModMasterClient] " + text);
		}
	}

	private static string Preview(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		text = text.Replace("\r", "\\r").Replace("\n", "\\n");
		if (text.Length > 500)
		{
			return text.Substring(0, 500) + "...";
		}
		return text;
	}

	private static bool loaded;

	private static string masterHost;

	private static int masterPort;

	private static bool autoResolve;

	private static bool useMasterList;

	private static IPEndPoint cachedEndPoint;

	[Serializable]
	private class ListRequest
	{
		public string userNetGuid;

		public int online_protocol_version;

		public string lang;
	}
}
