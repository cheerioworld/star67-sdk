using System;
using System.Net;
using System.Net.Sockets;

namespace Star67.Tracking
{
    /// <summary>
    /// Explicit-IP UDP receiver that registers itself with a remote sender over the control port.
    /// </summary>
    public sealed class UdpTrackingReceiverClient : ITrackingFrameSource
    {
        private readonly UdpTrackingSession _session;
        private readonly IPEndPoint _serverControlEndPoint;
        private readonly byte[] _sendBuffer = new byte[64];

        private Socket _registrationSocket;
        private bool _isStarted;
        private long _lastRegistrationTicks;

        public UdpTrackingReceiverClient(IPAddress serverIpAddress, int serverControlPort = TrackingProtocol.ControlPort, int localDataPort = TrackingProtocol.DataPort, uint? sessionToken = null)
        {
            if (serverIpAddress == null)
            {
                throw new ArgumentNullException(nameof(serverIpAddress));
            }

            if (serverIpAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 server addresses are supported.", nameof(serverIpAddress));
            }

            _serverControlEndPoint = new IPEndPoint(serverIpAddress, serverControlPort);
            _session = new UdpTrackingSession(localDataPort, sessionToken)
            {
                AllowedRemoteAddress = serverIpAddress
            };
        }

        public TrackingConnectionState State => _session.State;
        public TrackingSessionInfo SessionInfo => _session.SessionInfo;
        public uint SessionToken => _session.SessionToken;
        public int LocalDataPort => _session.Port;
        public IPAddress ServerIpAddress => _serverControlEndPoint.Address;
        public int ServerControlPort => _serverControlEndPoint.Port;
        public IPEndPoint RemoteEndPoint => _session.RemoteEndPoint;

        public ITrackingPacketSink PacketSink
        {
            get => _session.PacketSink;
            set => _session.PacketSink = value;
        }

        public void Start()
        {
            if (_isStarted)
            {
                return;
            }

            _session.Start();
            try
            {
                _registrationSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _registrationSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            }
            catch
            {
                _session.Stop();
                _registrationSocket?.Close();
                _registrationSocket = null;
                throw;
            }

            _isStarted = true;
            _lastRegistrationTicks = 0;
        }

        public void Stop()
        {
            _isStarted = false;

            try
            {
                _registrationSocket?.Close();
            }
            catch
            {
            }

            _registrationSocket = null;
            _lastRegistrationTicks = 0;
            _session.Stop();
        }

        public void Update()
        {
            if (!_isStarted)
            {
                return;
            }

            _session.Update();

            if (_session.State == TrackingConnectionState.Connected)
            {
                return;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            if (nowTicks - _lastRegistrationTicks < TimeSpan.FromMilliseconds(TrackingProtocol.DiscoveryIntervalMs).Ticks)
            {
                return;
            }

            SendRegistration();
            _lastRegistrationTicks = nowTicks;
        }

        public bool TryCopyLatestFrame(TrackingFrameBuffer destination)
        {
            return _session.TryCopyLatestFrame(destination);
        }

        public void Dispose()
        {
            Stop();
        }

        private void SendRegistration()
        {
            if (_registrationSocket == null)
            {
                return;
            }

            if (!TrackingPacketCodec.TryWriteRegisterReceiver(SessionToken, LocalDataPort, _sendBuffer, out int written))
            {
                return;
            }

            try
            {
                _registrationSocket.SendTo(_sendBuffer, 0, written, SocketFlags.None, _serverControlEndPoint);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
