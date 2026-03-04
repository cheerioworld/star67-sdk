using System;
using System.Net;
using System.Net.Sockets;

namespace Star67.Tracking
{
    public sealed class UdpTrackingSender : IDisposable
    {
        private readonly byte[] _receiveBuffer = new byte[64];
        private readonly byte[] _sendBuffer = new byte[TrackingProtocol.MaxPacketSize];
        private readonly TrackingSessionInfo _sessionInfo = new TrackingSessionInfo();
        private readonly DiscoveryAnnouncement _localAnnouncement;
        private readonly int _preferredLocalPort;

        private Socket _socket;
        private TrackingDiscoveryService _discoveryService;
        private EndPoint _receiveEndPoint;
        private IPEndPoint _remoteEndPoint;
        private uint _remoteSessionToken;
        private ulong _nextSequence;
        private bool _isHandshakeComplete;
        private long _lastConnectRequestTicks;
        private long _lastHelloTicks;

        public UdpTrackingSender(TrackingSessionInfo sessionInfo = null, int preferredLocalPort = 0)
        {
            _sessionInfo.CopyFrom(sessionInfo);
            _preferredLocalPort = preferredLocalPort;
            LocalSessionToken = unchecked((uint)Environment.TickCount);
            _localAnnouncement = new DiscoveryAnnouncement
            {
                Role = DiscoveryRole.App,
                SessionToken = LocalSessionToken
            };
            State = TrackingConnectionState.Stopped;
        }

        public uint LocalSessionToken { get; }
        public int LocalDataPort { get; private set; }
        public TrackingConnectionState State { get; private set; }
        public TrackingSessionInfo SessionInfo => _sessionInfo;
        public DiscoveryRegistry Registry => _discoveryService?.Registry;
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;
        public uint RemoteSessionToken => _remoteSessionToken;

        public void Start()
        {
            if (_socket != null)
            {
                return;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, _preferredLocalPort));
            _socket.Blocking = false;
            LocalDataPort = ((IPEndPoint)_socket.LocalEndPoint).Port;
            _receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);

