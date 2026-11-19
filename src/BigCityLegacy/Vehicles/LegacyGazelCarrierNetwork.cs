using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyGazelCarrierNetwork
{
    internal const string Capability = "BCL.gazel.carrier.v1";
    private const short MessageId = 18473;
    private const byte Subscribe = 0, Lock = 1, Unlock = 2, State = 3;
    private static readonly HashSet<NetworkConnection> subscribers = new HashSet<NetworkConnection>();
    private static readonly Dictionary<NetworkConnection, float> lastRequest = new Dictionary<NetworkConnection, float>();
    private static readonly Dictionary<uint, Packet> serverState = new Dictionary<uint, Packet>();
    private static readonly Dictionary<uint, Packet> clientState = new Dictionary<uint, Packet>();
    private static readonly List<uint> expired = new List<uint>();
    internal static bool Available { get; private set; }

    internal static void StartServer()
    {
        StopServer();
        NetworkServer.RegisterHandler(MessageId, ReceiveRequest);
    }

    internal static void StopServer()
    {
        subscribers.Clear();
        lastRequest.Clear();
        serverState.Clear();
        ReleaseAll();
    }

    internal static void StartClient(NetworkClient client)
    {
        StopClient();
        client.RegisterHandler(MessageId, ReceiveState);
    }

    internal static void StopClient()
    {
        Available = false;
        clientState.Clear();
        ReleaseAll();
    }

    private static void ReleaseAll()
    {
        foreach (var carrier in LegacyGazelCarrier.Active)
            if (carrier) carrier.Release();
    }

    internal static void AcceptCapability()
    {
        var connection = ClientScene.readyConnection;
        if (connection != null && connection.isReady && !Available)
            Available = connection.Send(MessageId, new Packet { Kind = Subscribe });
    }

    internal static void Forget(NetworkConnection conn)
    {
        subscribers.Remove(conn);
        lastRequest.Remove(conn);
    }

    internal static void Request(LegacyGazelCarrier carrier, CarControl car, bool release)
    {
        var conn = ClientScene.readyConnection;
        if (!Available || conn == null || !conn.isReady || !carrier.Truck.net_control || !car.net_control)
            return;

        conn.Send(MessageId, new Packet
        {
            Kind = release ? Unlock : Lock,
            Truck = carrier.Truck.net_control.netId.Value,
            Cargo = car.net_control.netId.Value
        });
    }

    private static void ReceiveRequest(NetworkMessage message)
    {
        if (message.reader.Length != 9 || !message.conn.isReady)
            return;

        var kind = message.reader.ReadByte();
        var truckId = message.reader.ReadUInt32();
        var cargoId = message.reader.ReadUInt32();
        float now = Time.realtimeSinceStartup;
        if (lastRequest.TryGetValue(message.conn, out float last) && now - last < 0.3f)
            return;
        lastRequest[message.conn] = now;

        var driver = FindDriver(message.conn);
        if (!driver) return;

        if (kind == Subscribe)
        {
            if (subscribers.Add(message.conn))
                foreach (var state in serverState.Values)
                    message.conn.Send(MessageId, state);
            return;
        }

        if (!subscribers.Contains(message.conn) || (kind != Lock && kind != Unlock))
            return;

        var carrier = FindCarrier(truckId, true);
        var car = FindCar(cargoId, true);
        if (!carrier || !car) return;

        if (kind == Unlock)
        {
            if (carrier.Cargo == car && (car.GetHandleInput() == driver || carrier.Truck.GetHandleInput() == driver))
            {
                carrier.Release();
                ClearServerState(truckId);
            }
            return;
        }

        if (car.GetHandleInput() != driver || !carrier.CanLoad(car))
            return;

        var inverse = Quaternion.Inverse(carrier.Truck.body.rotation);
        var packet = new Packet
        {
            Kind = State,
            Truck = truckId,
            Cargo = cargoId,
            Position = inverse * (car.body.position - carrier.Truck.body.position),
            Rotation = inverse * car.body.rotation
        };
        carrier.Lock(car, packet.Position, packet.Rotation);
        serverState[truckId] = packet;
        Broadcast(packet);
    }

    private static InputControl FindDriver(NetworkConnection conn)
    {
        for (int i = 0; i < NetManager.curCount; ++i)
        {
            var input = NetManager.netControlsByIndex[i];
            if (input && input.isConnected && input.netConnect && input.netConnect.conn == conn)
                return input.input;
        }
        return null;
    }

    private static void ReceiveState(NetworkMessage message)
    {
        if (message.reader.Length != 37 || message.reader.ReadByte() != State)
            return;
        var truck = message.reader.ReadUInt32();
        var cargo = message.reader.ReadUInt32();
        var position = message.reader.ReadVector3();
        var rotation = message.reader.ReadQuaternion();
        if (cargo == 0)
        {
            clientState.Remove(truck);
            var carrier = FindCarrier(truck, false);
            if (carrier) carrier.Release();
            return;
        }
        clientState[truck] = new Packet { Truck = truck, Cargo = cargo, Position = position, Rotation = rotation };
    }

    internal static void Tick()
    {
        if (NetworkServer.active)
        {
            expired.Clear();
            foreach (var entry in serverState)
            {
                var carrier = FindCarrier(entry.Key, true);
                if (!carrier || !carrier.Cargo) expired.Add(entry.Key);
            }
            foreach (uint id in expired) ClearServerState(id);
        }
        else
        {
            foreach (var entry in clientState.Values)
            {
                var carrier = FindCarrier(entry.Truck, false);
                var cargo = FindCar(entry.Cargo, false);
                if (carrier && cargo && cargo.isAlive && carrier.Truck.isAlive &&
                    carrier.isActiveAndEnabled && cargo.isActiveAndEnabled)
                    carrier.Lock(cargo, entry.Position, entry.Rotation);
            }
        }
    }

    private static void ClearServerState(uint id)
    {
        if (serverState.Remove(id))
            Broadcast(new Packet { Kind = State, Truck = id, Rotation = Quaternion.identity });
    }

    private static void Broadcast(Packet packet)
    {
        foreach (var conn in subscribers)
            if (conn.isReady) conn.Send(MessageId, packet);
    }

    private static LegacyGazelCarrier FindCarrier(uint id, bool server)
    {
        var car = FindCar(id, server);
        return car ? car.GetComponent<LegacyGazelCarrier>() : null;
    }

    private static CarControl FindCar(uint id, bool server)
    {
        var netId = new NetworkInstanceId(id);
        var go = server ? NetworkServer.FindLocalObject(netId) : ClientScene.FindLocalObject(netId);
        var motion = go ? go.GetComponent<NetControl>() : null;
        return motion ? motion.control as CarControl : null;
    }

    private sealed class Packet : MessageBase
    {
        internal byte Kind;
        internal uint Truck;
        internal uint Cargo;
        internal Vector3 Position;
        internal Quaternion Rotation;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(Kind);
            writer.Write(Truck);
            writer.Write(Cargo);
            if (Kind != State) return;
            writer.Write(Position);
            writer.Write(Rotation);
        }
    }
}
