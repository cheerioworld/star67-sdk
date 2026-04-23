using System;
using System.Text;
using Star67.Avatar.Calibration;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [Serializable]
    public sealed class SemanticHumanoidBoneCalibration
    {
        public HumanBodyBones Bone;
        public Transform Transform;
        public bool IsBound;
        public Quaternion ReferenceLocalRotation = Quaternion.identity;
        public Vector3 ReferenceLocalPosition = Vector3.zero;
        public Vector3 ReferenceDirectionLocal = Vector3.forward;
        public float ChildLength;
        public Vector3 CurlAxisLocal = Vector3.right;
        public Vector3 SplayAxisLocal = Vector3.up;
        public Vector3 PitchAxisLocal = Vector3.forward;

        public void Clear(HumanBodyBones bone)
        {
            Bone = bone;
            Transform = null;
            IsBound = false;
            ReferenceLocalRotation = Quaternion.identity;
            ReferenceLocalPosition = Vector3.zero;
            ReferenceDirectionLocal = Vector3.forward;
            ChildLength = 0f;
            CurlAxisLocal = Vector3.right;
            SplayAxisLocal = Vector3.zero;
            PitchAxisLocal = Vector3.zero;
        }
    }

    [Serializable]
    public sealed class SemanticHumanoidFingerCalibration
    {
        public SemanticHandFinger Finger;
        public bool IsValid;
        public SemanticHumanoidBoneCalibration Proximal = new SemanticHumanoidBoneCalibration();
        public SemanticHumanoidBoneCalibration Intermediate = new SemanticHumanoidBoneCalibration();
        public SemanticHumanoidBoneCalibration Distal = new SemanticHumanoidBoneCalibration();

        public void Clear(
            SemanticHandFinger finger,
            HumanBodyBones proximalBone,
            HumanBodyBones intermediateBone,
            HumanBodyBones distalBone)
        {
            Finger = finger;
            IsValid = false;
            if (Proximal == null)
            {
                Proximal = new SemanticHumanoidBoneCalibration();
            }

            if (Intermediate == null)
            {
                Intermediate = new SemanticHumanoidBoneCalibration();
            }

            if (Distal == null)
            {
                Distal = new SemanticHumanoidBoneCalibration();
            }

            Proximal.Clear(proximalBone);
            Intermediate.Clear(intermediateBone);
            Distal.Clear(distalBone);
        }

        public SemanticHumanoidBoneCalibration GetBone(int index)
        {
            switch (index)
            {
                case 0:
                    return Proximal;
                case 1:
                    return Intermediate;
                case 2:
                    return Distal;
                default:
                    return null;
            }
        }
    }

    [Serializable]
    public sealed class SemanticHumanoidHandCalibrationData
    {
        public bool IsLeftHand;
        public bool IsValid;
        public string MissingBones = string.Empty;
        public Transform HandTransform;
        public Quaternion HandReferenceLocalRotation = Quaternion.identity;
        public Vector3 PalmForwardLocal = Vector3.forward;
        public Vector3 ThumbSideLocal = Vector3.right;
        public Vector3 PalmNormalLocal = Vector3.up;
        public SemanticHumanoidFingerCalibration Thumb = new SemanticHumanoidFingerCalibration();
        public SemanticHumanoidFingerCalibration Index = new SemanticHumanoidFingerCalibration();
        public SemanticHumanoidFingerCalibration Middle = new SemanticHumanoidFingerCalibration();
        public SemanticHumanoidFingerCalibration Ring = new SemanticHumanoidFingerCalibration();
        public SemanticHumanoidFingerCalibration Little = new SemanticHumanoidFingerCalibration();

        public void Clear(bool isLeftHand, string missingBones = "")
        {
            IsLeftHand = isLeftHand;
            IsValid = false;
            MissingBones = missingBones ?? string.Empty;
            HandTransform = null;
            HandReferenceLocalRotation = Quaternion.identity;
            PalmForwardLocal = Vector3.forward;
            ThumbSideLocal = Vector3.right;
            PalmNormalLocal = Vector3.up;

            if (Thumb == null)
            {
                Thumb = new SemanticHumanoidFingerCalibration();
            }

            if (Index == null)
            {
                Index = new SemanticHumanoidFingerCalibration();
            }

            if (Middle == null)
            {
                Middle = new SemanticHumanoidFingerCalibration();
            }

            if (Ring == null)
            {
                Ring = new SemanticHumanoidFingerCalibration();
            }

            if (Little == null)
            {
                Little = new SemanticHumanoidFingerCalibration();
            }

            Thumb.Clear(
                SemanticHandFinger.Thumb,
                isLeftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal,
                isLeftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate,
                isLeftHand ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal);
            Index.Clear(
                SemanticHandFinger.Index,
                isLeftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal,
                isLeftHand ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate,
                isLeftHand ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal);
            Middle.Clear(
                SemanticHandFinger.Middle,
                isLeftHand ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal,
                isLeftHand ? HumanBodyBones.LeftMiddleIntermediate : HumanBodyBones.RightMiddleIntermediate,
                isLeftHand ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
            Ring.Clear(
                SemanticHandFinger.Ring,
                isLeftHand ? HumanBodyBones.LeftRingProximal : HumanBodyBones.RightRingProximal,
                isLeftHand ? HumanBodyBones.LeftRingIntermediate : HumanBodyBones.RightRingIntermediate,
                isLeftHand ? HumanBodyBones.LeftRingDistal : HumanBodyBones.RightRingDistal);
            Little.Clear(
                SemanticHandFinger.Little,
                isLeftHand ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal,
                isLeftHand ? HumanBodyBones.LeftLittleIntermediate : HumanBodyBones.RightLittleIntermediate,
                isLeftHand ? HumanBodyBones.LeftLittleDistal : HumanBodyBones.RightLittleDistal);
        }

        public SemanticHumanoidFingerCalibration GetFinger(SemanticHandFinger finger)
        {
            switch (finger)
            {
                case SemanticHandFinger.Thumb:
                    return Thumb;
                case SemanticHandFinger.Index:
                    return Index;
                case SemanticHandFinger.Middle:
                    return Middle;
                case SemanticHandFinger.Ring:
                    return Ring;
                case SemanticHandFinger.Little:
                    return Little;
                default:
                    return null;
            }
        }
    }

    [Serializable]
    public sealed class SemanticHumanoidHandCalibration
    {
        public SemanticHumanoidHandCalibrationData LeftHand = new SemanticHumanoidHandCalibrationData();
        public SemanticHumanoidHandCalibrationData RightHand = new SemanticHumanoidHandCalibrationData();

        public void Clear(string reason = "")
        {
            if (LeftHand == null)
            {
                LeftHand = new SemanticHumanoidHandCalibrationData();
            }

            if (RightHand == null)
            {
                RightHand = new SemanticHumanoidHandCalibrationData();
            }

            LeftHand.Clear(true, reason);
            RightHand.Clear(false, reason);
        }

        public SemanticHumanoidHandCalibrationData GetHand(bool isLeftHand)
        {
            return isLeftHand ? LeftHand : RightHand;
        }
    }

    public static class SemanticHumanoidHandCalibrationUtility
    {
        private const float AxisTestAngleDegrees = 4f;
        private const float MinimumFallbackLength = 0.015f;

        public static bool TryCapture(Animator animator, SemanticHumanoidHandCalibration destination)
        {
            return TryCapture(animator, destination, out _);
        }

        public static bool TryCapture(Animator animator, SemanticHumanoidHandCalibration destination, out string failureReason)
        {
            failureReason = string.Empty;
            if (destination == null)
            {
                failureReason = "Missing calibration destination.";
                return false;
            }

            destination.Clear();
            if (animator == null || animator.avatar == null || !animator.isHuman)
            {
                failureReason = "Missing humanoid Animator.";
                destination.Clear(failureReason);
                return false;
            }

            NeutralPoseSnapshot neutralPose = NeutralPoseSnapshot.Capture(animator);
            var rig = new AvatarRootRig(animator.transform, animator);
            if (!HumanoidTPoseScope.TryCreate(rig, out HumanoidTPoseScope scope))
            {
                failureReason = "Unable to capture humanoid T-pose.";
                destination.Clear(failureReason);
                return false;
            }

            using (scope)
            {
                CaptureHand(animator, true, destination.LeftHand);
                CaptureHand(animator, false, destination.RightHand);
            }

            neutralPose.ApplyTo(destination.LeftHand);
            neutralPose.ApplyTo(destination.RightHand);

            if (destination.LeftHand.IsValid || destination.RightHand.IsValid)
            {
                return true;
            }

            failureReason = BuildFailureReason(destination);
            return false;
        }

        private sealed class NeutralPoseSnapshot
        {
            private readonly Quaternion[] localRotations = new Quaternion[(int)HumanBodyBones.LastBone];
            private readonly Vector3[] localPositions = new Vector3[(int)HumanBodyBones.LastBone];
            private readonly bool[] hasBone = new bool[(int)HumanBodyBones.LastBone];

            public static NeutralPoseSnapshot Capture(Animator animator)
            {
                var snapshot = new NeutralPoseSnapshot();
                if (animator == null)
                {
                    return snapshot;
                }

                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    HumanBodyBones bone = (HumanBodyBones)i;
                    Transform transform = animator.GetBoneTransform(bone);
                    if (transform == null)
                    {
                        continue;
                    }

                    snapshot.hasBone[i] = true;
                    snapshot.localRotations[i] = transform.localRotation;
                    snapshot.localPositions[i] = transform.localPosition;
                }

                return snapshot;
            }

            public void ApplyTo(SemanticHumanoidHandCalibrationData hand)
            {
                if (hand == null)
                {
                    return;
                }

                HumanBodyBones handBone = hand.IsLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                if (TryGet(handBone, out Quaternion handRotation, out _))
                {
                    hand.HandReferenceLocalRotation = handRotation;
                }

                ApplyTo(hand.Thumb);
                ApplyTo(hand.Index);
                ApplyTo(hand.Middle);
                ApplyTo(hand.Ring);
                ApplyTo(hand.Little);
            }

            private void ApplyTo(SemanticHumanoidFingerCalibration finger)
            {
                if (finger == null)
                {
                    return;
                }

                ApplyTo(finger.Proximal);
                ApplyTo(finger.Intermediate);
                ApplyTo(finger.Distal);
            }

            private void ApplyTo(SemanticHumanoidBoneCalibration bone)
            {
                if (bone == null || !TryGet(bone.Bone, out Quaternion rotation, out Vector3 position))
                {
                    return;
                }

                bone.ReferenceLocalRotation = rotation;
                bone.ReferenceLocalPosition = position;
                RefreshReferenceDirection(bone);
            }

            private static void RefreshReferenceDirection(SemanticHumanoidBoneCalibration bone)
            {
                if (bone == null || bone.Transform == null)
                {
                    return;
                }

                Transform child = ResolvePrimaryChild(bone.Transform, null);
                if (child == null)
                {
                    return;
                }

                Vector3 direction = child.position - bone.Transform.position;
                if (!TryNormalize(ref direction))
                {
                    return;
                }

                bone.ReferenceDirectionLocal = ToLocalAxis(bone.Transform, direction, bone.ReferenceDirectionLocal);
            }

            private bool TryGet(HumanBodyBones bone, out Quaternion rotation, out Vector3 position)
            {
                int index = (int)bone;
                if (index < 0 || index >= hasBone.Length || !hasBone[index])
                {
                    rotation = Quaternion.identity;
                    position = Vector3.zero;
                    return false;
                }

                rotation = localRotations[index];
                position = localPositions[index];
                return true;
            }
        }

        private static void CaptureHand(Animator animator, bool isLeftHand, SemanticHumanoidHandCalibrationData destination)
        {
            destination.Clear(isLeftHand);
            if (animator == null)
            {
                destination.MissingBones = "Missing Animator.";
                return;
            }

            HumanBodyBones handBone = isLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            HumanBodyBones indexBone = isLeftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal;
            HumanBodyBones middleBone = isLeftHand ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal;
            HumanBodyBones ringBone = isLeftHand ? HumanBodyBones.LeftRingProximal : HumanBodyBones.RightRingProximal;
            HumanBodyBones littleBone = isLeftHand ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal;

            Transform hand = animator.GetBoneTransform(handBone);
            Transform index = animator.GetBoneTransform(indexBone);
            Transform middle = animator.GetBoneTransform(middleBone);
            Transform ring = animator.GetBoneTransform(ringBone);
            Transform little = animator.GetBoneTransform(littleBone);

            var missing = new StringBuilder();
            if (hand == null)
            {
                AppendMissing(missing, handBone);
            }

            if (index == null)
            {
                AppendMissing(missing, indexBone);
            }

            if (middle == null)
            {
                AppendMissing(missing, middleBone);
            }

            if (ring == null)
            {
                AppendMissing(missing, ringBone);
            }

            if (little == null)
            {
                AppendMissing(missing, littleBone);
            }

            destination.HandTransform = hand;
            destination.HandReferenceLocalRotation = hand != null ? hand.localRotation : Quaternion.identity;

            bool hasPalmBasis = TryBuildPalmBasis(hand, index, middle, little, isLeftHand, out Vector3 palmForward, out Vector3 thumbSide, out Vector3 palmNormal);

            if (hasPalmBasis && hand != null)
            {
                destination.PalmForwardLocal = ToLocalAxis(hand, palmForward, Vector3.forward);
                destination.ThumbSideLocal = ToLocalAxis(hand, thumbSide, Vector3.right);
                destination.PalmNormalLocal = ToLocalAxis(hand, palmNormal, Vector3.up);
            }
            else
            {
                AppendMissing(missing, "invalid palm basis");
            }

            CaptureThumb(animator, isLeftHand, destination.Thumb, palmForward, thumbSide, palmNormal, missing);
            CaptureFinger(animator, destination.Index, palmForward, thumbSide, palmNormal, missing);
            CaptureFinger(animator, destination.Middle, palmForward, thumbSide, palmNormal, missing);
            CaptureFinger(animator, destination.Ring, palmForward, thumbSide, palmNormal, missing);
            CaptureFinger(animator, destination.Little, palmForward, thumbSide, palmNormal, missing);

            destination.MissingBones = missing.ToString();
            destination.IsValid = hasPalmBasis
                && hand != null
                && destination.Thumb.IsValid
                && destination.Index.IsValid
                && destination.Middle.IsValid
                && destination.Ring.IsValid
                && destination.Little.IsValid;
        }

        private static void CaptureThumb(
            Animator animator,
            bool isLeftHand,
            SemanticHumanoidFingerCalibration destination,
            Vector3 palmForward,
            Vector3 thumbSide,
            Vector3 palmNormal,
            StringBuilder missing)
        {
            HumanBodyBones proximalBone = isLeftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
            HumanBodyBones intermediateBone = isLeftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate;
            HumanBodyBones distalBone = isLeftHand ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal;

            destination.Clear(SemanticHandFinger.Thumb, proximalBone, intermediateBone, distalBone);
            Transform proximal = animator.GetBoneTransform(proximalBone);
            Transform intermediate = animator.GetBoneTransform(intermediateBone);
            Transform distal = animator.GetBoneTransform(distalBone);
            AppendMissingIfNull(missing, proximalBone, proximal);
            AppendMissingIfNull(missing, intermediateBone, intermediate);
            AppendMissingIfNull(missing, distalBone, distal);

            if (proximal == null || intermediate == null || distal == null)
            {
                return;
            }

            Vector3 terminalPoint = ResolveTerminalPoint(distal, thumbSide, EstimateSegmentLength(proximal, intermediate));
            destination.Proximal = CaptureBone(proximalBone, proximal, intermediate, terminalPoint, palmForward, thumbSide, palmNormal, true);
            destination.Proximal.SplayAxisLocal = ToLocalAxis(
                proximal,
                ResolveAxisTowardThumb(palmNormal, proximal.position, terminalPoint, palmForward, thumbSide, palmNormal),
                Vector3.up);
            destination.Proximal.PitchAxisLocal = ToLocalAxis(
                proximal,
                ResolveThumbPitchAxis(proximal.position, terminalPoint, palmNormal, thumbSide),
                Vector3.forward);
            Vector3 palmInterior = -palmNormal;
            destination.Proximal.CurlAxisLocal = ToLocalAxis(
                proximal,
                ResolveCurlAxis(Vector3.Cross(palmNormal, ResolveDirection(proximal, intermediate, thumbSide)), proximal.position, terminalPoint, palmInterior),
                Vector3.right);

            Vector3 thumbFoldDirection = ResolveThumbFoldDirection(thumbSide, palmInterior);
            destination.Intermediate = CaptureThumbCurlBone(intermediateBone, intermediate, distal, terminalPoint, palmForward, thumbFoldDirection);
            destination.Distal = CaptureThumbCurlBone(distalBone, distal, null, terminalPoint, palmForward, thumbFoldDirection);
            destination.IsValid = destination.Proximal.IsBound && destination.Intermediate.IsBound && destination.Distal.IsBound;
        }

        private static Vector3 ResolveThumbFoldDirection(Vector3 thumbSide, Vector3 palmInterior)
        {
            Vector3 direction = -thumbSide + palmInterior * 0.65f;
            if (!TryNormalize(ref direction))
            {
                direction = -thumbSide;
                TryNormalize(ref direction);
            }

            return direction;
        }

        private static SemanticHumanoidBoneCalibration CaptureThumbCurlBone(
            HumanBodyBones boneId,
            Transform bone,
            Transform explicitChild,
            Vector3 terminalPoint,
            Vector3 fallbackDirection,
            Vector3 desiredCurlMovement)
        {
            var calibration = new SemanticHumanoidBoneCalibration();
            calibration.Clear(boneId);
            if (bone == null)
            {
                return calibration;
            }

            Transform child = ResolvePrimaryChild(bone, explicitChild);
            Vector3 direction = ResolveDirection(bone, child, fallbackDirection);
            Vector3 curlAxis = ResolveCurlAxis(Vector3.Cross(direction, desiredCurlMovement), bone.position, terminalPoint, desiredCurlMovement);
            float childLength = child != null
                ? Vector3.Distance(bone.position, child.position)
                : Mathf.Max(MinimumFallbackLength, Vector3.Distance(bone.position, terminalPoint));

            calibration.Bone = boneId;
            calibration.Transform = bone;
            calibration.IsBound = true;
            calibration.ReferenceLocalRotation = bone.localRotation;
            calibration.ReferenceLocalPosition = bone.localPosition;
            calibration.ReferenceDirectionLocal = ToLocalAxis(bone, direction, Vector3.forward);
            calibration.ChildLength = childLength;
            calibration.CurlAxisLocal = ToLocalAxis(bone, curlAxis, Vector3.right);
            calibration.SplayAxisLocal = Vector3.zero;
            calibration.PitchAxisLocal = Vector3.zero;
            return calibration;
        }

        private static void CaptureFinger(
            Animator animator,
            SemanticHumanoidFingerCalibration destination,
            Vector3 palmForward,
            Vector3 thumbSide,
            Vector3 palmNormal,
            StringBuilder missing)
        {
            if (destination == null)
            {
                return;
            }

            Transform proximal = animator.GetBoneTransform(destination.Proximal.Bone);
            Transform intermediate = animator.GetBoneTransform(destination.Intermediate.Bone);
            Transform distal = animator.GetBoneTransform(destination.Distal.Bone);
            AppendMissingIfNull(missing, destination.Proximal.Bone, proximal);
            AppendMissingIfNull(missing, destination.Intermediate.Bone, intermediate);
            AppendMissingIfNull(missing, destination.Distal.Bone, distal);

            if (proximal == null || intermediate == null || distal == null)
            {
                return;
            }

            float fallbackLength = EstimateSegmentLength(proximal, intermediate);
            Vector3 terminalPoint = ResolveTerminalPoint(distal, palmForward, fallbackLength);
            destination.Proximal = CaptureBone(destination.Proximal.Bone, proximal, intermediate, terminalPoint, palmForward, thumbSide, palmNormal, true);
            destination.Intermediate = CaptureBone(destination.Intermediate.Bone, intermediate, distal, terminalPoint, palmForward, thumbSide, palmNormal, false);
            destination.Distal = CaptureBone(destination.Distal.Bone, distal, null, terminalPoint, palmForward, thumbSide, palmNormal, false);
            destination.IsValid = destination.Proximal.IsBound && destination.Intermediate.IsBound && destination.Distal.IsBound;
        }

        private static SemanticHumanoidBoneCalibration CaptureBone(
            HumanBodyBones boneId,
            Transform bone,
            Transform explicitChild,
            Vector3 terminalPoint,
            Vector3 palmForward,
            Vector3 thumbSide,
            Vector3 palmNormal,
            bool includeSplay)
        {
            var calibration = new SemanticHumanoidBoneCalibration();
            calibration.Clear(boneId);
            if (bone == null)
            {
                return calibration;
            }

            Transform child = ResolvePrimaryChild(bone, explicitChild);
            Vector3 direction = ResolveDirection(bone, child, palmForward);
            Vector3 curlAxis = ResolveCurlAxis(Vector3.Cross(palmNormal, direction), bone.position, terminalPoint, palmNormal);
            Vector3 splayAxis = includeSplay
                ? ResolveAxisTowardThumb(palmNormal, bone.position, terminalPoint, palmForward, thumbSide, palmNormal)
                : Vector3.zero;
            float childLength = child != null
                ? Vector3.Distance(bone.position, child.position)
                : Mathf.Max(MinimumFallbackLength, Vector3.Distance(bone.position, terminalPoint));

            calibration.Bone = boneId;
            calibration.Transform = bone;
            calibration.IsBound = true;
            calibration.ReferenceLocalRotation = bone.localRotation;
            calibration.ReferenceLocalPosition = bone.localPosition;
            calibration.ReferenceDirectionLocal = ToLocalAxis(bone, direction, Vector3.forward);
            calibration.ChildLength = childLength;
            calibration.CurlAxisLocal = ToLocalAxis(bone, curlAxis, Vector3.right);
            calibration.SplayAxisLocal = includeSplay ? ToLocalAxis(bone, splayAxis, Vector3.up) : Vector3.zero;
            calibration.PitchAxisLocal = Vector3.zero;
            return calibration;
        }

        private static bool TryBuildPalmBasis(
            Transform hand,
            Transform index,
            Transform middle,
            Transform little,
            bool isLeftHand,
            out Vector3 palmForward,
            out Vector3 thumbSide,
            out Vector3 palmNormal)
        {
            palmForward = Vector3.forward;
            thumbSide = Vector3.right;
            palmNormal = Vector3.up;
            if (hand == null || index == null || middle == null || little == null)
            {
                return false;
            }

            palmForward = middle.position - hand.position;
            thumbSide = index.position - little.position;
            if (!TryNormalize(ref palmForward) || !TryNormalize(ref thumbSide))
            {
                return false;
            }

            palmNormal = isLeftHand
                ? Vector3.Cross(thumbSide, palmForward)
                : Vector3.Cross(palmForward, thumbSide);
            if (!TryNormalize(ref palmNormal))
            {
                return false;
            }

            thumbSide = isLeftHand
                ? Vector3.Cross(palmForward, palmNormal)
                : Vector3.Cross(palmNormal, palmForward);
            return TryNormalize(ref thumbSide);
        }

        private static Vector3 ResolveDirection(Transform bone, Transform child, Vector3 fallbackDirection)
        {
            Vector3 direction = child != null ? child.position - bone.position : Vector3.zero;
            if (!TryNormalize(ref direction))
            {
                direction = fallbackDirection;
            }

            if (!TryNormalize(ref direction))
            {
                direction = bone != null ? bone.forward : Vector3.forward;
                TryNormalize(ref direction);
            }

            return direction;
        }

        private static Transform ResolvePrimaryChild(Transform bone, Transform explicitChild)
        {
            if (explicitChild != null)
            {
                return explicitChild;
            }

            if (bone == null || bone.childCount == 0)
            {
                return null;
            }

            return bone.GetChild(0);
        }

        private static Vector3 ResolveTerminalPoint(Transform distal, Vector3 fallbackDirection, float fallbackLength)
        {
            if (distal == null)
            {
                return Vector3.zero;
            }

            Transform child = ResolvePrimaryChild(distal, null);
            if (child != null)
            {
                return child.position;
            }

            Vector3 direction = fallbackDirection;
            if (!TryNormalize(ref direction))
            {
                direction = distal.forward;
                TryNormalize(ref direction);
            }

            return distal.position + direction * Mathf.Max(MinimumFallbackLength, fallbackLength);
        }

        private static Vector3 ResolveCurlAxis(Vector3 candidateAxis, Vector3 origin, Vector3 terminalPoint, Vector3 desiredMovement)
        {
            if (!TryNormalize(ref candidateAxis))
            {
                return Vector3.right;
            }

            if (!TryNormalize(ref desiredMovement))
            {
                return candidateAxis;
            }

            Vector3 positiveMovement = RotatePoint(terminalPoint, origin, candidateAxis, AxisTestAngleDegrees) - terminalPoint;
            Vector3 negativeMovement = RotatePoint(terminalPoint, origin, candidateAxis, -AxisTestAngleDegrees) - terminalPoint;
            if (!TryNormalize(ref positiveMovement) || !TryNormalize(ref negativeMovement))
            {
                return candidateAxis;
            }

            float positiveScore = Vector3.Dot(positiveMovement, desiredMovement);
            float negativeScore = Vector3.Dot(negativeMovement, desiredMovement);
            return positiveScore >= negativeScore ? candidateAxis : -candidateAxis;
        }

        private static Vector3 ResolveAxisTowardThumb(
            Vector3 candidateAxis,
            Vector3 origin,
            Vector3 terminalPoint,
            Vector3 palmForward,
            Vector3 thumbSide,
            Vector3 palmNormal)
        {
            if (!TryNormalize(ref candidateAxis))
            {
                return Vector3.up;
            }

            Vector3 positiveDirection = RotatePoint(terminalPoint, origin, candidateAxis, AxisTestAngleDegrees) - origin;
            Vector3 negativeDirection = RotatePoint(terminalPoint, origin, candidateAxis, -AxisTestAngleDegrees) - origin;
            float positiveScore = ThumbSideScore(positiveDirection, palmForward, thumbSide, palmNormal);
            float negativeScore = ThumbSideScore(negativeDirection, palmForward, thumbSide, palmNormal);
            return positiveScore >= negativeScore ? candidateAxis : -candidateAxis;
        }

        private static Vector3 ResolveThumbPitchAxis(
            Vector3 origin,
            Vector3 terminalPoint,
            Vector3 palmNormal,
            Vector3 fallbackAxis)
        {
            Vector3 direction = terminalPoint - origin;
            TryNormalize(ref direction);
            Vector3 candidateAxis = Vector3.Cross(direction, palmNormal);
            if (!TryNormalize(ref candidateAxis))
            {
                candidateAxis = fallbackAxis;
            }

            if (!TryNormalize(ref candidateAxis))
            {
                return Vector3.forward;
            }

            Vector3 pointPositive = RotatePoint(terminalPoint, origin, candidateAxis, AxisTestAngleDegrees) - origin;
            Vector3 pointNegative = RotatePoint(terminalPoint, origin, candidateAxis, -AxisTestAngleDegrees) - origin;
            float positiveScore = Vector3.Dot(pointPositive.normalized, palmNormal);
            float negativeScore = Vector3.Dot(pointNegative.normalized, palmNormal);
            return positiveScore >= negativeScore ? candidateAxis : -candidateAxis;
        }

        private static float ThumbSideScore(Vector3 direction, Vector3 palmForward, Vector3 thumbSide, Vector3 palmNormal)
        {
            direction = Vector3.ProjectOnPlane(direction, palmNormal);
            if (!TryNormalize(ref direction))
            {
                direction = palmForward;
                TryNormalize(ref direction);
            }

            return Vector3.Dot(direction, thumbSide);
        }

        private static Vector3 RotatePoint(Vector3 point, Vector3 pivot, Vector3 axis, float angleDegrees)
        {
            return pivot + Quaternion.AngleAxis(angleDegrees, axis) * (point - pivot);
        }

        private static float EstimateSegmentLength(Transform a, Transform b)
        {
            if (a == null || b == null)
            {
                return MinimumFallbackLength;
            }

            return Mathf.Max(MinimumFallbackLength, Vector3.Distance(a.position, b.position));
        }

        private static Vector3 ToLocalAxis(Transform transform, Vector3 worldAxis, Vector3 fallback)
        {
            if (transform == null)
            {
                return fallback;
            }

            if (!TryNormalize(ref worldAxis))
            {
                return fallback;
            }

            Vector3 localAxis = transform.InverseTransformDirection(worldAxis);
            return TryNormalize(ref localAxis) ? localAxis : fallback;
        }

        private static void AppendMissingIfNull(StringBuilder builder, HumanBodyBones bone, Transform transform)
        {
            if (transform == null)
            {
                AppendMissing(builder, bone);
            }
        }

        private static void AppendMissing(StringBuilder builder, HumanBodyBones bone)
        {
            AppendMissing(builder, bone.ToString());
        }

        private static void AppendMissing(StringBuilder builder, string label)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
        }

        private static string BuildFailureReason(SemanticHumanoidHandCalibration calibration)
        {
            if (calibration == null)
            {
                return "Calibration failed.";
            }

            string left = calibration.LeftHand != null ? calibration.LeftHand.MissingBones : string.Empty;
            string right = calibration.RightHand != null ? calibration.RightHand.MissingBones : string.Empty;
            if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right))
            {
                return "Left: " + left + " | Right: " + right;
            }

            if (!string.IsNullOrEmpty(left))
            {
                return "Left: " + left;
            }

            if (!string.IsNullOrEmpty(right))
            {
                return "Right: " + right;
            }

            return "Calibration failed.";
        }

        private static bool TryNormalize(ref Vector3 vector)
        {
            float magnitude = vector.magnitude;
            if (magnitude < 1e-6f || float.IsNaN(magnitude) || float.IsInfinity(magnitude))
            {
                vector = Vector3.zero;
                return false;
            }

            vector /= magnitude;
            return true;
        }
    }
}
