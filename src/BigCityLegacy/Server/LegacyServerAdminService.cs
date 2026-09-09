using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyServerAdminService
{
    private const float ReconcileInterval = 1.0f;
    private const float PendingCarTimeout = 8.0f;

    private static readonly Dictionary<string, PlayerSession> sessionsByKey = new Dictionary<string, PlayerSession>();
    private static readonly Dictionary<int, PlayerSession> sessionsByPlayerId = new Dictionary<int, PlayerSession>();
    private static readonly List<PendingCarSpawn> pendingCarSpawns = new List<PendingCarSpawn>();

    private static int nextPlayerId = 1;
    private static float nextReconcileAt;
    private static bool serverStarted;

    internal static void ResetForServerStart()
    {
        sessionsByKey.Clear();
        sessionsByPlayerId.Clear();
        pendingCarSpawns.Clear();
        nextPlayerId = 1;
        nextReconcileAt = 0f;
        serverStarted = true;
    }

    internal static void Update()
    {
        if (!NetManager.isServer || !serverStarted)
        {
            return;
        }

        CleanupExpiredPendingCarSpawns();

        if (Time.realtimeSinceStartup < nextReconcileAt)
        {
            return;
        }

        nextReconcileAt = Time.realtimeSinceStartup + ReconcileInterval;
        ReconcileSessions();
    }

    internal static bool TryRejectBannedConnection(NetConnect connect, string guid, string nickname)
    {
        if (!NetManager.isServer || connect == null)
        {
            return false;
        }

        string ip = GetConnectionAddress(connect.conn);
        LegacyBanList.BanEntry entry;
        if (!LegacyBanList.IsBanned(guid, ip, out entry))
        {
            return false;
        }

        Debug.Log(
            "[Server] Rejected banned player '" + SafeName(nickname) +
            "' (GUID=" + LegacyBanList.NormalizeGuid(guid) +
            ", IP=" + LegacyBanList.NormalizeIp(ip) +
            ", BanID=" + entry.BanID.ToString() +
            ", Type=" + entry.Type.ToString() + ")."
        );

        Disconnect(connect);
        return true;
    }

    internal static void ObserveAuthenticatedConnection(NetConnect connect, string guid, string nickname, RuntimePlatform platform)
    {
        if (!NetManager.isServer || connect == null)
        {
            return;
        }

        string key = BuildSessionKey(connect, guid);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        PlayerSession session;
        if (!sessionsByKey.TryGetValue(key, out session))
        {
            session = new PlayerSession
            {
                PlayerId = nextPlayerId++,
                ConnectedAt = Time.realtimeSinceStartup,
                SessionKey = key,
                Connect = connect
            };
            sessionsByKey.Add(key, session);
            sessionsByPlayerId.Add(session.PlayerId, session);

            UpdateSession(session, connect, guid, nickname, platform);
            Debug.Log(
                "[Server] Player connected: " + session.PlayerId.ToString() +
                " | " + SafeName(session.Nickname) +
                " | GUID=" + session.Guid +
                " | IP=" + session.Ip
            );
            return;
        }

        UpdateSession(session, connect, guid, nickname, platform);
    }

    internal static void ObserveNickname(NetInputControl input, string nickname)
    {
        if (!NetManager.isServer || input == null || string.IsNullOrEmpty(nickname))
        {
            return;
        }

        PlayerSession session = FindPlayerForInput(input);
        if (session != null)
        {
            session.Nickname = nickname;
        }
    }

    internal static List<ServerPlayerSnapshot> GetPlayers()
    {
        ReconcileSessions();

        List<ServerPlayerSnapshot> players = new List<ServerPlayerSnapshot>();
        foreach (KeyValuePair<int, PlayerSession> pair in sessionsByPlayerId)
        {
            PlayerSession session = pair.Value;
            if (session == null || !session.IsConnected)
            {
                continue;
            }

            players.Add(CreateSnapshot(session));
        }

        players.Sort(delegate(ServerPlayerSnapshot a, ServerPlayerSnapshot b)
        {
            return a.PlayerId.CompareTo(b.PlayerId);
        });
        return players;
    }

    internal static bool TryGetPlayer(int playerId, out ServerPlayerSnapshot player)
    {
        player = null;
        ReconcileSessions();

        PlayerSession session;
        if (!sessionsByPlayerId.TryGetValue(playerId, out session) || session == null || !session.IsConnected)
        {
            return false;
        }

        player = CreateSnapshot(session);
        return true;
    }

    internal static bool KickPlayer(int playerId, out string error)
    {
        error = null;
        PlayerSession session;
        if (!sessionsByPlayerId.TryGetValue(playerId, out session) || session == null || !session.IsConnected)
        {
            error = "PlayerID " + playerId.ToString() + " was not found.";
            return false;
        }

        Disconnect(session.Connect);
        return true;
    }

    internal static bool BanAndKickPlayer(int playerId, int banType, out LegacyBanList.BanEntry ban, out string error)
    {
        ban = null;
        error = null;

        ServerPlayerSnapshot player;
        if (!TryGetPlayer(playerId, out player))
        {
            error = "PlayerID " + playerId.ToString() + " was not found.";
            return false;
        }

        if (!LegacyBanList.AddBan(player, banType, out ban, out error))
        {
            return false;
        }

        string kickError;
        if (!KickPlayer(playerId, out kickError))
        {
            Debug.LogWarning("[Server] Ban was saved but immediate disconnect failed: " + kickError);
        }

        return true;
    }

    internal static int DeleteAllCars(out int ejected, out int failed)
    {
        return DeleteCars(delegate (NetControl netControl)
        {
            return IsLiveServerCar(netControl);
        }, out ejected, out failed);
    }

    internal static int DeleteCarsOwnedByPlayer(int playerId, out int ejected, out int failed)
    {
        return DeleteCars(delegate (NetControl netControl)
        {
            if (!IsLiveServerCar(netControl))
            {
                return false;
            }

            LegacyServerVehicleOwner owner = netControl.GetComponent<LegacyServerVehicleOwner>();
            return owner != null && owner.OwnerPlayerId == playerId;
        }, out ejected, out failed);
    }

    private static int DeleteCars(Predicate<NetControl> matches, out int ejected, out int failed)
    {
        ejected = 0;
        failed = 0;

        if (!NetManager.isServer || !NetworkServer.active)
        {
            return 0;
        }

        List<NetControl> snapshot = SnapshotMatchingCars(matches);
        int removed = 0;

        for (int i = 0; i < snapshot.Count; i++)
        {
            int ejectedFromCar;
            string error;
            if (!TryDeleteCar(snapshot[i], out ejectedFromCar, out error))
            {
                failed++;
                Debug.LogWarning("[Server] Vehicle cleanup skipped a car: " + error);
                continue;
            }

            ejected += ejectedFromCar;
            removed++;
        }

        return removed;
    }

    private static List<NetControl> SnapshotMatchingCars(Predicate<NetControl> matches)
    {
        List<NetControl> result = new List<NetControl>();

        for (int i = 0; i < NetControl.netControls.Count; i++)
        {
            NetControl netControl = NetControl.netControls[i];
            if (matches != null && matches(netControl))
            {
                result.Add(netControl);
            }
        }

        return result;
    }

    private static bool IsLiveServerCar(NetControl netControl)
    {
        return netControl != null &&
               netControl.gameObject != null &&
               netControl.gameObject.activeInHierarchy &&
               netControl.isServer &&
               netControl.control is CarControl;
    }

    private static bool TryDeleteCar(NetControl netControl, out int ejected, out string error)
    {
        ejected = 0;
        error = null;

        if (!IsLiveServerCar(netControl))
        {
            error = "NetControl is no longer a live server car.";
            return false;
        }

        CarControl car = netControl.control as CarControl;
        if (car == null)
        {
            error = "NetControl does not own a CarControl.";
            return false;
        }

        try
        {
            while (car.child_controls.Count > 0)
            {
                Control passenger = car.child_controls[0];
                if (passenger == null)
                {
                    error = "Car has a null child control.";
                    return false;
                }

                int before = car.child_controls.Count;
                passenger.SetParentControl(null);
                passenger.ContolFallDown();

                if (car.child_controls.Count >= before)
                {
                    error = "SetParentControl(null) did not detach a car passenger.";
                    return false;
                }

                ejected++;
            }

            if (!NetworkServer.active)
            {
                error = "UNet server is not active.";
                return false;
            }

            LegacyServerVehicleOwner owner = netControl.GetComponent<LegacyServerVehicleOwner>();

            NetworkServer.Destroy(netControl.gameObject);

            if (!ReferenceEquals(owner, null))
            {
                owner.MarkInactive("removed by CLI");
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    internal static bool TrySendServerMessage(string message, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(message))
        {
            error = "Message text is empty.";
            return false;
        }

        try
        {
            LegacyHelpers.EnsureServerNetChat(NetManager.me);
            if (!NetChat.me)
            {
                error = "Server chat is not ready.";
                return false;
            }

            // NetChat.AddMessage dereferences its sender to read the nickname and
            // connection context. Passing null therefore works neither on the
            // dedicated server nor with the stock chat implementation.
            //
            // Reuse one live player input for the server-side broadcast, but only
            // for the synchronous AddMessage call. The nickname is restored in a
            // finally block before the next frame, so this does not rename the
            // actual player or require a client-side change.
            NetInputControl sender = FindAnyChatSender();
            if (sender == null)
            {
                error = "No active player input is available for the stock chat relay.";
                return false;
            }

            string originalNickname = GetStringMember(sender, "nikName", "nickname", "name") ?? string.Empty;
            if (!TrySetStringMember(sender, "[Server]", "nikName", "nickname", "name"))
            {
                error = "The active player input does not expose a writable nickname field.";
                return false;
            }

            try
            {
                NetChat.me.AddMessage(sender, message.Trim(), true, 0f);
            }
            finally
            {
                TrySetStringMember(sender, originalNickname, "nikName", "nickname", "name");
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static NetInputControl FindAnyChatSender()
    {
        foreach (KeyValuePair<int, PlayerSession> pair in sessionsByPlayerId)
        {
            PlayerSession session = pair.Value;
            if (session == null || !session.IsConnected || session.Connect == null)
            {
                continue;
            }

            NetInputControl input = GetMemberValue(
                session.Connect,
                "input",
                "netInput",
                "inputControl",
                "playerInput"
            ) as NetInputControl;

            if (IsUsableChatSender(input))
            {
                return input;
            }
        }

        foreach (KeyValuePair<int, PlayerSession> pair in sessionsByPlayerId)
        {
            PlayerSession session = pair.Value;
            if (session == null || !session.IsConnected || session.Connect == null)
            {
                continue;
            }

            NetInputControl input = session.Connect.GetComponent<NetInputControl>();
            if (IsUsableChatSender(input))
            {
                return input;
            }
        }

        NetInputControl[] inputs = Resources.FindObjectsOfTypeAll<NetInputControl>();
        for (int i = 0; i < inputs.Length; i++)
        {
            NetInputControl input = inputs[i];
            if (!IsUsableChatSender(input))
            {
                continue;
            }

            PlayerSession resolved = FindPlayerForInput(input);
            if (resolved != null)
            {
                return input;
            }

            string nickname = GetStringMember(input, "nikName", "nickname", "name");
            if (string.IsNullOrEmpty(nickname))
            {
                continue;
            }

            foreach (KeyValuePair<int, PlayerSession> pair in sessionsByPlayerId)
            {
                PlayerSession session = pair.Value;
                if (session != null && session.IsConnected &&
                    string.Equals(session.Nickname, nickname, StringComparison.Ordinal))
                {
                    return input;
                }
            }
        }

        return null;
    }

    private static bool IsUsableChatSender(NetInputControl input)
    {
        return input != null &&
               input.gameObject != null &&
               input.gameObject.activeInHierarchy;
    }

    internal static string GetNicknameForInput(NetInputControl input)
    {
        PlayerSession session = FindPlayerForInput(input);
        if (session != null && !string.IsNullOrEmpty(session.Nickname))
        {
            return session.Nickname;
        }

        return GetStringMember(input, "nikName", "nickname", "name") ?? "<unknown>";
    }

    internal static bool CanSpawnCarForPlayer(NetInputControl input, out int activeCars, out int reservedCars)
    {
        activeCars = 0;
        reservedCars = 0;

        PlayerSession session = FindPlayerForInput(input);
        if (session == null)
        {
            return true;
        }

        activeCars = CountActiveCarsForSession(session.SessionKey);
        reservedCars = CountPendingCarsForSession(session.SessionKey);
        return LegacyServerLimits.MaxCarsByPlayer <= 0 || activeCars + reservedCars < LegacyServerLimits.MaxCarsByPlayer;
    }

    internal static void RegisterCarSpawnRequest(NetInputControl input)
    {
        PlayerSession session = FindPlayerForInput(input);
        if (session == null)
        {
            Debug.LogWarning("[Server] Could not resolve a car spawn owner; per-player car tracking was skipped for this request.");
            return;
        }

        pendingCarSpawns.Add(new PendingCarSpawn
        {
            OwnerSessionKey = session.SessionKey,
            RequestedAt = Time.realtimeSinceStartup
        });
    }

    internal static void ObserveCreatedNetworkControl(NetControl netControl)
    {
        if (!NetManager.isServer || !IsActiveServerVehicle(netControl))
        {
            return;
        }

        LegacyServerVehicleOwner existing = netControl.GetComponent<LegacyServerVehicleOwner>();
        if (existing != null && !string.IsNullOrEmpty(existing.OwnerSessionKey))
        {
            return;
        }

        PlayerSession owner = FindOwnerByNetworkObject(netControl);
        PendingCarSpawn pending = null;

        if (owner != null)
        {
            pending = TakePendingForSession(owner.SessionKey);
        }

        if (pending == null)
        {
            pending = TakeOldestPending();
            if (pending != null)
            {
                sessionsByKey.TryGetValue(pending.OwnerSessionKey, out owner);
            }
        }

        if (pending == null || owner == null)
        {
            return;
        }

        LegacyServerVehicleOwner marker = existing ?? netControl.gameObject.AddComponent<LegacyServerVehicleOwner>();
        marker.OwnerSessionKey = owner.SessionKey;
        marker.OwnerPlayerId = owner.PlayerId;
        owner.TotalCarsSpawned++;
    }

    internal static int CountAllPendingCars()
    {
        CleanupExpiredPendingCarSpawns();
        return pendingCarSpawns.Count;
    }

    internal static int CountActiveCarsForSessionKey(string sessionKey)
    {
        return CountActiveCarsForSession(sessionKey);
    }

    internal static void Disconnect(NetConnect connect)
    {
        if (connect == null)
        {
            return;
        }

        try
        {
            if (connect.conn != null)
            {
                connect.conn.Disconnect();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Server] Disconnect failed: " + ex.Message);
        }
    }

    internal static string GetConnectionAddress(object connection)
    {
        object value = GetMemberValue(connection, "address", "Address");
        return LegacyBanList.NormalizeIp(value == null ? null : value.ToString());
    }

    private static void ReconcileSessions()
    {
        if (!NetManager.isServer || !serverStarted)
        {
            return;
        }

        HashSet<string> liveKeys = new HashSet<string>();
        NetConnect[] connects = Resources.FindObjectsOfTypeAll<NetConnect>();

        for (int i = 0; i < connects.Length; i++)
        {
            NetConnect connect = connects[i];
            if (!IsUsableConnection(connect))
            {
                continue;
            }

            string guid = LegacyBanList.NormalizeGuid(connect.privateGUID);
            // A NetConnect exists briefly before the stock client sends Cmd_SetPrivateID.
            // It is not a playable/admin-visible player until its GUID handshake arrives.
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            string key = BuildSessionKey(connect, guid);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            liveKeys.Add(key);
            PlayerSession session;
            if (!sessionsByKey.TryGetValue(key, out session))
            {
                ObserveAuthenticatedConnection(connect, guid, connect.nikName, RuntimePlatform.WindowsPlayer);
                continue;
            }

            UpdateSession(session, connect, guid, connect.nikName, session.RuntimePlatform);
        }

        List<string> disconnected = new List<string>();
        foreach (KeyValuePair<string, PlayerSession> pair in sessionsByKey)
        {
            PlayerSession session = pair.Value;
            if (session != null && session.IsConnected && !liveKeys.Contains(pair.Key))
            {
                disconnected.Add(pair.Key);
            }
        }

        for (int i = 0; i < disconnected.Count; i++)
        {
            PlayerSession session = sessionsByKey[disconnected[i]];
            session.IsConnected = false;
            Debug.Log(
                "[Server] Player disconnected: " + session.PlayerId.ToString() +
                " | " + SafeName(session.Nickname) +
                " | GUID=" + session.Guid +
                " | IP=" + session.Ip
            );
            sessionsByKey.Remove(disconnected[i]);
            sessionsByPlayerId.Remove(session.PlayerId);
            RemovePendingForSession(session.SessionKey);
        }
    }

    private static bool IsUsableConnection(NetConnect connect)
    {
        if (connect == null || connect.conn == null || connect.gameObject == null)
        {
            return false;
        }

        if (!connect.gameObject.activeInHierarchy)
        {
            return false;
        }

        object isConnected = GetMemberValue(connect.conn, "isConnected", "connected");
        if (isConnected is bool && !(bool)isConnected)
        {
            return false;
        }

        return true;
    }

    private static void UpdateSession(PlayerSession session, NetConnect connect, string guid, string nickname, RuntimePlatform platform)
    {
        session.Connect = connect;
        session.IsConnected = true;
        session.Guid = LegacyBanList.NormalizeGuid(guid);
        session.Nickname = string.IsNullOrEmpty(nickname) ? session.Nickname : nickname;
        session.Ip = GetConnectionAddress(connect.conn);
        session.RuntimePlatform = platform;
        session.Platform = platform.ToString();
    }

    private static PlayerSession FindPlayerForInput(NetInputControl input)
    {
        if (input == null)
        {
            return null;
        }

        object directConnect = GetMemberValue(input, "connect", "netConnect", "ownerConnect");
        NetConnect asConnect = directConnect as NetConnect;
        if (asConnect != null)
        {
            return FindPlayerForConnection(asConnect.conn, asConnect.privateGUID);
        }

        object connection = GetMemberValue(input, "connectionToClient", "conn", "connection", "ownerConnection");
        PlayerSession byConnection = FindPlayerForConnection(connection, null);
        if (byConnection != null)
        {
            return byConnection;
        }

        NetConnect onSameObject = input.GetComponent<NetConnect>();
        if (onSameObject != null)
        {
            PlayerSession sameObjectSession = FindPlayerForConnection(onSameObject.conn, onSameObject.privateGUID);
            if (sameObjectSession != null)
            {
                return sameObjectSession;
            }
        }

        foreach (KeyValuePair<string, PlayerSession> pair in sessionsByKey)
        {
            PlayerSession session = pair.Value;
            if (session == null || !session.IsConnected || session.Connect == null)
            {
                continue;
            }

            object candidate = GetMemberValue(session.Connect, "input", "netInput", "inputControl", "playerInput");
            if (ReferenceEquals(candidate, input))
            {
                return session;
            }
        }

        string nickname = GetStringMember(input, "nikName", "nickname", "name");
        if (!string.IsNullOrEmpty(nickname))
        {
            PlayerSession onlyMatch = null;
            foreach (KeyValuePair<string, PlayerSession> pair in sessionsByKey)
            {
                PlayerSession session = pair.Value;
                if (!session.IsConnected || !string.Equals(session.Nickname, nickname, StringComparison.Ordinal))
                {
                    continue;
                }

                if (onlyMatch != null)
                {
                    return null;
                }
                onlyMatch = session;
            }
            return onlyMatch;
        }

        return null;
    }

    private static PlayerSession FindOwnerByNetworkObject(NetControl netControl)
    {
        if (netControl == null)
        {
            return null;
        }

        NetworkIdentity identity = netControl.GetComponent<NetworkIdentity>();
        if (identity != null)
        {
            PlayerSession byConnection = FindPlayerForConnection(GetMemberValue(identity, "connectionToClient"), null);
            if (byConnection != null)
            {
                return byConnection;
            }
        }

        object directConnect = GetMemberValue(netControl, "connect", "netConnect", "ownerConnect");
        NetConnect asConnect = directConnect as NetConnect;
        if (asConnect != null)
        {
            return FindPlayerForConnection(asConnect.conn, asConnect.privateGUID);
        }

        object input = GetMemberValue(netControl, "input", "netInput", "ownerInput");
        return FindPlayerForInput(input as NetInputControl);
    }

    private static PlayerSession FindPlayerForConnection(object connection, string guid)
    {
        int connectionId = GetConnectionId(connection);
        string normalizedGuid = LegacyBanList.NormalizeGuid(guid);

        foreach (KeyValuePair<string, PlayerSession> pair in sessionsByKey)
        {
            PlayerSession session = pair.Value;
            if (session == null || !session.IsConnected)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(normalizedGuid) &&
                string.Equals(session.Guid, normalizedGuid, StringComparison.OrdinalIgnoreCase))
            {
                return session;
            }

            if (connectionId >= 0 && GetConnectionId(session.Connect != null ? session.Connect.conn : null) == connectionId)
            {
                return session;
            }
        }

        return null;
    }

    private static string BuildSessionKey(NetConnect connect, string guid)
    {
        if (connect == null)
        {
            return null;
        }

        int connectionId = GetConnectionId(connect.conn);
        string normalizedGuid = LegacyBanList.NormalizeGuid(guid);
        string ip = GetConnectionAddress(connect.conn);

        if (connectionId >= 0)
        {
            return "c:" + connectionId.ToString() + "|" + normalizedGuid + "|" + ip;
        }

        if (!string.IsNullOrEmpty(normalizedGuid))
        {
            return "g:" + normalizedGuid + "|" + ip;
        }

        return "o:" + connect.GetInstanceID().ToString();
    }

    private static ServerPlayerSnapshot CreateSnapshot(PlayerSession session)
    {
        int activeCars = CountActiveCarsForSession(session.SessionKey);
        int ping = GetPing(session.Connect != null ? session.Connect.conn : null);

        return new ServerPlayerSnapshot
        {
            PlayerId = session.PlayerId,
            Nickname = session.Nickname ?? string.Empty,
            Guid = session.Guid ?? string.Empty,
            Ip = session.Ip ?? string.Empty,
            ConnectedSeconds = Math.Max(0f, Time.realtimeSinceStartup - session.ConnectedAt),
            ActiveCars = activeCars,
            TotalCarsSpawned = session.TotalCarsSpawned,
            PingMilliseconds = ping,
            Platform = session.Platform ?? string.Empty,
            SessionKey = session.SessionKey
        };
    }

    private static int CountActiveCarsForSession(string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            return 0;
        }

        LegacyServerVehicleOwner[] owners = Resources.FindObjectsOfTypeAll<LegacyServerVehicleOwner>();
        int count = 0;
        for (int i = 0; i < owners.Length; i++)
        {
            LegacyServerVehicleOwner owner = owners[i];
            if (owner == null || !string.Equals(owner.OwnerSessionKey, sessionKey, StringComparison.Ordinal))
            {
                continue;
            }

            NetControl netControl = owner.GetComponent<NetControl>();
            if (IsActiveServerVehicle(netControl))
            {
                count++;
            }
        }

        return count;
    }

    internal static bool IsActiveServerVehicle(NetControl netControl)
    {
        if (netControl == null ||
            netControl.gameObject == null ||
            !netControl.gameObject.activeInHierarchy ||
            !netControl.isServer ||
            string.Equals(netControl.objName, "Male", StringComparison.Ordinal))
        {
            return false;
        }

        LegacyServerVehicleOwner owner = netControl.GetComponent<LegacyServerVehicleOwner>();
        if (owner != null && owner.IsInactive)
        {
            return false;
        }

        if (!IsVehicleRuntimeDestroyed(netControl))
        {
            return true;
        }

        if (owner != null)
        {
            owner.MarkInactive("destroyed");
        }

        return false;
    }

    /// <summary>
    /// Vehicle instances survive in the control cache after an explosion, so their
    /// GameObject remains active. The relevant CarControl/Control state is the
    /// authority for the player and global car counters.
    /// </summary>
    internal static bool IsVehicleRuntimeDestroyed(NetControl netControl)
    {
        if (netControl == null)
        {
            return true;
        }

        if (HasDestroyedFlag(netControl))
        {
            return true;
        }

        object control = netControl.control;
        if (HasDestroyedFlag(control))
        {
            return true;
        }

        object itemControl = netControl.objItem != null ? netControl.objItem.control : null;
        if (!ReferenceEquals(itemControl, control) && HasDestroyedFlag(itemControl))
        {
            return true;
        }

        return false;
    }

    private static bool HasDestroyedFlag(object instance)
    {
        if (instance == null)
        {
            return false;
        }

        UnityEngine.Object unityObject = instance as UnityEngine.Object;
        if (unityObject != null && !unityObject)
        {
            return true;
        }

        string[] booleanNames =
        {
            "isDead",
            "dead",
            "isDied",
            "isDestroyed",
            "destroyed",
            "isExploded",
            "exploded",
            "isExplosion",
            "isBlowed",
            "blowed",
            "isWreck",
            "isBurnedOut",
            "needDestroy",
            "needRemove"
        };

        for (int i = 0; i < booleanNames.Length; i++)
        {
            object value = GetMemberValue(instance, booleanNames[i]);
            if (value is bool && (bool)value)
            {
                return true;
            }
        }

        string[] stateNames = { "deathState", "destroyState", "destroyedState", "carDeathState" };
        for (int i = 0; i < stateNames.Length; i++)
        {
            object state = GetMemberValue(instance, stateNames[i]);
            if (state == null)
            {
                continue;
            }

            string stateText = state.ToString();
            if (stateText.IndexOf("dead", StringComparison.OrdinalIgnoreCase) >= 0 ||
                stateText.IndexOf("destroy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                stateText.IndexOf("explod", StringComparison.OrdinalIgnoreCase) >= 0 ||
                stateText.IndexOf("wreck", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountPendingCarsForSession(string sessionKey)
    {
        CleanupExpiredPendingCarSpawns();
        int count = 0;
        for (int i = 0; i < pendingCarSpawns.Count; i++)
        {
            if (string.Equals(pendingCarSpawns[i].OwnerSessionKey, sessionKey, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static PendingCarSpawn TakePendingForSession(string sessionKey)
    {
        CleanupExpiredPendingCarSpawns();
        for (int i = 0; i < pendingCarSpawns.Count; i++)
        {
            PendingCarSpawn pending = pendingCarSpawns[i];
            if (!string.Equals(pending.OwnerSessionKey, sessionKey, StringComparison.Ordinal))
            {
                continue;
            }

            pendingCarSpawns.RemoveAt(i);
            return pending;
        }
        return null;
    }

    private static PendingCarSpawn TakeOldestPending()
    {
        CleanupExpiredPendingCarSpawns();
        if (pendingCarSpawns.Count == 0)
        {
            return null;
        }

        PendingCarSpawn pending = pendingCarSpawns[0];
        pendingCarSpawns.RemoveAt(0);
        return pending;
    }

    private static void RemovePendingForSession(string sessionKey)
    {
        for (int i = pendingCarSpawns.Count - 1; i >= 0; i--)
        {
            if (string.Equals(pendingCarSpawns[i].OwnerSessionKey, sessionKey, StringComparison.Ordinal))
            {
                pendingCarSpawns.RemoveAt(i);
            }
        }
    }

    private static void CleanupExpiredPendingCarSpawns()
    {
        float now = Time.realtimeSinceStartup;
        for (int i = pendingCarSpawns.Count - 1; i >= 0; i--)
        {
            if (now - pendingCarSpawns[i].RequestedAt > PendingCarTimeout)
            {
                pendingCarSpawns.RemoveAt(i);
            }
        }
    }

    private static int GetPing(object connection)
    {
        int hostId = GetIntMember(connection, "hostId", -1);
        int connectionId = GetConnectionId(connection);
        if (hostId < 0 || connectionId < 0)
        {
            return -1;
        }

        try
        {
            byte error;
            int ping = NetworkTransport.GetCurrentRTT(hostId, connectionId, out error);
            return error == 0 ? ping : -1;
        }
        catch
        {
            return -1;
        }
    }

    private static int GetConnectionId(object connection)
    {
        return GetIntMember(connection, "connectionId", -1);
    }

    private static int GetIntMember(object instance, string memberName, int fallback)
    {
        object value = GetMemberValue(instance, memberName);
        if (value is int)
        {
            return (int)value;
        }
        return fallback;
    }

    private static string GetStringMember(object instance, params string[] names)
    {
        object value = GetMemberValue(instance, names);
        return value as string;
    }

    private static bool TrySetStringMember(object instance, string value, params string[] names)
    {
        if (instance == null || names == null)
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null && field.FieldType == typeof(string))
                {
                    try
                    {
                        field.SetValue(instance, value);
                        return true;
                    }
                    catch
                    {
                    }
                }

                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.PropertyType == typeof(string) && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        property.SetValue(instance, value, null);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }
        }

        return false;
    }

    private static object GetMemberValue(object instance, params string[] names)
    {
        if (instance == null || names == null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    try { return field.GetValue(instance); }
                    catch { }
                }

                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    try { return property.GetValue(instance, null); }
                    catch { }
                }
            }
        }

        return null;
    }

    private static string SafeName(string value)
    {
        return string.IsNullOrEmpty(value) ? "<unknown>" : value;
    }

    private sealed class PlayerSession
    {
        internal int PlayerId;
        internal string SessionKey;
        internal NetConnect Connect;
        internal string Nickname;
        internal string Guid;
        internal string Ip;
        internal string Platform;
        internal RuntimePlatform RuntimePlatform;
        internal float ConnectedAt;
        internal int TotalCarsSpawned;
        internal bool IsConnected = true;
    }

    private sealed class PendingCarSpawn
    {
        internal string OwnerSessionKey;
        internal float RequestedAt;
    }

    internal sealed class ServerPlayerSnapshot
    {
        internal int PlayerId;
        internal string SessionKey;
        internal string Nickname;
        internal string Guid;
        internal string Ip;
        internal float ConnectedSeconds;
        internal int ActiveCars;
        internal int TotalCarsSpawned;
        internal int PingMilliseconds;
        internal string Platform;
    }
}

internal sealed class LegacyServerVehicleOwner : MonoBehaviour
{
    internal string OwnerSessionKey;
    internal int OwnerPlayerId;
    internal bool IsInactive { get; private set; }

    private string inactiveReason;

    private void Update()
    {
        if (IsInactive)
        {
            return;
        }

        NetControl netControl = GetComponent<NetControl>();
        if (LegacyServerAdminService.IsVehicleRuntimeDestroyed(netControl))
        {
            MarkInactive("destroyed");
        }
    }

    internal void MarkInactive(string reason)
    {
        if (IsInactive)
        {
            return;
        }

        IsInactive = true;
        inactiveReason = string.IsNullOrEmpty(reason) ? "inactive" : reason;

        Debug.Log(
            "[Server] Vehicle slot released: owner=" + OwnerPlayerId.ToString() +
            ", reason=" + inactiveReason + "."
        );
    }
}
