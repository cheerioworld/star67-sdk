using Unity.Mathematics;

namespace Star67.Tracking
{
    public static class TrackingMath
    {
        public static float3 TransformPoint(TrackingPose pose, float3 point)
        {
            return pose.Position + math.rotate(pose.Rotation, point);
        }

        public static TrackingVector3Value TransformPointValue(TrackingPose pose, TrackingVector3Value point)
        {
            float3 transformed = TransformPoint(pose, new float3(point.X, point.Y, point.Z));
            return new TrackingVector3Value(transformed.x, transformed.y, transformed.z);
        }

        public static TrackingPose Combine(TrackingPose parent, TrackingPose child)
        {
            return new TrackingPose
            {
                Position = TransformPoint(parent, child.Position),
                Rotation = math.mul(parent.Rotation, child.Rotation)
            };
        }
    }
}
