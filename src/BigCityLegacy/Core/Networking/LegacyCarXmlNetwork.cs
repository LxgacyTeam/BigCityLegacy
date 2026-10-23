using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

internal sealed class LegacyCarXmlData : MonoBehaviour
{
    internal string Token, FullXml;
    internal byte[] Packed;
}

internal sealed class LegacyCarXmlPump : MonoBehaviour
{
    private void Update() { LegacyCarXmlNetwork.Tick(); }
}

internal static class LegacyCarXmlNetwork
{
    internal const string Capability = "BCL.car.xml.v1";
    private const short MessageId = 18471;
    private const byte Begin = 0, Chunk = 1, Ack = 2, Request = 3, Cancel = 4, Unavailable = 5;
    private const float Timeout = 90f;
    private sealed class Peer
    {
        internal LegacyCarXmlTransfer Sending, Receiving;
        internal float LastUpload = -10f, LastWarning = -10f;
        internal uint AckId;
        internal int AckNext;
    }
    private sealed class UploadJob
    {
        internal NetInputControl Input;
        internal NetControl Car;
        internal LegacyCarXmlTransfer Transfer;
    }
    private static readonly Dictionary<NetworkConnection, Peer> peers = new Dictionary<NetworkConnection, Peer>();
    private static readonly HashSet<NetConnect> advertised = new HashSet<NetConnect>();
    private static readonly Dictionary<uint, float> requests = new Dictionary<uint, float>();
    private static readonly List<UploadJob> uploads = new List<UploadJob>();
    private static Peer client = new Peer();
    private static bool capable;
    private static uint nextId;
    private static string pendingXml, pendingToken;
    private static float lastSweep;
    private static uint requestedObject;
    private static int peerCursor;

    internal static void StartServer()
    {
        peers.Clear(); advertised.Clear();
        NetworkServer.RegisterHandler(MessageId, ReceiveServer);
        InstallPump();
    }
    internal static void StopServer() { peers.Clear(); advertised.Clear(); }
    internal static void StartClient(NetworkClient value)
    {
        StopClient();
        value.RegisterHandler(MessageId, ReceiveClient);
        InstallPump();
    }
    internal static void StopClient()
    {
        capable = false; client = new Peer(); requests.Clear(); uploads.Clear(); requestedObject = 0;
    }
    private static void InstallPump()
    {
        if (NetManager.me && !NetManager.me.GetComponent<LegacyCarXmlPump>()) NetManager.me.gameObject.AddComponent<LegacyCarXmlPump>();
    }
    internal static void Advertise(NetConnect connect)
    {
        if (!NetworkServer.active || !connect || connect.conn == null || !connect.netInput || !connect.netInput.onServerNetPlayer ||
            !connect.netInput.onServerNetPlayer.syncPlayer || !advertised.Add(connect)) return;
        connect.netInput.onServerNetPlayer.syncPlayer.CallTarget_EventMessageHello(connect.conn, Capability);
    }
    internal static void AcceptCapability(NetSyncPlayer player, string command)
    {
        if (command == Capability && player.netControl && player.netControl.netInputControl && player.netControl.netInputControl.hasAuthority)
            capable = true;
    }
    internal static bool Upload(NetInputControl input, NetControl car, string file, string xml, bool testDrive)
    {
        if (NetworkServer.active) return false;
        if (car && !(car.control is CarControl)) return false;
        bool large = LegacyCarXmlCodec.IsLarge(xml);
        // Small edits must not overtake a preceding large edit of the same car.
        if (!large && (client.Sending == null && uploads.Count == 0 || !capable)) return false;
        if (!capable || ClientScene.readyConnection == null || !ClientScene.readyConnection.isReady)
        {
            Debug.LogWarning("[BCL vinyl] This car exceeds the stock XML limit. Update the server to BCL with large-vinyl support.");
            return true;
        }
        if (!input || !input.hasAuthority)
        { Debug.LogWarning("[BCL vinyl] Car XML upload queue is full or the player is unavailable."); return true; }
        try
        {
            string token;
            LegacyCarXmlCodec.MakeWire(xml, out token);
            byte[] packed = LegacyCarXmlCodec.Pack(xml);
            uint objectId = car ? car.netId.Value : 0;
            if (car && !car.hasAuthority) return true;
            var job = new UploadJob { Input = input, Car = car, Transfer = new LegacyCarXmlTransfer {
                Id = NextId(), ObjectId = objectId, Operation = car ? (byte)1 : (byte)0,
                File = file ?? "", Token = token, TestDrive = testDrive, Bytes = packed } };
            int existing = car ? uploads.FindIndex(j => j.Car == car) : -1;
            if (existing >= 0) uploads[existing] = job; // Rapid paint edits keep their latest value.
            else if (uploads.Count < 4) uploads.Add(job);
            else Debug.LogWarning("[BCL vinyl] Car XML upload queue is full.");
        }
        catch (Exception ex) { Debug.LogWarning("[BCL vinyl] Cannot upload car: " + ex.Message); }
        return true;
    }
    private static uint NextId() { if (++nextId == 0) ++nextId; return nextId; }

