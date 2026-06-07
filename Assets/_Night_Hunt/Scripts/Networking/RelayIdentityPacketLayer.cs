using LiteNetLib.Layers;
using System;
using System.Net;
using System.Text;

namespace NightHunt.Networking.Relay
{
    /// <summary>
    /// Adds a small stable routing header to Tugboat/LiteNetLib packets while
    /// custom relay mode is active. The relay reads this header to route by
    /// session/player identity instead of unstable UDP source ports.
    /// </summary>
    public sealed class RelayIdentityPacketLayer : PacketLayerBase
    {
        public const int HeaderSize = 32;
        private const byte Magic0 = (byte)'N';
        private const byte Magic1 = (byte)'H';
        private const byte Magic2 = (byte)'R';
        private const byte Magic3 = (byte)'1';
        private const byte Version = 1;

        public readonly ulong SessionHash;
        public readonly ulong PeerId;
        public readonly ulong Nonce;

        public RelayIdentityPacketLayer(string sessionId, ulong peerId) : this(
            sessionId,
            peerId,
            ComputeHash64($"{Guid.NewGuid():N}:{DateTime.UtcNow.Ticks}"))
        {
        }

        public RelayIdentityPacketLayer(string sessionId, ulong peerId, ulong nonce) : base(HeaderSize)
        {
            SessionHash = ComputeHash64(sessionId ?? string.Empty);
            PeerId = peerId;
            Nonce = nonce == 0UL ? 1UL : nonce;
        }

        public override void ProcessInboundPacket(ref IPEndPoint endPoint, ref byte[] data, ref int length)
        {
            if (length < HeaderSize || !HasRelayHeader(data))
                return;

            int payloadLength = length - HeaderSize;
            Buffer.BlockCopy(data, HeaderSize, data, 0, payloadLength);
            length = payloadLength;
        }

        public override void ProcessOutBoundPacket(ref IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            Buffer.BlockCopy(data, offset, data, offset + HeaderSize, length);
            int header = offset;
            data[header] = Magic0;
            data[header + 1] = Magic1;
            data[header + 2] = Magic2;
            data[header + 3] = Magic3;
            data[header + 4] = Version;
            data[header + 5] = 0;
            data[header + 6] = 0;
            data[header + 7] = 0;
            WriteUInt64BigEndian(data, header + 8, SessionHash);
            WriteUInt64BigEndian(data, header + 16, PeerId);
            WriteUInt64BigEndian(data, header + 24, Nonce);
            length += HeaderSize;
        }

        public static ulong ComputeHash64(string value)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= prime;
                }
                return hash == 0UL ? 1UL : hash;
            }
        }

        private static bool HasRelayHeader(byte[] data)
        {
            return data != null
                && data.Length >= HeaderSize
                && data[0] == Magic0
                && data[1] == Magic1
                && data[2] == Magic2
                && data[3] == Magic3
                && data[4] == Version;
        }

        private static void WriteUInt64BigEndian(byte[] data, int offset, ulong value)
        {
            data[offset] = (byte)(value >> 56);
            data[offset + 1] = (byte)(value >> 48);
            data[offset + 2] = (byte)(value >> 40);
            data[offset + 3] = (byte)(value >> 32);
            data[offset + 4] = (byte)(value >> 24);
            data[offset + 5] = (byte)(value >> 16);
            data[offset + 6] = (byte)(value >> 8);
            data[offset + 7] = (byte)value;
        }
    }
}
