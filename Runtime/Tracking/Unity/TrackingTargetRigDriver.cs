using UnityEngine;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Applies canonical tracking frames to a <see cref="TrackingTargetRig"/> for IK and downstream retargeting.
    /// </summary>
    public sealed class TrackingTargetRigDriver : MonoBehaviour, ITrackingFrameApplier
    {
        [SerializeField] private TrackingTargetRig rig;

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

        /// <inheritdoc />
        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            if (rig == null || frame == null)
            {
                return;
            }

            TrackingTargetRigState state = rig.State;

            ApplyPose(rig.CameraWorldTarget, frame.CameraWorldPose);
            state.CameraTracked = (frame.Features & TrackingFeatureFlags.CameraWorldPose) != 0;

            TrackingPose headWorldPose = TrackingMath.Combine(frame.CameraWorldPose, frame.HeadPoseCameraSpace);
            ApplyPose(rig.HeadWorldTarget, headWorldPose);
            state.HeadTracked = (frame.Features & TrackingFeatureFlags.HeadPose) != 0;

            ApplyHand(frame.CameraWorldPose, frame.LeftHand, rig.LeftWristTarget, rig.LeftHandJointTargets, ref state.LeftHandTracked);
            ApplyHand(frame.CameraWorldPose, frame.RightHand, rig.RightWristTarget, rig.RightHandJointTargets, ref state.RightHandTracked);
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

        private static void ApplyPose(Transform target, TrackingPose pose)
        {
            if (target == null)
            {
                return;
            }

            target.position = ToVector3(pose.GetPositionValue());
            target.rotation = ToQuaternion(pose.GetRotationValue());
        }

        private static void ApplyHand(TrackingPose cameraWorldPose, TrackedHandData hand, Transform wristTarget, Transform[] jointTargets, ref bool handTracked)
        {
            handTracked = hand != null && hand.IsTracked;
            if (!handTracked)
            {
                return;
            }

            if (wristTarget != null)
            {
                wristTarget.position = ToVector3(TrackingMath.TransformPointValue(cameraWorldPose, hand.GetJointPositionValue(HandJointId.Wrist)));
                if (TryComputeWristRotation(hand, cameraWorldPose, out Quaternion wristRotation))
                {
                    wristTarget.rotation = wristRotation;
                }
            }

            if (jointTargets == null)
            {
                return;
            }

            int count = Mathf.Min(jointTargets.Length, TrackingProtocol.HandJointCount);
            for (int i = 0; i < count; i++)
            {
                Transform jointTarget = jointTargets[i];
                if (jointTarget == null)
                {
                    continue;
                }

                TrackingVector3Value worldPosition = TrackingMath.TransformPointValue(cameraWorldPose, hand.GetJointPositionValue(i));
                jointTarget.position = ToVector3(worldPosition);
            }
        }

        private static bool TryComputeWristRotation(TrackedHandData hand, TrackingPose cameraWorldPose, out Quaternion worldRotation)
        {
            worldRotation = Quaternion.identity;
            Vector3 wrist = ToVector3(hand.GetJointPositionValue(HandJointId.Wrist));
            Vector3 index = ToVector3(hand.GetJointPositionValue(HandJointId.IndexMcp));
            Vector3 pinky = ToVector3(hand.GetJointPositionValue(HandJointId.PinkyMcp));
            Vector3 middle = ToVector3(hand.GetJointPositionValue(HandJointId.MiddleMcp));

            Vector3 across = index - pinky;
            Vector3 forward = middle - wrist;
            if (across.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            across.Normalize();
            forward.Normalize();
            Vector3 up = Vector3.Cross(forward, across);
            if (up.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            up.Normalize();
            Quaternion localRotation = Quaternion.LookRotation(forward, up);
            worldRotation = ToQuaternion(cameraWorldPose.GetRotationValue()) * localRotation;
            return true;
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
