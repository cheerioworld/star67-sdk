using UnityEngine;

namespace Star67.Tracking.Unity
{
    /// <summary>
    /// Applies canonical tracking frames to Unity targets such as rigs, meshes, or control objects.
    /// </summary>
    public interface ITrackingFrameApplier
    {
        /// <summary>
        /// Gets the minimum feature set required for this applier to produce meaningful output.
        /// </summary>
        TrackingFeatureFlags RequiredFeatures { get; }

        /// <summary>
        /// Applies a single tracking frame to this applier's target.
        /// </summary>
        /// <param name="frame">Frame to apply.</param>
        void ApplyFrame(TrackingFrameBuffer frame);

        /// <summary>
        /// Clears any transient state and returns outputs to their neutral state.
        /// </summary>
        void ResetState();
    }
}
