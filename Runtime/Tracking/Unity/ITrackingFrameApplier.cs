using UnityEngine;

namespace Star67.Tracking.Unity
{
    public interface ITrackingFrameApplier
    {
        TrackingFeatureFlags RequiredFeatures { get; }
        void ApplyFrame(TrackingFrameBuffer frame);
        void ResetState();
    }
}
