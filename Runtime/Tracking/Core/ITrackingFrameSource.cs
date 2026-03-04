using System;

namespace Star67.Tracking
{
    /// <summary>
    /// Represents a source that publishes canonical tracking frames (live or playback) to consumers.
    /// </summary>
    public interface ITrackingFrameSource : IDisposable
    {
        /// <summary>
        /// Gets the current connection or playback state of this source.
        /// </summary>
        TrackingConnectionState State { get; }

        /// <summary>
        /// Gets metadata for the active source session.
        /// </summary>
        TrackingSessionInfo SessionInfo { get; }

        /// <summary>
        /// Advances the source by one tick and refreshes any internal state.
        /// </summary>
        void Update();

        /// <summary>
        /// Copies the latest available frame into <paramref name="destination"/>.
        /// </summary>
        /// <param name="destination">Reusable destination buffer that receives the latest frame.</param>
        /// <returns><c>true</c> if a frame was available and copied; otherwise <c>false</c>.</returns>
        bool TryCopyLatestFrame(TrackingFrameBuffer destination);
    }

    /// <summary>
    /// Receives raw frame packets that were accepted by a live source.
    /// </summary>
    public interface ITrackingPacketSink
    {
        /// <summary>
        /// Called when a frame packet is accepted by the source.
        /// </summary>
        /// <param name="packet">Packet bytes for a single frame.</param>
        /// <param name="captureTimestampUs">Capture timestamp from the frame packet in microseconds.</param>
        void OnFramePacket(ReadOnlySpan<byte> packet, long captureTimestampUs);
    }
}
