using System;
using RootMotion.FinalIK;
using Star67.Avatar;
using Star67.Tracking;
using UnityEngine;

namespace Star67.Sdk.Avatar
{
    public class AvatarVRIKTargetsDriver: AvatarComponent
    {
        public override string Name => "AvatarVRIKTargetsDriver";

        public AvatarIKTargets Targets { get; private set; }
        public VRIK VRIK { get; private set; }

        public AvatarVRIKTargetsDriver() {}

        protected override bool TryInitialize()
        {
            var trackingTargets = Targets = Avatar.IKTargets ?? AvatarIKTargets.Create(Avatar.Rig.Root.parent);
            
            Transform root = Avatar.Rig.Root;
            // if (trackingTargets == null)
            // {
            //     Debug.LogWarning($"AvatarVRIKTargetsDriver: A AvatarIKTargets was not found while loading avatar '{root.name}'. Skipping VRIK setup.");
            //     return false;
            // }

            VRIK vrik = VRIK ??= root.GetComponent<VRIK>() ?? root.gameObject.AddComponent<VRIK>();
            // if (vrik == null)
            // {
            //     vrik = root.gameObject.AddComponent<VRIK>();
            // }

            vrik.AutoDetectReferences();
            if (vrik.references == null || !vrik.references.isFilled)
            {
                Debug.LogWarning($"VrikAvatarLoaderPostprocessor: VRIK could not auto-detect a valid humanoid rig for avatar '{root.name}'.");
                return false;
            }

            vrik.GuessHandOrientations();
            vrik.solver.spine.headTarget = trackingTargets.HeadTarget;
            vrik.solver.leftArm.target = trackingTargets.LeftWristTarget;
            vrik.solver.rightArm.target = trackingTargets.RightWristTarget;
            // vrik.solver.locomotion.mode = IKSolverVR.Locomotion.Mode.Animated;
            vrik.solver.Reset();
            
            Debug.Log(VRIK);
            return true;
        }

        public void Apply(TrackingFrameBuffer frame)
        {
            Apply(frame, ToPose(frame.CameraWorldPose));
        }

        public void Apply(TrackingFrameBuffer frame, Pose calibratedCameraPose)
        {
            if (frame == null || Targets == null)
            {
                return;
            }

            if (Targets.CameraTarget != null)
            {
                Targets.CameraTarget.localPosition = calibratedCameraPose.position;
                Targets.CameraTarget.localRotation = calibratedCameraPose.rotation;
            }

            if (Targets.HeadTarget != null)
            {
                Pose headWorldPose = Combine(calibratedCameraPose, frame.HeadPoseCameraSpace);
                Targets.HeadTarget.localPosition = headWorldPose.position;
                Targets.HeadTarget.localRotation = headWorldPose.rotation;
            }
        }

        protected override void OnRemoved()
        {
            UnityEngine.Object.Destroy(Targets.TargetSpaceRoot.gameObject);
            Targets = null;
            UnityEngine.Object.Destroy(VRIK);
        }
        
        private static Vector3 ToVector3(TrackingVector3Value value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Quaternion ToQuaternion(TrackingQuaternionValue value)
        {
            return new Quaternion(value.X, value.Y, value.Z, value.W);
        }

        private static Pose ToPose(TrackingPose pose)
        {
            return new Pose(ToVector3(pose.GetPositionValue()), ToQuaternion(pose.GetRotationValue()));
        }

        private static Pose Combine(Pose parent, TrackingPose child)
        {
            Vector3 childPosition = ToVector3(child.GetPositionValue());
            Quaternion childRotation = ToQuaternion(child.GetRotationValue());
            return new Pose(
                parent.position + parent.rotation * childPosition,
                parent.rotation * childRotation);
        }
    }
}
