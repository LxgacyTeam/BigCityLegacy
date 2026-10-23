using System;
using System.IO;

// One bounded transfer in each direction per connection, eight small packets
// in flight. ACKs prevent a car/late-join burst from filling UNet's global queues.
internal sealed class LegacyCarXmlTransfer
{
    internal const int ChunkSize = 900, Window = 8;
    internal uint Id, ObjectId;
    internal byte Operation;
    internal string File, Token;
    internal bool TestDrive, Begun;
    internal byte[] Bytes;
    internal int Received, Sent, Acknowledged;
    internal float LastProgress;
    internal int Count { get { return (Bytes.Length + ChunkSize - 1) / ChunkSize; } }

    internal static LegacyCarXmlTransfer Receiving(uint id, uint objectId, byte operation, string file, string token, bool testDrive, int length, float now)
    {
        if (id == 0 || operation > 2 || file == null || file.Length > 128 || !LegacyCarXmlCodec.IsHash(token) || length <= 0 || length > LegacyCarXmlCodec.MaxBytes)
            throw new InvalidDataException("Invalid car XML transfer header.");
        return new LegacyCarXmlTransfer { Id = id, ObjectId = objectId, Operation = operation, File = file,
            Token = token, TestDrive = testDrive, Bytes = new byte[length], LastProgress = now };
    }
    internal void Add(int index, byte[] chunk, float now)
    {
        int expected = Math.Min(ChunkSize, Bytes.Length - Received * ChunkSize);
        if (index != Received || chunk == null || chunk.Length != expected || expected <= 0)
            throw new InvalidDataException("Invalid car XML chunk sequence/size.");
        Buffer.BlockCopy(chunk, 0, Bytes, Received * ChunkSize, chunk.Length);
        Received++;
        LastProgress = now;
    }
    internal void Ack(int next, float now)
    {
        if (next < Acknowledged || next > Sent) throw new InvalidDataException("Invalid car XML acknowledgement.");
        if (next > Acknowledged) LastProgress = now;
        Acknowledged = next;
    }
}