    // Called before the stock SyncVar setter, including server-initiated cars/updates.
    internal static void SetWire(NetControl car, ref string xml)
    {
        if (NetworkServer.active && LegacyCarXmlCodec.IsCarXml(xml)) LegacyServerVinylLimit.Validate(xml);
        string full = null, token = null;
        if (NetworkServer.active && LegacyCarXmlCodec.IsLarge(xml) && LegacyCarXmlCodec.IsCarXml(xml))
        {
            full = xml;
            xml = LegacyCarXmlCodec.MakeWire(full, out token);
        }
        else
        {
            token = LegacyCarXmlCodec.Token(xml);
            if (token != null && token == pendingToken) full = pendingXml;
        }
        LegacyCarXmlData data = car.GetComponent<LegacyCarXmlData>();
        if (full != null)
        {
            if (!data) data = car.gameObject.AddComponent<LegacyCarXmlData>();
            data.FullXml = full; data.Token = token; data.Packed = null;
        }
        else if (data && data.Token != token) { data.FullXml = null; data.Token = null; data.Packed = null; }
    }
    internal static string Resolve(NetControl car)
    {
        string wire = car.xmlData, token = LegacyCarXmlCodec.Token(wire);
        if (token == null) return null;
        LegacyCarXmlData data = car.GetComponent<LegacyCarXmlData>();
        if (data && data.Token == token && data.FullXml != null) return data.FullXml;
        if (!NetworkServer.active && car.netId.Value != 0 && !requests.ContainsKey(car.netId.Value))
            requests[car.netId.Value] = -Timeout;
        return null;
    }
    private static NetInputControl Authenticated(NetworkConnection conn)
    {
        for (int i = 0; i < NetManager.curCount; i++)
        {
            NetInputControl input = NetManager.netControlsByIndex[i];
            if (input && input.netConnect && input.netConnect.conn == conn && input.isConnected) return input;
        }
        return null;
    }
    private static NetControl Find(uint id, bool server)
    {
        GameObject go = server ? NetworkServer.FindLocalObject(new NetworkInstanceId(id)) : ClientScene.FindLocalObject(new NetworkInstanceId(id));
        return go ? go.GetComponent<NetControl>() : null;
    }
    private static bool Authorized(NetInputControl input, uint objectId)
    {
        if (!input || objectId == 0) return false;
        NetControl car = Find(objectId, true);
        return car && car.control is CarControl && car.netIdentity.clientAuthorityOwner == input.netConnect.conn;
    }
    private static void ReceiveServer(NetworkMessage message)
    {
        NetInputControl input = Authenticated(message.conn);
        if (!input) return;
        Peer peer;
        if (!peers.TryGetValue(message.conn, out peer)) { peer = new Peer(); peers.Add(message.conn, peer); }
        Receive(message, peer, input);
    }
    private static void ReceiveClient(NetworkMessage message) { if (capable) Receive(message, client, null); }
    private static void Receive(NetworkMessage message, Peer peer, NetInputControl input)
    {
        bool server = input;
        float now = Time.realtimeSinceStartup;
        uint id = 0;
        try
        {
            NetworkReader reader = message.reader;
            byte kind = reader.ReadByte();
            id = reader.ReadUInt32();
            if (kind == Request && server)
            {
                if (peer.Sending != null) { Send(message.conn, Unavailable, id); return; }
                NetControl car = Find(id, true);
                LegacyCarXmlData data = car ? car.GetComponent<LegacyCarXmlData>() : null;
                if (!car || car.netIdentity.observers == null || !car.netIdentity.observers.Contains(message.conn) ||
                    !data || data.FullXml == null || data.Token != LegacyCarXmlCodec.Token(car.xmlData))
                { Send(message.conn, Unavailable, id); return; }
                if (data.Packed == null) data.Packed = LegacyCarXmlCodec.Pack(data.FullXml);
                peer.Sending = new LegacyCarXmlTransfer { Id = NextId(), ObjectId = id, Operation = 2, File = "",
                    Token = data.Token, Bytes = data.Packed, LastProgress = now };
                return;
            }
            if (kind == Begin)
            {
                uint objectId = reader.ReadUInt32(); byte operation = reader.ReadByte();
                string file = reader.ReadString(), token = reader.ReadString();
                bool testDrive = reader.ReadBoolean(); int length = reader.ReadInt32();
                if (peer.Receiving != null || (server && (operation > 1 || now - peer.LastUpload < 1f ||
                    (operation == 1 && !Authorized(input, objectId)))) ||
                    (!server && (operation != 2 || objectId != requestedObject))) throw new InvalidDataException("Unexpected car XML transfer.");
                if (server && operation == 0)
                {
                    ControlArray cars = ControlArray.byType(ControlArray.Type.Car);
                    if (!cars || !cars.controls.Exists(c => c.name == file)) throw new InvalidDataException("Unknown vehicle model.");
                    if (LegacyPlayerProperties.Has(input, "nocars") || !LegacyCarSpawnTimeout.CanSpawn(input))
                        throw new InvalidDataException("Car spawning is currently restricted.");
                }
                peer.Receiving = LegacyCarXmlTransfer.Receiving(id, objectId, operation, file, token, testDrive, length, now);
                peer.LastUpload = now;
                SendAck(message.conn, peer, id, 0);
                return;
            }
            if (kind == Chunk)
            {
                LegacyCarXmlTransfer transfer = peer.Receiving;
                if (transfer == null || transfer.Id != id) return;
                int index = reader.ReadInt32();
                // Read the declared length before allocating a chunk supplied by a client.
                ushort size = reader.ReadUInt16();
                if (size > LegacyCarXmlTransfer.ChunkSize) throw new InvalidDataException("Car XML chunk is too large.");
                transfer.Add(index, reader.ReadBytes(size), now);
                if (transfer.Received == transfer.Count)
                {
                    peer.Receiving = null;
                    Complete(transfer, input);
                }
                SendAck(message.conn, peer, id, transfer.Received);
                return;
            }
            if (kind == Ack)
            {
                if (peer.Sending == null || peer.Sending.Id != id) return;
                peer.Sending.Ack(reader.ReadInt32(), now);
                if (peer.Sending.Acknowledged == peer.Sending.Count) peer.Sending = null;
                return;
            }
            if (kind == Cancel)
            {
                if (peer.Sending != null && peer.Sending.Id == id) peer.Sending = null;
                if (peer.Receiving != null && peer.Receiving.Id == id) peer.Receiving = null;
                if (!server) requestedObject = 0;
                return;
            }
            if (kind == Unavailable && !server)
            {
                if (requestedObject == id) requestedObject = 0;
                return;
            }
            throw new InvalidDataException("Unknown car XML message.");
        }
        catch (Exception ex)
        {
            if (id != 0) SendCancel(message.conn, id);
            peer.Receiving = null;
            if (!server) requestedObject = 0;
            if (now - peer.LastWarning >= 5f)
            { peer.LastWarning = now; Debug.LogWarning("[BCL vinyl] Transfer rejected: " + ex.Message); }
        }
    }
    private static void Complete(LegacyCarXmlTransfer transfer, NetInputControl input)
    {
        string full = LegacyCarXmlCodec.Unpack(transfer.Bytes, transfer.Token);
        if (input)
        {
            LegacyServerVinylLimit.Validate(full);
            string token;
            string wire = LegacyCarXmlCodec.MakeWire(full, out token);
            pendingToken = token; pendingXml = full;
            try
            {
                if (transfer.Operation == 0) input.Cmd_DeliveryCar(transfer.File, wire, transfer.TestDrive);
                else if (Authorized(input, transfer.ObjectId)) Find(transfer.ObjectId, true).SetXmlDataServer(wire);
            }
            finally { pendingToken = pendingXml = null; }
        }
        else
        {
            NetControl car = Find(transfer.ObjectId, false);
            if (car && LegacyCarXmlCodec.Token(car.xmlData) == transfer.Token)
            {
                LegacyCarXmlData data = car.GetComponent<LegacyCarXmlData>();
                if (!data) data = car.gameObject.AddComponent<LegacyCarXmlData>();
                data.Token = transfer.Token; data.FullXml = full;
                car.LoadFromXmlIfExists();
                requests.Remove(transfer.ObjectId);
            }
            requestedObject = 0;
        }
    }
    private static bool Send(NetworkConnection conn, byte kind, uint id, Action<NetworkWriter> write = null)
    {
        if (conn == null || !conn.isReady) return false;
        var writer = new NetworkWriter();
        writer.StartMessage(MessageId); writer.Write(kind); writer.Write(id);
        if (write != null) write(writer);
        writer.FinishMessage();
        return conn.SendWriter(writer, 0);
    }
    private static void SendAck(NetworkConnection conn, Peer peer, uint id, int next)
    {
        peer.AckId = id; peer.AckNext = next;
        if (Send(conn, Ack, id, w => w.Write(next))) peer.AckId = 0;
    }
    private static void SendCancel(NetworkConnection conn, uint id) { Send(conn, Cancel, id); }
    private static void Advance(NetworkConnection conn, Peer peer, float now, ref int budget)
    {
        if (peer.AckId != 0) SendAck(conn, peer, peer.AckId, peer.AckNext);
        LegacyCarXmlTransfer transfer = peer.Sending;
        if (transfer != null)
        {
            if (now - transfer.LastProgress > Timeout) { peer.Sending = null; SendCancel(conn, transfer.Id); Debug.LogWarning("[BCL vinyl] Upload/download timed out."); }
            else if (!transfer.Begun)
            {
                transfer.Begun = Send(conn, Begin, transfer.Id, w => {
                    w.Write(transfer.ObjectId); w.Write(transfer.Operation); w.Write(transfer.File); w.Write(transfer.Token);
                    w.Write(transfer.TestDrive); w.Write(transfer.Bytes.Length);
                });
            }
            else
            {
                while (budget > 0 && transfer.Sent < transfer.Count && transfer.Sent - transfer.Acknowledged < LegacyCarXmlTransfer.Window)
                {
                    int index = transfer.Sent, offset = index * LegacyCarXmlTransfer.ChunkSize;
                    int count = Math.Min(LegacyCarXmlTransfer.ChunkSize, transfer.Bytes.Length - offset);
                    transfer.Sent++;
                    bool sent = Send(conn, Chunk, transfer.Id, w => {
                        w.Write(index); w.Write((ushort)count);
                        // Write(bytes, count) has no additional length prefix.
                        var chunk = new byte[count]; Buffer.BlockCopy(transfer.Bytes, offset, chunk, 0, count); w.Write(chunk, count);
                    });
                    if (!sent) { transfer.Sent--; break; }
                    budget--;
                }
            }
        }
        if (peer.Receiving != null && now - peer.Receiving.LastProgress > Timeout)
        { SendCancel(conn, peer.Receiving.Id); peer.Receiving = null; }
    }
    internal static void Tick()
    {
        float now = Time.realtimeSinceStartup;
        int budget = 32;
        if (NetworkServer.active)
        {
            // Snapshot: synchronous host ACKs may modify transfer state in SendWriter.
            var snapshot = new List<KeyValuePair<NetworkConnection, Peer>>(peers);
            for (int i = 0; i < snapshot.Count && budget > 0; i++)
            {
                var pair = snapshot[(peerCursor + i) % snapshot.Count];
                Advance(pair.Key, pair.Value, now, ref budget);
            }
            peerCursor = snapshot.Count == 0 ? 0 : (peerCursor + 1) % snapshot.Count;
            if (now - lastSweep > 2f)
            {
                lastSweep = now;
                foreach (NetworkConnection conn in new List<NetworkConnection>(peers.Keys)) if (!Authenticated(conn)) peers.Remove(conn);
                advertised.RemoveWhere(c => !c);
            }
        }
        NetworkConnection connection = ClientScene.readyConnection;
        if (!capable || connection == null || !connection.isReady) return;
        if (client.Sending == null && uploads.Count > 0 && now - client.LastUpload >= 1.1f)
        {
            UploadJob job = uploads[0]; uploads.RemoveAt(0);
            if (job.Input && job.Input.hasAuthority && (job.Transfer.ObjectId == 0 || job.Car && job.Car.hasAuthority))
            {
                client.Sending = job.Transfer;
                client.Sending.LastProgress = client.LastUpload = now;
            }
        }
        Advance(connection, client, now, ref budget);
        if (requestedObject != 0)
        {
            float started;
            if (!requests.TryGetValue(requestedObject, out started) || client.Receiving == null && now - started > Timeout)
            { client.Receiving = null; requestedObject = 0; }
        }
        if (requestedObject == 0 && client.Receiving == null)
            foreach (uint id in new List<uint>(requests.Keys))
            {
                NetControl car = Find(id, false);
                if (!car || LegacyCarXmlCodec.Token(car.xmlData) == null) { requests.Remove(id); continue; }
                if (now - requests[id] < 5f) continue;
                if (Send(connection, Request, id)) { requestedObject = id; requests[id] = now; }
                break;
            }
    }
}
