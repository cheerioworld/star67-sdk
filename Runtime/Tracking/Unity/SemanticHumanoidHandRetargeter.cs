using System;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [Serializable]
    public sealed class SemanticHumanoidHandRetargetDiagnostics
    {
        public bool IsCalibrated;
        public bool IsApplied;
        public bool IsTracked;
        public float Confidence;
        public string MissingBones = string.Empty;
        public string Summary = string.Empty;
        public SemanticThumbPose Thumb;
        public SemanticFingerPose Index;
        public SemanticFingerPose Middle;
        public SemanticFingerPose Ring;
        public SemanticFingerPose Little;

        public void ClearRuntimeState()
        {
            IsApplied = false;
            IsTracked = false;
            Confidence = 0f;
            Summary = string.Empty;
            Thumb = default(SemanticThumbPose);
            Index = default(SemanticFingerPose);
            Middle = default(SemanticFingerPose);
            Ring = default(SemanticFingerPose);
            Little = default(SemanticFingerPose);
        }
    }

    [AddComponentMenu("Star67/Semantic Humanoid Hand Retargeter")]
    [DisallowMultipleComponent]
    public sealed class SemanticHumanoidHandRetargeter : MonoBehaviour, ITrackingFrameApplier
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _applyLeftHand = true;
        [SerializeField] private bool _applyRightHand = true;
        [SerializeField] private bool _resetUntrackedHands = true;
        [SerializeField] private bool _captureCalibrationOnEnable = true;
        [SerializeField] private SemanticHandPoseSettings _poseSettings = SemanticHandPoseSettings.Default;
        [SerializeField] private SemanticHumanoidHandCalibration _calibration = new SemanticHumanoidHandCalibration();
        [SerializeField] private SemanticHumanoidHandRetargetDiagnostics _leftDiagnostics = new SemanticHumanoidHandRetargetDiagnostics();
        [SerializeField] private SemanticHumanoidHandRetargetDiagnostics _rightDiagnostics = new SemanticHumanoidHandRetargetDiagnostics();

        public TrackingFeatureFlags RequiredFeatures => TrackingFeatureFlags.LeftHand | TrackingFeatureFlags.RightHand;

        public Animator Animator => _animator;
        public SemanticHumanoidHandCalibration Calibration => _calibration;
        public SemanticHumanoidHandRetargetDiagnostics LeftDiagnostics => _leftDiagnostics;
        public SemanticHumanoidHandRetargetDiagnostics RightDiagnostics => _rightDiagnostics;

        private void Awake()
        {
            _poseSettings = ResolveSettings(_poseSettings);
            ResolveAnimator();
            SyncCalibrationDiagnostics();
        }

        private void OnEnable()
        {
            _poseSettings = ResolveSettings(_poseSettings);
            ResolveAnimator();
            if (_captureCalibrationOnEnable)
            {
                CaptureCalibration();
                return;
            }

            SyncCalibrationDiagnostics();
        }

        private void OnDisable()
        {
            if (_resetUntrackedHands)
            {
                ResetState();
            }
        }

        public void BindAnimator(Animator animator)
        {
            if (ReferenceEquals(_animator, animator) && HasAnyCalibration())
            {
                return;
            }

            if (_resetUntrackedHands)
            {
                ResetState();
            }

            _animator = animator;
            if (_animator == null)
            {
                EnsureCalibration();
                _calibration.Clear("Missing humanoid Animator.");
                SyncCalibrationDiagnostics();
                return;
            }

            CaptureCalibration();
        }

        [ContextMenu("Capture Calibration")]
        public void CaptureCalibration()
        {
            EnsureCalibration();
            ResolveAnimator();
            _poseSettings = ResolveSettings(_poseSettings);

            if (!SemanticHumanoidHandCalibrationUtility.TryCapture(_animator, _calibration, out string failureReason))
            {
                if (!string.IsNullOrEmpty(failureReason))
                {
                    if (_calibration.LeftHand != null && !_calibration.LeftHand.IsValid && string.IsNullOrEmpty(_calibration.LeftHand.MissingBones))
                    {
                        _calibration.LeftHand.MissingBones = failureReason;
                    }

                    if (_calibration.RightHand != null && !_calibration.RightHand.IsValid && string.IsNullOrEmpty(_calibration.RightHand.MissingBones))
                    {
                        _calibration.RightHand.MissingBones = failureReason;
                    }
                }
            }

            SyncCalibrationDiagnostics();
        }

        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            if (frame == null)
            {
                ResetState();
                return;
            }

            EnsureCalibration();
            EnsureCalibrationBindings();
            ApplyTrackedHand(_calibration.LeftHand, frame.LeftHand, _leftDiagnostics, _applyLeftHand);
            ApplyTrackedHand(_calibration.RightHand, frame.RightHand, _rightDiagnostics, _applyRightHand);
        }

        public void ResetState()
        {
            EnsureCalibration();
            ResetHand(_calibration.LeftHand);
            ResetHand(_calibration.RightHand);
            _leftDiagnostics?.ClearRuntimeState();
            _rightDiagnostics?.ClearRuntimeState();
            SyncCalibrationDiagnostics();
        }

        private void ApplyTrackedHand(
            SemanticHumanoidHandCalibrationData calibration,
            TrackedHandData trackedHand,
            SemanticHumanoidHandRetargetDiagnostics diagnostics,
            bool isEnabled)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.IsCalibrated = calibration != null && calibration.IsValid;
            diagnostics.MissingBones = calibration != null ? calibration.MissingBones ?? string.Empty : "Not calibrated.";
            diagnostics.IsTracked = trackedHand != null && trackedHand.IsTracked;
            diagnostics.Confidence = trackedHand != null ? trackedHand.Confidence : 0f;
            diagnostics.IsApplied = false;

            if (!isEnabled)
            {
                diagnostics.Summary = "disabled";
                return;
            }

            if (calibration == null || !calibration.IsValid)
            {
                diagnostics.Summary = string.IsNullOrEmpty(diagnostics.MissingBones) ? "uncalibrated" : diagnostics.MissingBones;
                return;
            }

            if (trackedHand == null || !trackedHand.IsTracked)
            {
                if (_resetUntrackedHands)
                {
                    ResetHand(calibration);
                }

                diagnostics.Thumb = default(SemanticThumbPose);
                diagnostics.Index = default(SemanticFingerPose);
                diagnostics.Middle = default(SemanticFingerPose);
                diagnostics.Ring = default(SemanticFingerPose);
                diagnostics.Little = default(SemanticFingerPose);
                diagnostics.Summary = "untracked";
                return;
            }

            ApplyFinger(calibration.Index, trackedHand.Index);
            ApplyFinger(calibration.Middle, trackedHand.Middle);
            ApplyFinger(calibration.Ring, trackedHand.Ring);
            ApplyFinger(calibration.Little, trackedHand.Little);
            ApplyThumb(calibration, trackedHand.Thumb);

            diagnostics.IsApplied = true;
            diagnostics.Thumb = trackedHand.Thumb;
            diagnostics.Index = trackedHand.Index;
            diagnostics.Middle = trackedHand.Middle;
            diagnostics.Ring = trackedHand.Ring;
            diagnostics.Little = trackedHand.Little;
            diagnostics.Summary = BuildSummary(trackedHand);
        }

        private void ApplyFinger(SemanticHumanoidFingerCalibration calibration, SemanticFingerPose pose)
        {
            if (calibration == null || !calibration.IsValid)
            {
                return;
            }

            float mcpSplayRadians = _poseSettings.FingerMcpSplayRange.DenormalizeSigned(pose.McpSplay);
            float mcpCurlRadians = _poseSettings.FingerMcpCurlRange.Denormalize01(pose.McpCurl);
            float pipCurlRadians = _poseSettings.FingerPipCurlRange.Denormalize01(pose.PipCurl);
            float dipCurlRadians = _poseSettings.FingerDipCurlRange.Denormalize01(pose.DipCurl);

            ApplyBoneRotation(calibration.Proximal, mcpSplayRadians, 0f, mcpCurlRadians);
            ApplyBoneRotation(calibration.Intermediate, 0f, 0f, pipCurlRadians);
            ApplyBoneRotation(calibration.Distal, 0f, 0f, dipCurlRadians);
        }

        private void ApplyThumb(SemanticHumanoidHandCalibrationData handCalibration, SemanticThumbPose pose)
        {
            SemanticHumanoidFingerCalibration calibration = handCalibration != null ? handCalibration.Thumb : null;
            if (calibration == null || !calibration.IsValid)
            {
                return;
            }

            float baseCurlRadians = _poseSettings.ThumbBaseCurlRange.Denormalize01(pose.BaseCurl);
            float tipCurlRadians = _poseSettings.ThumbTipCurlRange.Denormalize01(pose.TipCurl);

            if (TryApplyThumbSegmentAims(handCalibration, calibration, pose))
            {
                return;
            }

            ApplyThumbRootAim(handCalibration, calibration.Proximal, new Vector3(pose.AimPalmSpace.x, pose.AimPalmSpace.y, pose.AimPalmSpace.z));
            ApplyBoneRotation(calibration.Intermediate, 0f, 0f, baseCurlRadians);
            ApplyBoneRotation(calibration.Distal, 0f, 0f, tipCurlRadians);
        }

        private bool TryApplyThumbSegmentAims(
            SemanticHumanoidHandCalibrationData handCalibration,
            SemanticHumanoidFingerCalibration calibration,
            SemanticThumbPose pose)
        {
            if (handCalibration == null || calibration == null || !calibration.IsValid)
            {
                return false;
            }

            if (!TryBuildHandDirection(handCalibration, new Vector3(pose.ProximalPalmSpace.x, pose.ProximalPalmSpace.y, pose.ProximalPalmSpace.z), out Vector3 proximalDirection)
                || !TryBuildHandDirection(handCalibration, new Vector3(pose.IntermediatePalmSpace.x, pose.IntermediatePalmSpace.y, pose.IntermediatePalmSpace.z), out Vector3 intermediateDirection)
                || !TryBuildHandDirection(handCalibration, new Vector3(pose.DistalPalmSpace.x, pose.DistalPalmSpace.y, pose.DistalPalmSpace.z), out Vector3 distalDirection))
            {
                return false;
            }

            return TryApplyBoneAim(handCalibration, calibration.Proximal, proximalDirection)
                && TryApplyBoneAim(handCalibration, calibration.Intermediate, intermediateDirection)
                && TryApplyBoneAim(handCalibration, calibration.Distal, distalDirection);
        }

        private void ApplyThumbRootAim(
            SemanticHumanoidHandCalibrationData handCalibration,
            SemanticHumanoidBoneCalibration calibration,
            Vector3 aimPalmSpace)
        {
            if (handCalibration == null || handCalibration.HandTransform == null || calibration == null || !calibration.IsBound || calibration.Transform == null)
            {
                return;
            }

            if (!TryGetReferencePalmSpaceDirection(handCalibration, calibration, out Vector3 referencePalmSpace)
                || !TryNormalize(ref aimPalmSpace))
            {
                calibration.Transform.localRotation = calibration.ReferenceLocalRotation;
                return;
            }

            float splayRadians = SignedPlanarAngleRadians(referencePalmSpace, aimPalmSpace);
            float pitchRadians = Mathf.Asin(Mathf.Clamp(aimPalmSpace.y, -1f, 1f))
                - Mathf.Asin(Mathf.Clamp(referencePalmSpace.y, -1f, 1f));

            ApplyBoneRotation(calibration, splayRadians, pitchRadians, 0f);
        }

        private static bool TryGetReferencePalmSpaceDirection(
            SemanticHumanoidHandCalibrationData handCalibration,
            SemanticHumanoidBoneCalibration calibration,
            out Vector3 direction)
        {
            direction = Vector3.zero;
            Transform parent = calibration.Transform.parent;
            Quaternion referenceWorldRotation = parent != null
                ? parent.rotation * calibration.ReferenceLocalRotation
                : calibration.ReferenceLocalRotation;

            Vector3 referenceWorldDirection = referenceWorldRotation * calibration.ReferenceDirectionLocal;
            if (!TryNormalize(ref referenceWorldDirection))
            {
                return false;
            }

            Vector3 referenceHandDirection = handCalibration.HandTransform.InverseTransformDirection(referenceWorldDirection);
            if (!TryNormalize(ref referenceHandDirection))
            {
                return false;
            }

            return TryToPalmSpace(handCalibration, referenceHandDirection, out direction);
        }

        private static bool TryToPalmSpace(SemanticHumanoidHandCalibrationData handCalibration, Vector3 handDirection, out Vector3 palmSpaceDirection)
        {
            palmSpaceDirection = Vector3.zero;
            Vector3 palmForward = handCalibration.PalmForwardLocal;
            Vector3 thumbSide = handCalibration.ThumbSideLocal;
            Vector3 palmNormal = handCalibration.PalmNormalLocal;
            if (!TryNormalize(ref palmForward) || !TryNormalize(ref thumbSide) || !TryNormalize(ref palmNormal))
            {
                return false;
            }

            palmSpaceDirection = new Vector3(
                Vector3.Dot(handDirection, thumbSide),
                Vector3.Dot(handDirection, palmNormal),
                Vector3.Dot(handDirection, palmForward));
            return TryNormalize(ref palmSpaceDirection);
        }

        private static bool TryBuildHandDirection(
            SemanticHumanoidHandCalibrationData handCalibration,
            Vector3 palmSpaceDirection,
            out Vector3 handDirection)
        {
            handDirection = Vector3.zero;
            if (!TryNormalize(ref palmSpaceDirection))
            {
                return false;
            }

            Vector3 palmForward = handCalibration.PalmForwardLocal;
            Vector3 thumbSide = handCalibration.ThumbSideLocal;
            Vector3 palmNormal = handCalibration.PalmNormalLocal;
            if (!TryNormalize(ref palmForward) || !TryNormalize(ref thumbSide) || !TryNormalize(ref palmNormal))
            {
                return false;
            }

            handDirection = thumbSide * palmSpaceDirection.x
                + palmNormal * palmSpaceDirection.y
                + palmForward * palmSpaceDirection.z;
            return TryNormalize(ref handDirection);
        }

        private static bool TryApplyBoneAim(
            SemanticHumanoidHandCalibrationData handCalibration,
            SemanticHumanoidBoneCalibration calibration,
            Vector3 targetHandDirection)
        {
            if (handCalibration == null || handCalibration.HandTransform == null || calibration == null || !calibration.IsBound || calibration.Transform == null)
            {
                return false;
            }

            if (!TryNormalize(ref targetHandDirection))
            {
                return false;
            }

            Transform parent = calibration.Transform.parent;
            Quaternion referenceWorldRotation = parent != null
                ? parent.rotation * calibration.ReferenceLocalRotation
                : calibration.ReferenceLocalRotation;
            Vector3 referenceWorldDirection = referenceWorldRotation * calibration.ReferenceDirectionLocal;
            if (!TryNormalize(ref referenceWorldDirection))
            {
                return false;
            }

            Vector3 desiredWorldDirection = handCalibration.HandTransform.TransformDirection(targetHandDirection);
            if (!TryNormalize(ref desiredWorldDirection))
            {
                return false;
            }

            Quaternion targetWorldRotation = Quaternion.FromToRotation(referenceWorldDirection, desiredWorldDirection) * referenceWorldRotation;
            calibration.Transform.localRotation = parent != null
                ? Quaternion.Inverse(parent.rotation) * targetWorldRotation
                : targetWorldRotation;
            return true;
        }

        private static float SignedPlanarAngleRadians(Vector3 fromPalmSpace, Vector3 toPalmSpace)
        {
            Vector2 from = new Vector2(fromPalmSpace.x, fromPalmSpace.z);
            Vector2 to = new Vector2(toPalmSpace.x, toPalmSpace.z);
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }

            float fromAngle = Mathf.Atan2(from.x, from.y);
            float toAngle = Mathf.Atan2(to.x, to.y);
            return DeltaRadians(toAngle - fromAngle);
        }

        private static float DeltaRadians(float value)
        {
            while (value > Mathf.PI)
            {
                value -= Mathf.PI * 2f;
            }

            while (value < -Mathf.PI)
            {
                value += Mathf.PI * 2f;
            }

            return value;
        }

        private static void ApplyBoneRotation(SemanticHumanoidBoneCalibration calibration, float splayRadians, float pitchRadians, float curlRadians)
        {
            if (calibration == null || !calibration.IsBound || calibration.Transform == null)
            {
                return;
            }

            Quaternion localRotation = calibration.ReferenceLocalRotation;
            localRotation = AppendAxisRotation(localRotation, calibration.SplayAxisLocal, splayRadians);
            localRotation = AppendAxisRotation(localRotation, calibration.PitchAxisLocal, pitchRadians);
            localRotation = AppendAxisRotation(localRotation, calibration.CurlAxisLocal, curlRadians);
            calibration.Transform.localRotation = localRotation;
        }

        private static Quaternion AppendAxisRotation(Quaternion current, Vector3 axisLocal, float radians)
        {
            if (Mathf.Abs(radians) < 1e-5f || !TryNormalize(ref axisLocal))
            {
                return current;
            }

            return current * Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axisLocal);
        }

        private static void ResetHand(SemanticHumanoidHandCalibrationData hand)
        {
            if (hand == null || !hand.IsValid)
            {
                return;
            }

            ResetFinger(hand.Thumb);
            ResetFinger(hand.Index);
            ResetFinger(hand.Middle);
            ResetFinger(hand.Ring);
            ResetFinger(hand.Little);
        }

        private static void ResetFinger(SemanticHumanoidFingerCalibration finger)
        {
            if (finger == null)
            {
                return;
            }

            ResetBone(finger.Proximal);
            ResetBone(finger.Intermediate);
            ResetBone(finger.Distal);
        }

        private static void ResetBone(SemanticHumanoidBoneCalibration calibration)
        {
            if (calibration == null || !calibration.IsBound || calibration.Transform == null)
            {
                return;
            }

            calibration.Transform.localRotation = calibration.ReferenceLocalRotation;
        }

        private void EnsureCalibrationBindings()
        {
            if (_animator == null)
            {
                return;
            }

            if ((_applyLeftHand && NeedsCalibration(_calibration.LeftHand)) || (_applyRightHand && NeedsCalibration(_calibration.RightHand)))
            {
                CaptureCalibration();
            }
        }

        private static bool NeedsCalibration(SemanticHumanoidHandCalibrationData hand)
        {
            if (hand == null || !hand.IsValid || hand.HandTransform == null)
            {
                return true;
            }

            return !HasBoundBone(hand.Thumb.Proximal)
                || !HasBoundBone(hand.Thumb.Intermediate)
                || !HasBoundBone(hand.Thumb.Distal)
                || !HasBoundBone(hand.Index.Proximal)
                || !HasBoundBone(hand.Index.Intermediate)
                || !HasBoundBone(hand.Index.Distal)
                || !HasBoundBone(hand.Middle.Proximal)
                || !HasBoundBone(hand.Middle.Intermediate)
                || !HasBoundBone(hand.Middle.Distal)
                || !HasBoundBone(hand.Ring.Proximal)
                || !HasBoundBone(hand.Ring.Intermediate)
                || !HasBoundBone(hand.Ring.Distal)
                || !HasBoundBone(hand.Little.Proximal)
                || !HasBoundBone(hand.Little.Intermediate)
                || !HasBoundBone(hand.Little.Distal);
        }

        private static bool HasBoundBone(SemanticHumanoidBoneCalibration calibration)
        {
            return calibration != null && calibration.IsBound && calibration.Transform != null;
        }

        private void ResolveAnimator()
        {
            if (_animator != null && _animator.avatar != null && _animator.isHuman)
            {
                return;
            }

            _animator = GetComponent<Animator>();
            if (_animator != null && _animator.isHuman)
            {
                return;
            }

            _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null && _animator.isHuman)
            {
                return;
            }

            Animator[] animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null && animator.avatar != null && animator.isHuman)
                {
                    _animator = animator;
                    return;
                }
            }

            _animator = null;
        }

        private void EnsureCalibration()
        {
            if (_calibration == null)
            {
                _calibration = new SemanticHumanoidHandCalibration();
            }

            if (_leftDiagnostics == null)
            {
                _leftDiagnostics = new SemanticHumanoidHandRetargetDiagnostics();
            }

            if (_rightDiagnostics == null)
            {
                _rightDiagnostics = new SemanticHumanoidHandRetargetDiagnostics();
            }
        }

        private void SyncCalibrationDiagnostics()
        {
            EnsureCalibration();
            SyncHandDiagnostics(_leftDiagnostics, _calibration.LeftHand);
            SyncHandDiagnostics(_rightDiagnostics, _calibration.RightHand);
        }

        private static void SyncHandDiagnostics(
            SemanticHumanoidHandRetargetDiagnostics diagnostics,
            SemanticHumanoidHandCalibrationData calibration)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.IsCalibrated = calibration != null && calibration.IsValid;
            diagnostics.MissingBones = calibration != null ? calibration.MissingBones ?? string.Empty : "Not calibrated.";
        }

        private bool HasAnyCalibration()
        {
            return _calibration != null
                && ((_calibration.LeftHand != null && _calibration.LeftHand.IsValid)
                    || (_calibration.RightHand != null && _calibration.RightHand.IsValid));
        }

        private static string BuildSummary(TrackedHandData hand)
        {
            if (hand == null || !hand.IsTracked)
            {
                return "untracked";
            }

            return string.Format(
                "conf {0:0.00} | thumb aim {1:0.00}/{2:0.00}/{3:0.00} curl {4:0.00}/{5:0.00} | index {6:0.00}/{7:0.00}/{8:0.00}/{9:0.00}",
                hand.Confidence,
                hand.Thumb.AimPalmSpace.x,
                hand.Thumb.AimPalmSpace.y,
                hand.Thumb.AimPalmSpace.z,
                hand.Thumb.BaseCurl,
                hand.Thumb.TipCurl,
                hand.Index.McpCurl,
                hand.Index.PipCurl,
                hand.Index.DipCurl,
                hand.Index.McpSplay);
        }

        private static SemanticHandPoseSettings ResolveSettings(SemanticHandPoseSettings settings)
        {
            if (Mathf.Abs(settings.FingerMcpCurlRange.MaxRadians - settings.FingerMcpCurlRange.MinRadians) < 1e-5f)
            {
                return SemanticHandPoseSettings.Default;
            }

            return settings;
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
