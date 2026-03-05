using System;
using Unity.Mathematics;

namespace Star67.Tracking
{
    /// <summary>
    /// Mutable hand-tracking payload that stores a fixed set of joint positions.
    /// </summary>
    public sealed class TrackedHandData
    {
        /// <summary>
        /// Indicates whether this hand payload currently contains tracked data.
        /// </summary>
        public bool IsTracked;

        /// <summary>
        /// Confidence value for this hand sample, usually in the range [0, 1].
        /// </summary>
        public float Confidence;

        /// <summary>
        /// Joint positions in camera-local space using <see cref="HandJointId"/> ordering.
        /// </summary>
        public float3[] JointPositions { get; }

        /// <summary>
        /// Creates a hand payload with preallocated joint storage.
        /// </summary>
        public TrackedHandData()
        {
            JointPositions = new float3[TrackingProtocol.HandJointCount];
            Clear();
        }

        /// <summary>
        /// Resets the hand payload to an untracked state.
        /// </summary>
        public void Clear()
        {
            IsTracked = false;
            Confidence = 0f;
            for (int i = 0; i < JointPositions.Length; i++)
            {
                JointPositions[i] = float3.zero;
            }
        }

        /// <summary>
        /// Copies all hand data from <paramref name="source"/>.
        /// </summary>
        /// <param name="source">Source hand payload to copy from.</param>
        public void CopyFrom(TrackedHandData source)
        {
            if (source == null)
            {
                Clear();
                return;
            }

            IsTracked = source.IsTracked;
            Confidence = source.Confidence;
            Array.Copy(source.JointPositions, JointPositions, JointPositions.Length);
        }

        /// <summary>
        /// Sets a joint position by raw joint index.
        /// </summary>
        /// <param name="jointIndex">Joint index in protocol ordering.</param>
        /// <param name="x">Joint X position in camera-local space.</param>
        /// <param name="y">Joint Y position in camera-local space.</param>
        /// <param name="z">Joint Z position in camera-local space.</param>
        public void SetJointPosition(int jointIndex, float x, float y, float z)
        {
            JointPositions[jointIndex] = new float3(x, y, z);
        }

        /// <summary>
        /// Sets a joint position by strongly-typed joint id.
        /// </summary>
        /// <param name="jointId">Joint identifier.</param>
        /// <param name="x">Joint X position in camera-local space.</param>
        /// <param name="y">Joint Y position in camera-local space.</param>
        /// <param name="z">Joint Z position in camera-local space.</param>
        public void SetJointPosition(HandJointId jointId, float x, float y, float z)
        {
            SetJointPosition((int)jointId, x, y, z);
        }

        /// <summary>
        /// Gets a joint position as a serializable value type.
        /// </summary>
        /// <param name="jointIndex">Joint index in protocol ordering.</param>
        /// <returns>The requested joint position.</returns>
        public TrackingVector3Value GetJointPositionValue(int jointIndex)
        {
            float3 jointPosition = JointPositions[jointIndex];
            return new TrackingVector3Value(jointPosition.x, jointPosition.y, jointPosition.z);
        }

        /// <summary>
        /// Gets a joint position as a serializable value type.
        /// </summary>
        /// <param name="jointId">Joint identifier.</param>
        /// <returns>The requested joint position.</returns>
        public TrackingVector3Value GetJointPositionValue(HandJointId jointId)
        {
            return GetJointPositionValue((int)jointId);
        }
    }

    /// <summary>
    /// Reusable canonical tracking frame buffer used by transport, recording, and runtime appliers.
    /// </summary>
    public sealed class TrackingFrameBuffer
    {
        /// <summary>
        /// Monotonic frame sequence number within a source session.
        /// </summary>
        public ulong Sequence;

        /// <summary>
        /// Session token associated with the packet source.
        /// </summary>
        public uint SessionToken;

        /// <summary>
        /// Capture timestamp in microseconds from the sender clock.
        /// </summary>
        public long CaptureTimestampUs;

        /// <summary>
        /// Feature mask describing which payload sections are valid for this frame.
        /// </summary>
        public TrackingFeatureFlags Features;

        /// <summary>
        /// ARKit-derived camera world pose for this frame.
        /// </summary>
        public TrackingPose CameraWorldPose;

        /// <summary>
        /// Head pose relative to the camera pose for this frame.
        /// </summary>
        public TrackingPose HeadPoseCameraSpace;

