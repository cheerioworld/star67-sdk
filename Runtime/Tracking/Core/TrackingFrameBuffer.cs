using System;
using Unity.Mathematics;

namespace Star67.Tracking
{
    public sealed class TrackedHandData
    {
        public bool IsTracked;
        public float Confidence;
        public float3[] JointPositions { get; }

        public TrackedHandData()
        {
            JointPositions = new float3[TrackingProtocol.HandJointCount];
            Clear();
        }

        public void Clear()
        {
            IsTracked = false;
            Confidence = 0f;
            for (int i = 0; i < JointPositions.Length; i++)
            {
                JointPositions[i] = float3.zero;
            }
        }

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

        public void SetJointPosition(int jointIndex, float x, float y, float z)
        {
            JointPositions[jointIndex] = new float3(x, y, z);
        }

        public void SetJointPosition(HandJointId jointId, float x, float y, float z)
        {
            SetJointPosition((int)jointId, x, y, z);
        }

        public TrackingVector3Value GetJointPositionValue(int jointIndex)
        {
            float3 jointPosition = JointPositions[jointIndex];
            return new TrackingVector3Value(jointPosition.x, jointPosition.y, jointPosition.z);
        }

        public TrackingVector3Value GetJointPositionValue(HandJointId jointId)
        {
            return GetJointPositionValue((int)jointId);
        }
    }

    public sealed class TrackingFrameBuffer
    {
        public ulong Sequence;
        public uint SessionToken;
        public long CaptureTimestampUs;
        public TrackingFeatureFlags Features;
        public TrackingPose CameraWorldPose;
        public TrackingPose HeadPoseCameraSpace;
        public FaceBlendshape[] FaceBlendshapes { get; }
        public TrackedHandData LeftHand { get; }
        public TrackedHandData RightHand { get; }

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