            SyncAnnouncement();
            _discoveryService = new TrackingDiscoveryService(_localAnnouncement);
            _discoveryService.Start();
            State = TrackingConnectionState.Discovering;
        }

        public void Stop()
        {
            _remoteEndPoint = null;
            _remoteSessionToken = 0;
            _nextSequence = 0;
            _isHandshakeComplete = false;
            _lastConnectRequestTicks = 0;
            _lastHelloTicks = 0;

            _discoveryService?.Dispose();
            _discoveryService = null;

            try
            {
                _socket?.Close();
            }
            catch
            {
            }

            _socket = null;
            LocalDataPort = 0;
            State = TrackingConnectionState.Stopped;
        }

        public void Update()
        {
            if (_socket == null || _discoveryService == null)
            {
                return;
            }

            SyncAnnouncement();

            DiscoveryRegistryEntry[] editors = _discoveryService.Registry.Snapshot(TimeSpan.FromMilliseconds(TrackingProtocol.SessionTimeoutMs));
            if (!TrySelectEditor(editors, out DiscoveryRegistryEntry editor))
            {
                _remoteEndPoint = null;
                _remoteSessionToken = 0;
                _isHandshakeComplete = false;
                State = TrackingConnectionState.Discovering;
                return;
            }

            IPEndPoint editorEndPoint = new IPEndPoint(editor.RemoteEndPoint.Address, editor.Announcement.DataPort);
            bool endpointChanged = _remoteEndPoint == null
                || !_remoteEndPoint.Address.Equals(editorEndPoint.Address)
                || _remoteEndPoint.Port != editorEndPoint.Port
                || _remoteSessionToken != editor.Announcement.SessionToken;

            if (endpointChanged)
            {
                _isHandshakeComplete = false;
                _lastConnectRequestTicks = 0;
                _lastHelloTicks = 0;
            }

            _remoteEndPoint = editorEndPoint;
            _remoteSessionToken = editor.Announcement.SessionToken;

            ReceivePackets();

            long nowTicks = DateTime.UtcNow.Ticks;
            if (endpointChanged || nowTicks - _lastConnectRequestTicks >= TimeSpan.FromMilliseconds(TrackingProtocol.DiscoveryIntervalMs).Ticks)
            {
                SendConnectRequest();
                _lastConnectRequestTicks = nowTicks;
            }

            if (_isHandshakeComplete
                && nowTicks - _lastHelloTicks >= TimeSpan.FromMilliseconds(TrackingProtocol.DiscoveryIntervalMs).Ticks)
            {
                SendHello();
                _lastHelloTicks = nowTicks;
            }

            State = _isHandshakeComplete ? TrackingConnectionState.Connected : TrackingConnectionState.Listening;
        }

        public bool SendFrame(TrackingFrameBuffer frame)
        {
            if (_socket == null || _remoteEndPoint == null || _remoteSessionToken == 0 || !_isHandshakeComplete || frame == null)
            {
                return false;
            }

            if (frame.Sequence == 0)
            {
                frame.Sequence = ++_nextSequence;
            }
            else if (frame.Sequence > _nextSequence)
            {
                _nextSequence = frame.Sequence;
            }

            if (frame.CaptureTimestampUs == 0)
            {
                frame.CaptureTimestampUs = GetUtcTimestampUs();
            }

            frame.SessionToken = _remoteSessionToken;
            if (!TrackingPacketCodec.TryWriteFramePacket(frame, _sendBuffer, out int written))
            {
                return false;
            }

            return TrySendPacket(written);
        }

        public void Dispose()
        {
            Stop();
        }

        private void SyncAnnouncement()
        {
            _localAnnouncement.ProtocolVersion = TrackingProtocol.ProtocolVersion;
            _localAnnouncement.DeviceName = _sessionInfo.DeviceName ?? string.Empty;
            _localAnnouncement.DataPort = LocalDataPort == 0 ? TrackingProtocol.DataPort : LocalDataPort;
            _localAnnouncement.PackageVersion = _sessionInfo.AppVersion ?? string.Empty;
            _localAnnouncement.AvailableFeatures = _sessionInfo.AvailableFeatures;
        }

        private void SendConnectRequest()
        {
            if (_remoteEndPoint == null || _remoteSessionToken == 0)
            {
                return;
            }

            if (TrackingPacketCodec.TryWriteConnectRequest(_remoteSessionToken, _sendBuffer, out int written))
            {
                TrySendPacket(written);
            }
        }

        private void SendHello()
        {
            if (_remoteEndPoint == null || _remoteSessionToken == 0)
            {
                return;
            }

            if (TrackingPacketCodec.TryWriteHelloPacket(_remoteSessionToken, _sessionInfo, _sendBuffer, out int written))
            {
                TrySendPacket(written);
            }
        }

        private bool TrySendPacket(int written)
        {
            if (_socket == null || _remoteEndPoint == null || written <= 0)
            {
                return false;
            }

            try
            {
                _socket.SendTo(_sendBuffer, 0, written, SocketFlags.None, _remoteEndPoint);
                return true;
            }
            catch (SocketException)
            {
                State = TrackingConnectionState.Discovering;
                return false;
            }
            catch (ObjectDisposedException)
            {
                State = TrackingConnectionState.Stopped;
                return false;
            }
        }

        private void ReceivePackets()
        {
            if (_socket == null)
            {
                return;
            }

            while (_socket.Poll(0, SelectMode.SelectRead))
            {
                try
                {
                    int received = _socket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref _receiveEndPoint);
                    if (received <= 0 || _remoteEndPoint == null)
                    {
                        continue;
                    }

                    IPEndPoint remoteEndPoint = (IPEndPoint)_receiveEndPoint;
                    if (!_remoteEndPoint.Equals(remoteEndPoint))
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> packet = new ReadOnlySpan<byte>(_receiveBuffer, 0, received);
                    if (TrackingPacketCodec.TryReadConnectAck(packet, out uint sessionToken) && sessionToken == _remoteSessionToken)
                    {
                        _isHandshakeComplete = true;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock || ex.SocketErrorCode == SocketError.TimedOut)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private static bool TrySelectEditor(DiscoveryRegistryEntry[] entries, out DiscoveryRegistryEntry bestEntry)
        {
            bestEntry = default;
            bool found = false;
            DateTime latestSeenUtc = DateTime.MinValue;

            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                DiscoveryRegistryEntry entry = entries[i];
                if (entry.Announcement == null || entry.Announcement.Role != DiscoveryRole.Editor)
                {
                    continue;
                }

                if (!found || entry.LastSeenUtc > latestSeenUtc)
                {
                    latestSeenUtc = entry.LastSeenUtc;
                    bestEntry = entry;
                    found = true;
                }
            }

            return found;
        }

        private static long GetUtcTimestampUs()
        {
            return DateTime.UtcNow.Ticks / 10L;
        }
    }
}
