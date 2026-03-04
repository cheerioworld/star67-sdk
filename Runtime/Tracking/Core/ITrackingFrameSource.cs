using System;

namespace Star67.Tracking
{
    public interface ITrackingFrameSource : IDisposable
    {
        TrackingConnectionState State { get; }
        TrackingSessionInfo SessionInfo { get; }
        void Update();
        bool TryCopyLatestFrame(TrackingFrameBuffer destination);
    }

    public interface ITrackingPacketSink
    {
        void OnFramePacket(ReadOnlySpan<byte> packet, long captureTimestampUs);
    }
}
