using System;
using RootMotion;
using RootMotion.FinalIK;
using Star67.Avatar;
using Star67.Tracking;
using UnityEngine;

namespace Star67.Sdk.Avatar
{
    [Serializable]
    public sealed class AvatarVRIKHandTargetDiagnostics
    {
        public bool TargetBound;
        public bool VrikReferenceValid;
        public bool IsTracked;
        public bool IsApplied;
        public float Confidence;
        public float PositionWeight;
        public float RotationWeight;
        public string SourceHandSlot = string.Empty;
        public bool MirrorHandsForSelfie;
        public bool MirrorWristPositionForSelfie;
        public Vector3 SourceWristPosition;
        public Vector3 TargetLocalPosition;
        public Quaternion TargetLocalRotation = Quaternion.identity;
        public string Summary = string.Empty;

        public void ClearRuntimeState()
        {
            IsTracked = false;
            IsApplied = false;
            Confidence = 0f;
            PositionWeight = 0f;
            RotationWeight = 0f;
            SourceHandSlot = string.Empty;
            MirrorHandsForSelfie = false;
            MirrorWristPositionForSelfie = false;
            SourceWristPosition = Vector3.zero;
            TargetLocalPosition = Vector3.zero;
            TargetLocalRotation = Quaternion.identity;
            Summary = string.Empty;
        }
    }

    public class AvatarVRIKTargetsDriver: AvatarComponent
    {
        public override string Name => "AvatarVRIKTargetsDriver";

        public AvatarIKTargets Targets { get; private set; }
        public VRIK VRIK { get; private set; }
        public bool MirrorHandsForSelfie { get; set; }
        // Use with MirrorHandsForSelfie when the source pose is in camera space and the avatar
        // should behave like a mirror. The slot swap maps physical right to avatar left; this
        // reflects the wrist offset so that cross-body motion also moves in the mirrored direction.
        public bool MirrorWristPositionsForSelfie { get; set; }
        public AvatarVRIKHandTargetDiagnostics LeftHandDiagnostics { get; } = new AvatarVRIKHandTargetDiagnostics();
        public AvatarVRIKHandTargetDiagnostics RightHandDiagnostics { get; } = new AvatarVRIKHandTargetDiagnostics();

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
            SyncStaticDiagnostics();
            SetArmWeights(vrik.solver.leftArm, 0f, 0f, LeftHandDiagnostics);
            SetArmWeights(vrik.solver.rightArm, 0f, 0f, RightHandDiagnostics);
            
            Debug.Log(VRIK);
            return true;
        }

        public void Apply(TrackingFrameBuffer frame)
        {
            Apply(frame, ToPose(frame.CameraWorldPose));
        }

        public void Apply(TrackingFrameBuffer frame, Pose calibratedCameraPose)
        {
            if (frame == null || Targets == null || VRIK == null)
            {
                LeftHandDiagnostics?.ClearRuntimeState();
                RightHandDiagnostics?.ClearRuntimeState();
                ReleaseHand(VRIK != null ? VRIK.solver.leftArm : null, LeftHandDiagnostics, frame == null ? "no frame" : "driver unavailable");
                ReleaseHand(VRIK != null ? VRIK.solver.rightArm : null, RightHandDiagnostics, frame == null ? "no frame" : "driver unavailable");
                return;
            }

            SyncStaticDiagnostics();

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

            // Selfie mode should behave like a mirror: the user's right hand drives the avatar's left wrist.
            TrackedHandData leftTargetSourceHand = MirrorHandsForSelfie ? frame.RightHand : frame.LeftHand;
            TrackedHandData rightTargetSourceHand = MirrorHandsForSelfie ? frame.LeftHand : frame.RightHand;
            string leftTargetSourceSlot = MirrorHandsForSelfie ? "RightHand" : "LeftHand";
            string rightTargetSourceSlot = MirrorHandsForSelfie ? "LeftHand" : "RightHand";

            ApplyHand(calibratedCameraPose, leftTargetSourceHand, leftTargetSourceSlot, Targets.LeftWristTarget, VRIK.solver.leftArm, LeftHandDiagnostics, true, MirrorHandsForSelfie, MirrorWristPositionsForSelfie);
            ApplyHand(calibratedCameraPose, rightTargetSourceHand, rightTargetSourceSlot, Targets.RightWristTarget, VRIK.solver.rightArm, RightHandDiagnostics, false, MirrorHandsForSelfie, MirrorWristPositionsForSelfie);
        }

