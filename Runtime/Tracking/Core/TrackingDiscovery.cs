using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Star67.Tracking
{
    public sealed class DiscoveryAnnouncement
    {
        public byte ProtocolVersion { get; set; } = TrackingProtocol.ProtocolVersion;
        public DiscoveryRole Role { get; set; }
        public uint SessionToken { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int DataPort { get; set; } = TrackingProtocol.DataPort;
        public string PackageVersion { get; set; } = string.Empty;
        public TrackingFeatureFlags AvailableFeatures { get; set; }
    }

    public readonly struct DiscoveryRegistryEntry
    {
        public DiscoveryRegistryEntry(DiscoveryAnnouncement announcement, IPEndPoint remoteEndPoint, DateTime lastSeenUtc)
        {
            Announcement = announcement;
            RemoteEndPoint = remoteEndPoint;
            LastSeenUtc = lastSeenUtc;
        }

        public DiscoveryAnnouncement Announcement { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public DateTime LastSeenUtc { get; }
    }

    public sealed class DiscoveryRegistry
    {
        private readonly Dictionary<string, DiscoveryRegistryEntry> _entries = new Dictionary<string, DiscoveryRegistryEntry>(StringComparer.Ordinal);
        private readonly object _sync = new object();

        public void Upsert(DiscoveryAnnouncement announcement, IPEndPoint remoteEndPoint)
        {
            if (announcement == null || remoteEndPoint == null)
            {
                return;
            }

            lock (_sync)
            {
                string key = remoteEndPoint.ToString();
                _entries[key] = new DiscoveryRegistryEntry(announcement, remoteEndPoint, DateTime.UtcNow);
            }
        }

        public DiscoveryRegistryEntry[] Snapshot(TimeSpan maxAge)
        {
            DateTime cutoff = DateTime.UtcNow - maxAge;
            lock (_sync)
            {
                var active = new List<DiscoveryRegistryEntry>(_entries.Count);
                foreach (KeyValuePair<string, DiscoveryRegistryEntry> pair in _entries)
                {
                    if (pair.Value.LastSeenUtc >= cutoff)
                    {
                        active.Add(pair.Value);
                    }
                }

                return active.ToArray();
            }
        }
    }

    public static class TrackingDiscoveryCodec
    {
        public static bool TryWriteDiscoveryAnnouncement(DiscoveryAnnouncement announcement, Span<byte> destination, out int written)
        {
            written = 0;
            if (announcement == null)
            {
                return false;
            }

            int offset = 0;
            if (!TryWriteByte(TrackingProtocol.ProtocolVersion, destination, ref offset)
                || !TryWriteByte((byte)TrackingPacketType.Discovery, destination, ref offset)
                || !TryWriteByte((byte)announcement.Role, destination, ref offset)
                || !TryWriteUInt32(announcement.SessionToken, destination, ref offset)
                || !TryWriteString(announcement.DeviceName, destination, ref offset)
                || !TryWriteInt32(announcement.DataPort, destination, ref offset)
                || !TryWriteString(announcement.PackageVersion, destination, ref offset)
                || !TryWriteUInt32((uint)announcement.AvailableFeatures, destination, ref offset))
            {
                return false;
            }

            written = offset;
            return true;
        }

        public static bool TryReadDiscoveryAnnouncement(ReadOnlySpan<byte> source, out DiscoveryAnnouncement announcement)
        {
            announcement = null;
            int offset = 0;
            if (!TryReadByte(source, ref offset, out byte protocolVersion)
                || protocolVersion != TrackingProtocol.ProtocolVersion
                || !TryReadByte(source, ref offset, out byte packetType)
                || packetType != (byte)TrackingPacketType.Discovery
                || !TryReadByte(source, ref offset, out byte role)
                || !TryReadUInt32(source, ref offset, out uint sessionToken)
                || !TryReadString(source, ref offset, out string deviceName)
                || !TryReadInt32(source, ref offset, out int dataPort)
                || !TryReadString(source, ref offset, out string packageVersion)
                || !TryReadUInt32(source, ref offset, out uint features))
            {
                return false;
            }

            announcement = new DiscoveryAnnouncement
            {
                ProtocolVersion = protocolVersion,
                Role = (DiscoveryRole)role,
                SessionToken = sessionToken,
                DeviceName = deviceName,
                DataPort = dataPort,
                PackageVersion = packageVersion,
                AvailableFeatures = (TrackingFeatureFlags)features
            };
            return true;
        }

        private static bool TryWriteByte(byte value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 1)
            {
                return false;
            }

            destination[offset++] = value;
            return true;
        }

        private static bool TryReadByte(ReadOnlySpan<byte> source, ref int offset, out byte value)
        {
            value = 0;
            if (source.Length - offset < 1)
            {
                return false;
            }

            value = source[offset++];
            return true;
        }

        private static bool TryWriteUInt32(uint value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 4)
            {
                return false;
            }

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
            offset += 4;
            return true;
        }

        private static bool TryReadUInt32(ReadOnlySpan<byte> source, ref int offset, out uint value)
        {
            value = 0;
            if (source.Length - offset < 4)
            {
                return false;
            }

            value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
            offset += 4;
            return true;
        }

        private static bool TryWriteInt32(int value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 4)
            {
                return false;
            }

            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), value);
            offset += 4;
            return true;
        }

        private static bool TryReadInt32(ReadOnlySpan<byte> source, ref int offset, out int value)
        {
            value = 0;
            if (source.Length - offset < 4)
            {
                return false;
            }

            value = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, 4));
            offset += 4;
            return true;
        }

        private static bool TryWriteString(string value, Span<byte> destination, ref int offset)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > ushort.MaxValue || destination.Length - offset < bytes.Length + 2)
            {
                return false;
            }

            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), (ushort)bytes.Length);
            offset += 2;
            bytes.AsSpan().CopyTo(destination.Slice(offset, bytes.Length));
            offset += bytes.Length;
            return true;
        }

        private static bool TryReadString(ReadOnlySpan<byte> source, ref int offset, out string value)
        {
            value = string.Empty;
            if (source.Length - offset < 2)
            {
                return false;
            }

            ushort length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));
            offset += 2;
            if (source.Length - offset < length)
            {
                return false;
            }

            value = System.Text.Encoding.UTF8.GetString(source.Slice(offset, length).ToArray());
            offset += length;
            return true;
        }
    }

    public sealed class TrackingDiscoveryService : IDisposable
    {
        private readonly DiscoveryAnnouncement _localAnnouncement;
        private readonly DiscoveryRegistry _registry = new DiscoveryRegistry();
        private readonly byte[] _sendBuffer = new byte[256];
        private readonly byte[] _receiveBuffer = new byte[512];

        private Socket _socket;
        private Thread _thread;
        private volatile bool _isRunning;

        public TrackingDiscoveryService(DiscoveryAnnouncement localAnnouncement)
        {
            _localAnnouncement = localAnnouncement ?? throw new ArgumentNullException(nameof(localAnnouncement));
        }

        public DiscoveryRegistry Registry => _registry;

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _socket.Bind(new IPEndPoint(IPAddress.Any, TrackingProtocol.DiscoveryPort));
            _socket.ReceiveTimeout = 250;

            _isRunning = true;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "Star67TrackingDiscovery"
            };
            _thread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _socket?.Close();
            }
            catch
            {
            }

            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(500);
            }

            _thread = null;
            _socket = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private void Loop()
        {
            long nextAdvertiseTicks = 0;
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                if (nowTicks >= nextAdvertiseTicks)
                {
                    SendAnnouncement();
                    nextAdvertiseTicks = nowTicks + TimeSpan.FromMilliseconds(TrackingProtocol.DiscoveryIntervalMs).Ticks;
                }

                try
                {
                    int received = _socket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref remote);
                    if (received <= 0)
                    {
                        continue;
                    }

                    if (TrackingDiscoveryCodec.TryReadDiscoveryAnnouncement(new ReadOnlySpan<byte>(_receiveBuffer, 0, received), out DiscoveryAnnouncement announcement))
                    {
                        if (announcement.Role == _localAnnouncement.Role && announcement.SessionToken == _localAnnouncement.SessionToken)
                        {
                            continue;
                        }

                        _registry.Upsert(announcement, (IPEndPoint)remote);
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut || ex.SocketErrorCode == SocketError.Interrupted)
                {
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void SendAnnouncement()
        {
            if (!TrackingDiscoveryCodec.TryWriteDiscoveryAnnouncement(_localAnnouncement, _sendBuffer, out int written))
            {
                return;
            }

            _socket.SendTo(_sendBuffer, 0, written, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, TrackingProtocol.DiscoveryPort));
        }
    }
}
