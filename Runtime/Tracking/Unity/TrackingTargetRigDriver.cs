using UnityEngine;

namespace Star67.Tracking.Unity
{
    /// <summary>
    /// Applies canonical tracking frames to a <see cref="TrackingTargetRig"/> for IK and downstream retargeting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackingTargetRigDriver : MonoBehaviour, ITrackingFrameApplier
    {
        [SerializeField] private TrackingTargetRig rig;

        private float _avatarHeight = 0f;

        /// <summary>
        /// Gets or sets the output rig that receives camera, head, and hand targets.
        /// </summary>
        public TrackingTargetRig Rig
        {
            get => rig;
            set => rig = value;
        }

        /// <inheritdoc />
        public TrackingFeatureFlags RequiredFeatures => TrackingFeatureFlags.CameraWorldPose | TrackingFeatureFlags.HeadPose;

        private void Awake()
        {
            if (rig == null)
            {
                rig = GetComponent<TrackingTargetRig>();
            }
        }

        public void SetAvatarHeight(float heightMeters)
        {
            _avatarHeight = heightMeters;
        }

        /// <inheritdoc />
        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            if (rig == null || frame == null)
            {
                return;
            }

            TrackingTargetRigState state = rig.State;

            ApplyPose(rig.CameraWorldTarget, frame.CameraWorldPose, _avatarHeight);
            state.CameraTracked = (frame.Features & TrackingFeatureFlags.CameraWorldPose) != 0;

            TrackingPose headWorldPose = TrackingMath.Combine(frame.CameraWorldPose, frame.HeadPoseCameraSpace);
            ApplyPose(rig.HeadWorldTarget, headWorldPose, _avatarHeight);
            state.HeadTracked = (frame.Features & TrackingFeatureFlags.HeadPose) != 0;

            ApplyHand(frame.CameraWorldPose, frame.LeftHand, rig.LeftWristTarget, ref state.LeftHandTracked);
            ApplyHand(frame.CameraWorldPose, frame.RightHand, rig.RightWristTarget, ref state.RightHandTracked);
        }

        /// <inheritdoc />
        public void ResetState()
        {
            if (rig == null)
            {
                return;
            }

            rig.State.CameraTracked = false;
            rig.State.HeadTracked = false;
            rig.State.LeftHandTracked = false;
            rig.State.RightHandTracked = false;
        }

        private static void ApplyPose(Transform target, TrackingPose pose, float heightOffset)
        {
            if (target == null)
            {
                return;
            }

            target.position = ToVector3(pose.GetPositionValue()) + Vector3.up * heightOffset;
            target.rotation = ToQuaternion(pose.GetRotationValue());
        }

        private static void ApplyHand(TrackingPose cameraWorldPose, TrackedHandData hand, Transform wristTarget, ref bool handTracked)
        {
            handTracked = hand != null && hand.IsTracked;
            if (!handTracked || wristTarget == null)
            {
                return;
            }

            TrackingPose wristWorldPose = TrackingMath.Combine(cameraWorldPose, hand.WristPoseSourceSpace);
            wristTarget.position = ToVector3(wristWorldPose.GetPositionValue());
            wristTarget.rotation = ToQuaternion(wristWorldPose.GetRotationValue());
        }

        private static Vector3 ToVector3(TrackingVector3Value value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Quaternion ToQuaternion(TrackingQuaternionValue value)
        {
            return new Quaternion(value.X, value.Y, value.Z, value.W);
        }
    }
}
