using System;
using System.Net;
using System.Net.Sockets;

namespace Star67.Tracking
{
    public sealed class UdpTrackingSender : IDisposable
    {
        private readonly byte[] _receiveBuffer = new byte[64];
        private readonly byte[] _controlReceiveBuffer = new byte[64];
        private readonly byte[] _sendBuffer = new byte[TrackingProtocol.MaxPacketSize];
        private readonly TrackingSessionInfo _sessionInfo = new TrackingSessionInfo();
        private readonly int _preferredLocalPort;

        private Socket _socket;
        private Socket _controlSocket;
        private EndPoint _receiveEndPoint;
        private EndPoint _controlReceiveEndPoint;
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
            State = TrackingConnectionState.Stopped;
        }

        public int LocalDataPort { get; private set; }
        public TrackingConnectionState State { get; private set; }
        public TrackingSessionInfo SessionInfo => _sessionInfo;
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;
        public uint RemoteSessionToken => _remoteSessionToken;

        public static string[] GetLocalIPv4Addresses()
        {
            return TrackingNetworkUtilities.GetLocalIPv4Addresses();
        }

        public void Start()
        {
            if (_socket != null || _controlSocket != null)
            {
                return;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, _preferredLocalPort));
            _socket.Blocking = false;
            LocalDataPort = ((IPEndPoint)_socket.LocalEndPoint).Port;
            _receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);

            _controlSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _controlSocket.Bind(new IPEndPoint(IPAddress.Any, TrackingProtocol.ControlPort));
            _controlSocket.Blocking = false;
            _controlReceiveEndPoint = new IPEndPoint(IPAddress.Any, 0);

            State = TrackingConnectionState.Listening;
        }

        public void Stop()
        {
            _remoteEndPoint = null;
            _remoteSessionToken = 0;
            _nextSequence = 0;
            _isHandshakeComplete = false;
            _lastConnectRequestTicks = 0;
            _lastHelloTicks = 0;

            try
            {
                _socket?.Close();
            }
            catch
            {
            }

            try
            {
                _controlSocket?.Close();
            }
            catch
            {
            }

            _socket = null;
            _controlSocket = null;
            LocalDataPort = 0;
            State = TrackingConnectionState.Stopped;
        }

        public void Update()
        {
            if (_socket == null || _controlSocket == null)
            {
                return;
            }

            ReceiveRegistrationPackets();
            ReceivePackets();

            if (_remoteEndPoint == null || _remoteSessionToken == 0)
            {
                _isHandshakeComplete = false;
                State = TrackingConnectionState.Listening;
                return;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            if (!_isHandshakeComplete
                && nowTicks - _lastConnectRequestTicks >= TimeSpan.FromMilliseconds(TrackingProtocol.DiscoveryIntervalMs).Ticks)
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
                _isHandshakeComplete = false;
                State = TrackingConnectionState.Listening;
                return false;
            }
            catch (ObjectDisposedException)
            {
                State = TrackingConnectionState.Stopped;
                return false;
            }
        }

        private void ReceiveRegistrationPackets()
        {
            if (_controlSocket == null)
            {
                return;
            }

            while (_controlSocket.Poll(0, SelectMode.SelectRead))
            {
                try
                {
                    int received = _controlSocket.ReceiveFrom(_controlReceiveBuffer, 0, _controlReceiveBuffer.Length, SocketFlags.None, ref _controlReceiveEndPoint);
                    if (received <= 0)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> packet = new ReadOnlySpan<byte>(_controlReceiveBuffer, 0, received);
                    if (!TrackingPacketCodec.TryReadRegisterReceiver(packet, out uint sessionToken, out int receiverDataPort)
                        || receiverDataPort <= 0
                        || receiverDataPort > ushort.MaxValue)
                    {
                        continue;
                    }

                    IPEndPoint receiveEndPoint = (IPEndPoint)_controlReceiveEndPoint;
                    IPEndPoint remoteEndPoint = new IPEndPoint(receiveEndPoint.Address, receiverDataPort);
                    bool endpointChanged = _remoteEndPoint == null
                        || !_remoteEndPoint.Address.Equals(remoteEndPoint.Address)
                        || _remoteEndPoint.Port != remoteEndPoint.Port
                        || _remoteSessionToken != sessionToken;

                    _remoteEndPoint = remoteEndPoint;
                    _remoteSessionToken = sessionToken;
                    if (endpointChanged)
                    {
                        _isHandshakeComplete = false;
                        _lastConnectRequestTicks = 0;
                        _lastHelloTicks = 0;
                        State = TrackingConnectionState.Listening;
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

        private static long GetUtcTimestampUs()
        {
            return DateTime.UtcNow.Ticks / 10L;
        }
    }
}