        /// <summary>
        /// Face blendshape weights in protocol order.
        /// </summary>
        public FaceBlendshape[] FaceBlendshapes;

        /// <summary>
        /// Left-hand tracking data.
        /// </summary>
        public TrackedHandData LeftHand;

        /// <summary>
        /// Right-hand tracking data.
        /// </summary>
        public TrackedHandData RightHand;

        /// <summary>
        /// Creates a frame buffer with fixed-size backing storage for all payload sections.
        /// </summary>
        public TrackingFrameBuffer()
        {
            FaceBlendshapes = new FaceBlendshape[TrackingProtocol.FaceBlendshapeCount];
            for (int i = 0; i < FaceBlendshapes.Length; i++)
            {
                FaceBlendshapes[i].location = (FaceBlendshapeLocation)i;
                FaceBlendshapes[i].weight = 0f;
            }

            LeftHand = new TrackedHandData();
            RightHand = new TrackedHandData();
            Clear();
        }

        /// <summary>
        /// Resets this instance to its default state without reallocating backing arrays.
        /// </summary>
        public void Clear()
        {
            Sequence = 0;
            SessionToken = 0;
            CaptureTimestampUs = 0;
            Features = TrackingFeatureFlags.None;
            CameraWorldPose = TrackingPose.Identity;
            HeadPoseCameraSpace = TrackingPose.Identity;
            for (int i = 0; i < FaceBlendshapes.Length; i++)
            {
                FaceBlendshapes[i].weight = 0f;
            }

            LeftHand.Clear();
            RightHand.Clear();
        }

        /// <summary>
        /// Copies all frame data from <paramref name="source"/> into this instance.
        /// </summary>
        /// <param name="source">Source frame to copy from.</param>
        public void CopyFrom(TrackingFrameBuffer source)
        {
            if (source == null)
            {
                Clear();
                return;
            }

            Sequence = source.Sequence;
            SessionToken = source.SessionToken;
            CaptureTimestampUs = source.CaptureTimestampUs;
            Features = source.Features;
            CameraWorldPose = source.CameraWorldPose;
            HeadPoseCameraSpace = source.HeadPoseCameraSpace;
            for (int i = 0; i < FaceBlendshapes.Length; i++)
            {
                FaceBlendshapes[i].weight = source.FaceBlendshapes[i].weight;
            }

            LeftHand.CopyFrom(source.LeftHand);
            RightHand.CopyFrom(source.RightHand);
        }

        public override string ToString()
        {
            float jawOpenWeight = FaceBlendshapes != null && FaceBlendshapes.Length > (int)FaceBlendshapeLocation.JawOpen
                ? FaceBlendshapes[(int)FaceBlendshapeLocation.JawOpen].weight
                : 0f;

            return $"TrackingFrameBuffer(seq={Sequence}, session={SessionToken}, tsUs={CaptureTimestampUs}, " +
                   $"features={Features}, cameraPos={CameraWorldPose.Position}, headPos={HeadPoseCameraSpace.Position}, " +
                   $"leftTracked={LeftHand?.IsTracked ?? false}, rightTracked={RightHand?.IsTracked ?? false}, jawOpen={jawOpenWeight:0.###})";
        }
    }

    public sealed class TrackingSessionInfo
    {
        public byte ProtocolVersion { get; set; } = TrackingProtocol.ProtocolVersion;
        public string AppVersion { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public TrackingFeatureFlags AvailableFeatures { get; set; }
        public float NominalFps { get; set; }

        public TrackingSessionInfo Clone()
        {
            return new TrackingSessionInfo
            {
                ProtocolVersion = ProtocolVersion,
                AppVersion = AppVersion,
                DeviceName = DeviceName,
                AvailableFeatures = AvailableFeatures,
                NominalFps = NominalFps
            };
        }

        public void CopyFrom(TrackingSessionInfo other)
        {
            if (other == null)
            {
                ProtocolVersion = TrackingProtocol.ProtocolVersion;
                AppVersion = string.Empty;
                DeviceName = string.Empty;
                AvailableFeatures = TrackingFeatureFlags.None;
                NominalFps = 0f;
                return;
            }

            ProtocolVersion = other.ProtocolVersion;
            AppVersion = other.AppVersion ?? string.Empty;
            DeviceName = other.DeviceName ?? string.Empty;
            AvailableFeatures = other.AvailableFeatures;
            NominalFps = other.NominalFps;
        }
    }
}