        protected override void OnRemoved()
        {
            LeftHandDiagnostics?.ClearRuntimeState();
            RightHandDiagnostics?.ClearRuntimeState();
            ReleaseHand(VRIK != null ? VRIK.solver.leftArm : null, LeftHandDiagnostics, "removed");
            ReleaseHand(VRIK != null ? VRIK.solver.rightArm : null, RightHandDiagnostics, "removed");
            if (Targets != null && Targets.TargetSpaceRoot != null)
            {
                UnityEngine.Object.Destroy(Targets.TargetSpaceRoot.gameObject);
            }
            Targets = null;
            UnityEngine.Object.Destroy(VRIK);
        }

        private void SyncStaticDiagnostics()
        {
            LeftHandDiagnostics.TargetBound = Targets != null && Targets.LeftWristTarget != null;
            LeftHandDiagnostics.VrikReferenceValid = VRIK != null && VRIK.references != null && VRIK.references.leftHand != null;
            RightHandDiagnostics.TargetBound = Targets != null && Targets.RightWristTarget != null;
            RightHandDiagnostics.VrikReferenceValid = VRIK != null && VRIK.references != null && VRIK.references.rightHand != null;
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
            return Combine(parent, child, false);
        }

        private static Pose Combine(Pose parent, TrackingPose child, bool mirrorSourceOrientation)
        {
            return Combine(parent, child, false, mirrorSourceOrientation);
        }

        private static Pose Combine(Pose parent, TrackingPose child, bool mirrorSourcePosition, bool mirrorSourceOrientation)
        {
            Vector3 childPosition = ToVector3(child.GetPositionValue());
            Quaternion childRotation = ToQuaternion(child.GetRotationValue());
            if (mirrorSourcePosition)
            {
                childPosition = ReflectAcrossSelfieMirrorPlane(childPosition);
            }

            if (mirrorSourceOrientation)
            {
                childRotation = MirrorSourceRotationForSelfie(childRotation);
            }

            return new Pose(
                parent.position + parent.rotation * childPosition,
                parent.rotation * childRotation);
        }

        private static Quaternion MirrorSourceRotationForSelfie(Quaternion sourceRotation)
        {
            Vector3 sourceForward = ReflectAcrossSelfieMirrorPlane(sourceRotation * Vector3.forward);
            Vector3 sourceUp = ReflectAcrossSelfieMirrorPlane(sourceRotation * Vector3.up);
            if (sourceForward.sqrMagnitude <= 1e-8f)
            {
                sourceForward = Vector3.forward;
            }

            if (sourceUp.sqrMagnitude <= 1e-8f)
            {
                sourceUp = Vector3.up;
            }

            Vector3.OrthoNormalize(ref sourceForward, ref sourceUp);
            return Quaternion.LookRotation(sourceForward, sourceUp);
        }

        private static Vector3 ReflectAcrossSelfieMirrorPlane(Vector3 sourceSpaceDirection)
        {
            return new Vector3(-sourceSpaceDirection.x, sourceSpaceDirection.y, sourceSpaceDirection.z);
        }

        private static void ApplyHand(
            Pose calibratedCameraPose,
            TrackedHandData trackedHand,
            string sourceHandSlot,
            Transform wristTarget,
            IKSolverVR.Arm arm,
            AvatarVRIKHandTargetDiagnostics diagnostics,
            bool isLeft,
            bool mirrorSourceOrientation,
            bool mirrorSourcePosition)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.TargetBound = wristTarget != null;
            diagnostics.VrikReferenceValid &= arm != null;
            diagnostics.IsTracked = trackedHand != null && trackedHand.IsTracked;
            diagnostics.IsApplied = false;
            diagnostics.Confidence = trackedHand != null ? Mathf.Clamp01(trackedHand.Confidence) : 0f;
            diagnostics.SourceHandSlot = sourceHandSlot ?? string.Empty;
            diagnostics.MirrorHandsForSelfie = mirrorSourceOrientation;
            diagnostics.MirrorWristPositionForSelfie = mirrorSourcePosition;

