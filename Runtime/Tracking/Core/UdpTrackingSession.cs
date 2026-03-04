using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Star67.Tracking
{
    public sealed class UdpTrackingSession : ITrackingFrameSource
    {
        private readonly object _sync = new object();
        private readonly TrackingFrameBuffer _latestFrame = new TrackingFrameBuffer();
        private readonly TrackingFrameBuffer _decodeBuffer = new TrackingFrameBuffer();
        private readonly TrackingSessionInfo _sessionInfo = new TrackingSessionInfo();
        private readonly byte[] _receiveBuffer = new byte[TrackingProtocol.MaxPacketSize];
        private readonly byte[] _sendBuffer = new byte[64];
        private readonly int _port;

        private Socket _socket;
        private Thread _thread;
        private volatile bool _isRunning;
        private long _lastActivityTicks;
        private bool _hasFrame;
        private ulong _latestSequence;
        private IPEndPoint _remoteEndPoint;

        public UdpTrackingSession(int port = TrackingProtocol.DataPort, uint? sessionToken = null)
        {
            _port = port;
            SessionToken = sessionToken ?? (uint)Environment.TickCount;
            State = TrackingConnectionState.Stopped;
        }

        public uint SessionToken { get; }
        public TrackingConnectionState State { get; private set; }
        public TrackingSessionInfo SessionInfo => _sessionInfo;
        public ITrackingPacketSink PacketSink { get; set; }

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _socket.ReceiveTimeout = 250;
            _isRunning = true;
            _lastActivityTicks = DateTime.UtcNow.Ticks;
            State = TrackingConnectionState.Listening;

            _thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Star67TrackingUdpSession"
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
            _remoteEndPoint = null;
            State = TrackingConnectionState.Stopped;
        }

        public void Update()
        {
            if ((State == TrackingConnectionState.Connected || State == TrackingConnectionState.Listening)
                && DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastActivityTicks) > TimeSpan.FromMilliseconds(TrackingProtocol.SessionTimeoutMs).Ticks)
            {
                if (State == TrackingConnectionState.Connected)
                {
                    State = TrackingConnectionState.TimedOut;
                }
            }
        }

        public bool TryCopyLatestFrame(TrackingFrameBuffer destination)
        {
            if (destination == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (!_hasFrame)
                {
                    return false;
                }

                destination.CopyFrom(_latestFrame);
                return true;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReceiveLoop()
        {
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            while (_isRunning)
            {
                try
                {
                    int received = _socket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref remote);
                    if (received <= 0)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> packet = new ReadOnlySpan<byte>(_receiveBuffer, 0, received);
                    HandlePacket(packet, (IPEndPoint)remote);
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

        private void HandlePacket(ReadOnlySpan<byte> packet, IPEndPoint remoteEndPoint)
        {
            if (!TrackingPacketCodec.TryReadPacketHeader(packet, out TrackingPacketType packetType, out uint sessionToken, out _)
                || sessionToken != SessionToken)
            {
                return;
            }

            switch (packetType)
            {
                case TrackingPacketType.ConnectRequest:
                    if (!TrackingPacketCodec.TryReadConnectRequest(packet, out _))
                    {
                        return;
                    }

                    _remoteEndPoint = remoteEndPoint;
                    State = TrackingConnectionState.Listening;
                    Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
                    if (TrackingPacketCodec.TryWriteConnectAck(SessionToken, _sendBuffer, out int ackBytes))
                    {
                        _socket.SendTo(_sendBuffer, 0, ackBytes, SocketFlags.None, remoteEndPoint);
                    }
                    break;

                case TrackingPacketType.Hello:
                    if (!AcceptRemote(remoteEndPoint) || !TrackingPacketCodec.TryReadHelloPacket(packet, out _, _sessionInfo))
                    {
                        return;
                    }

                    State = TrackingConnectionState.Connected;
                    Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
                    break;

                case TrackingPacketType.Frame:
                    if (!AcceptRemote(remoteEndPoint) || !TrackingPacketCodec.TryReadFramePacket(packet, _decodeBuffer))
                    {
                        return;
                    }

                    if (_decodeBuffer.Sequence <= _latestSequence)
                    {
                        return;
                    }

                    lock (_sync)
                    {
                        _latestFrame.CopyFrom(_decodeBuffer);
                        _latestSequence = _decodeBuffer.Sequence;
                        _hasFrame = true;
                    }

                    State = TrackingConnectionState.Connected;
                    Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
                    PacketSink?.OnFramePacket(packet, _decodeBuffer.CaptureTimestampUs);
                    break;
            }
        }

        private bool AcceptRemote(IPEndPoint remoteEndPoint)
        {
            if (_remoteEndPoint == null)
            {
                _remoteEndPoint = remoteEndPoint;
                return true;
            }

            return _remoteEndPoint.Equals(remoteEndPoint);
        }
    }
}
