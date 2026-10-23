using System;
using System.Collections.Generic;

// Serialized messages are copied because UNet reuses its writers. Preserve FIFO
// across spawn, authority, RPC, SyncVar and destroy messages on the same channel.
internal sealed class LegacyReliableSendQueue<T>
{
    private const int PeerLimit = 16 * 1024 * 1024, TotalLimit = 256 * 1024 * 1024;
    private readonly Dictionary<T, Queue<byte[]>> queues = new Dictionary<T, Queue<byte[]>>();
    private readonly Dictionary<T, int> sizes = new Dictionary<T, int>();
    private int cursor;
    internal int Bytes { get; private set; }
    internal bool Enqueue(T peer, byte[] source, int count)
    {
        if (source == null || count <= 0 || count >= 65535 || count > source.Length) return false;
        int size; sizes.TryGetValue(peer, out size);
        if (size + count > PeerLimit || Bytes + count > TotalLimit) return false;
        var copy = new byte[count]; Buffer.BlockCopy(source, 0, copy, 0, count);
        Queue<byte[]> queue;
        if (!queues.TryGetValue(peer, out queue)) { queue = new Queue<byte[]>(); queues.Add(peer, queue); }
        queue.Enqueue(copy); sizes[peer] = size + count; Bytes += count;
        return true;
    }
    internal void Remove(T peer)
    {
        int size;
        if (sizes.TryGetValue(peer, out size)) Bytes -= size;
        sizes.Remove(peer); queues.Remove(peer);
    }
    internal void Clear() { queues.Clear(); sizes.Clear(); Bytes = cursor = 0; }
    internal void Flush(Func<T, bool> alive, Func<T, int, bool> canSend, Func<T, byte[], bool> send)
    {
        var peers = new List<T>(queues.Keys);
        int budget = 256 * 1024;
        for (int i = 0; i < peers.Count && budget > 0; i++)
        {
            T peer = peers[(cursor + i) % peers.Count];
            if (!alive(peer)) { Remove(peer); continue; }
            Queue<byte[]> queue = queues[peer];
            int sent = 0;
            while (queue.Count > 0)
            {
                byte[] bytes = queue.Peek();
                if (sent != 0 && sent + bytes.Length > 32768 || bytes.Length > budget || !canSend(peer, bytes.Length)) break;
                if (!send(peer, bytes)) break;
                queue.Dequeue(); Bytes -= bytes.Length; sizes[peer] -= bytes.Length;
                budget -= bytes.Length; sent += bytes.Length;
            }
            if (queue.Count == 0) Remove(peer);
        }
        cursor = peers.Count == 0 ? 0 : (cursor + 1) % peers.Count;
    }
}