            if (arm == null)
            {
                ReleaseHand(null, diagnostics, "missing VRIK arm");
                return;
            }

            if (wristTarget == null)
            {
                ReleaseHand(arm, diagnostics, "missing wrist target");
                return;
            }

            if (trackedHand == null || !trackedHand.IsTracked)
            {
                ReleaseHand(arm, diagnostics, $"{diagnostics.SourceHandSlot} untracked");
                return;
            }

            Pose wristTargetPose = Combine(calibratedCameraPose, trackedHand.WristPoseSourceSpace, mirrorSourcePosition, mirrorSourceOrientation);
            diagnostics.SourceWristPosition = ToVector3(trackedHand.WristPoseSourceSpace.GetPositionValue());
            wristTarget.localPosition = wristTargetPose.position;
            wristTarget.localRotation = MatchWristRotationToVrik(wristTargetPose.rotation, arm, isLeft);
            diagnostics.TargetLocalPosition = wristTarget.localPosition;
            diagnostics.TargetLocalRotation = wristTarget.localRotation;
            SetArmWeights(arm, 1f, 1f, diagnostics);
            diagnostics.IsApplied = true;
            diagnostics.Summary = $"{diagnostics.SourceHandSlot} tracked conf {diagnostics.Confidence:0.00} src {diagnostics.SourceWristPosition} target {diagnostics.TargetLocalPosition}";
        }

        private static Quaternion MatchWristRotationToVrik(Quaternion wristRotation, IKSolverVR.Arm arm, bool isLeft)
        {
            Vector3 sourceForward = wristRotation * Vector3.forward;
            Vector3 sourceHandNormal = wristRotation * Vector3.up;
            // Match the anatomical thumb axis directly. In selfie mirroring, matching only
            // finger direction plus hand normal can preserve the silhouette while swapping palm/back.
            Vector3 sourceThumbSide = isLeft
                ? Vector3.Cross(sourceForward, sourceHandNormal)
                : Vector3.Cross(sourceHandNormal, sourceForward);
            Vector3 destinationForward = arm.wristToPalmAxis;
            Vector3 destinationThumbSide = arm.palmToThumbAxis;

            if (destinationForward.sqrMagnitude <= 1e-8f)
            {
                destinationForward = Vector3.forward;
            }

            if (sourceThumbSide.sqrMagnitude <= 1e-8f)
            {
                sourceThumbSide = isLeft ? Vector3.left : Vector3.right;
            }

            if (destinationThumbSide.sqrMagnitude <= 1e-8f)
            {
                destinationThumbSide = isLeft ? Vector3.left : Vector3.right;
            }

            Vector3.OrthoNormalize(ref sourceForward, ref sourceThumbSide);
            Vector3.OrthoNormalize(ref destinationForward, ref destinationThumbSide);

            Quaternion sourceBasisRotation = Quaternion.LookRotation(sourceForward, sourceThumbSide);
            return QuaTools.MatchRotation(
                sourceBasisRotation,
                Vector3.forward,
                Vector3.up,
                destinationForward,
                destinationThumbSide);
        }

        private static void SetArmWeights(
            IKSolverVR.Arm arm,
            float positionWeight,
            float rotationWeight,
            AvatarVRIKHandTargetDiagnostics diagnostics)
        {
            if (arm != null)
            {
                arm.positionWeight = positionWeight;
                arm.rotationWeight = rotationWeight;
            }

            if (diagnostics != null)
            {
                diagnostics.PositionWeight = positionWeight;
                diagnostics.RotationWeight = rotationWeight;
            }
        }

        private static void ReleaseHand(IKSolverVR.Arm arm, AvatarVRIKHandTargetDiagnostics diagnostics, string summary)
        {
            SetArmWeights(arm, 0f, 0f, diagnostics);
            if (diagnostics != null)
            {
                diagnostics.IsApplied = false;
                diagnostics.Summary = summary ?? string.Empty;
            }
        }
    }
}
