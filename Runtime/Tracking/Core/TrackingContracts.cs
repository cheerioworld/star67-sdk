using System;
using Unity.Mathematics;

namespace Star67.Tracking
{
    [Serializable]
    public struct TrackingVector3Value
    {
        public float X;
        public float Y;
        public float Z;

        public TrackingVector3Value(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    [Serializable]
    public struct TrackingQuaternionValue
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public TrackingQuaternionValue(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    [Flags]
    public enum TrackingFeatureFlags
    {
        None = 0,
        Face = 1 << 0,
        HeadPose = 1 << 1,
        CameraWorldPose = 1 << 2,
        LeftHand = 1 << 3,
        RightHand = 1 << 4
    }

    public enum TrackingConnectionState
    {
        Stopped = 0,
        Discovering = 1,
        Listening = 2,
        Connected = 3,
        TimedOut = 4,
        Playback = 5
    }

    public enum DiscoveryRole
    {
        Editor = 0,
        App = 1
    }

    public enum TrackingPacketType : byte
    {
        ConnectRequest = 1,
        ConnectAck = 2,
        Hello = 3,
        Frame = 4,
        Discovery = 5,
        RegisterReceiver = 6
    }

    [Serializable]
    public struct TrackingPose
    {
        public float3 Position;
        public quaternion Rotation;

        public TrackingVector3Value GetPositionValue()
        {
            return new TrackingVector3Value(Position.x, Position.y, Position.z);
        }

        public TrackingQuaternionValue GetRotationValue()
        {
            return new TrackingQuaternionValue(Rotation.value.x, Rotation.value.y, Rotation.value.z, Rotation.value.w);
        }

        public static TrackingPose FromPositionAndRotation(float px, float py, float pz, float qx, float qy, float qz, float qw)
        {
            return new TrackingPose
            {
                Position = new float3(px, py, pz),
                Rotation = new quaternion(qx, qy, qz, qw)
            };
        }

        public static TrackingPose FromPositionAndEuler(float px, float py, float pz, float ex, float ey, float ez)
        {
            return new TrackingPose
            {
                Position = new float3(px, py, pz),
                Rotation = quaternion.EulerXYZ(ex, ey, ez)
            };
        }

        public static TrackingPose Identity => new TrackingPose
        {
            Position = float3.zero,
            Rotation = quaternion.identity
        };
    }
}
