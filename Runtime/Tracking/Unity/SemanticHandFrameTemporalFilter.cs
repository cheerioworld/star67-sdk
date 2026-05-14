using System;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Star67.Tracking.Unity
{
    public enum SemanticHandTemporalFilterPhase
    {
        Untracked = 0,
        Tracked = 1,
        Hold = 2,
        Relax = 3
    }

    [Serializable]
    public sealed class SemanticHandTemporalHandDiagnostics
    {
        public SemanticHandTemporalFilterPhase Phase;
        public bool HasRawHand;
        public bool HasFilteredHand;
        public float RawConfidence;
        public float OutputConfidence;
        public float HoldRemainingSeconds;
        public float RelaxProgress01;

        public void Clear()
        {
            Phase = SemanticHandTemporalFilterPhase.Untracked;
            HasRawHand = false;
            HasFilteredHand = false;
            RawConfidence = 0f;
            OutputConfidence = 0f;
            HoldRemainingSeconds = 0f;
            RelaxProgress01 = 0f;
        }
    }

    [Serializable]
    public sealed class SemanticHandTemporalFilterDiagnostics
    {
        [SerializeField] private SemanticHandTemporalHandDiagnostics _leftHand = new SemanticHandTemporalHandDiagnostics();
        [SerializeField] private SemanticHandTemporalHandDiagnostics _rightHand = new SemanticHandTemporalHandDiagnostics();

        public SemanticHandTemporalHandDiagnostics LeftHand => _leftHand;
        public SemanticHandTemporalHandDiagnostics RightHand => _rightHand;

        public void Clear()
        {
            _leftHand?.Clear();
            _rightHand?.Clear();
        }
    }

    [AddComponentMenu("Star67/Semantic Hand Frame Temporal Filter")]
    [DisallowMultipleComponent]
    public sealed class SemanticHandFrameTemporalFilter : MonoBehaviour, ITrackingFrameSource
    {
        private const float Ln2 = 0.6931471805599453f;

        [SerializeField] private MonoBehaviour _sourceBehaviour;
        [SerializeField] private SemanticHandTemporalFilterSettings _settings = SemanticHandTemporalFilterSettings.Default;
        [SerializeField] private SemanticHandTemporalFilterDiagnostics _diagnostics = new SemanticHandTemporalFilterDiagnostics();

        private readonly TrackingFrameBuffer _sourceFrame = new TrackingFrameBuffer();
        private readonly TrackingFrameBuffer _latestRawFrame = new TrackingFrameBuffer();
        private readonly TrackingFrameBuffer _latestFrame = new TrackingFrameBuffer();
        private readonly TrackingSessionInfo _sessionInfo = new TrackingSessionInfo();
        private readonly HandState _leftState = new HandState();
        private readonly HandState _rightState = new HandState();

        private ITrackingFrameSource _runtimeSource;
        private MonoBehaviour _lastInvalidSourceBehaviour;
        private ulong _lastObservedSequence;
        private long _lastObservedCaptureTimestampUs;
        private long _lastProcessedCaptureTimestampUs;
        private bool _hasObservedSequence;
        private bool _hasObservedCaptureTimestampUs;
        private bool _hasLatestFrame;
        private bool _hasLatestRawFrame;

        public TrackingConnectionState State => ResolveSource(logWarnings: false)?.State ?? TrackingConnectionState.Stopped;
        public TrackingSessionInfo SessionInfo
        {
            get
            {
                SyncSessionInfo(ResolveSource(logWarnings: false));
                return _sessionInfo;
            }
        }

        public SemanticHandTemporalFilterSettings Settings => _settings;
        public SemanticHandTemporalFilterDiagnostics Diagnostics => _diagnostics;
        public ITrackingFrameSource WrappedSource => ResolveSource(logWarnings: false);

        private void Awake()
        {
            _settings = SemanticHandTemporalFilterSettings.Resolve(_settings);
        }

        private void OnValidate()
        {
            _settings = SemanticHandTemporalFilterSettings.Resolve(_settings);
        }

        private void OnDisable()
        {
            Dispose();
        }

        public void SetSource(ITrackingFrameSource source)
        {
            if (AreSameSource(_runtimeSource, source))
            {
                return;
            }

            _runtimeSource = IsAlive(source) ? source : null;
            _sourceBehaviour = source as MonoBehaviour;
            ClearState();
        }

        public void Update()
        {
            _settings = SemanticHandTemporalFilterSettings.Resolve(_settings);

            ITrackingFrameSource source = ResolveSource(logWarnings: true);
            SyncSessionInfo(source);
            if (source == null)
            {
                return;
            }

            source.Update();
            if (!source.TryCopyLatestFrame(_sourceFrame))
            {
                return;
            }

            _latestRawFrame.CopyFrom(_sourceFrame);
            _hasLatestRawFrame = true;

            if (!IsFreshFrame(_sourceFrame))
            {
                return;
            }

            float deltaTime = ResolveDeltaTimeSeconds(_sourceFrame.CaptureTimestampUs);

            _latestFrame.CopyFrom(_sourceFrame);
            _latestFrame.Features &= ~(TrackingFeatureFlags.LeftHand | TrackingFeatureFlags.RightHand);
            _latestFrame.LeftHand.Clear();
            _latestFrame.RightHand.Clear();

            if (UpdateHand(
                    _sourceFrame.LeftHand,
                    (_sourceFrame.Features & TrackingFeatureFlags.LeftHand) != 0,
                    _leftState,
                    _diagnostics.LeftHand,
                    _latestFrame.LeftHand,
                    deltaTime))
            {
                _latestFrame.Features |= TrackingFeatureFlags.LeftHand;
            }

            if (UpdateHand(
                    _sourceFrame.RightHand,
                    (_sourceFrame.Features & TrackingFeatureFlags.RightHand) != 0,
                    _rightState,
                    _diagnostics.RightHand,
                    _latestFrame.RightHand,
                    deltaTime))
            {
                _latestFrame.Features |= TrackingFeatureFlags.RightHand;
            }

            _hasLatestFrame = true;
        }

        public bool TryCopyLatestFrame(TrackingFrameBuffer destination)
        {
            if (destination == null || !_hasLatestFrame)
            {
                return false;
            }

            destination.CopyFrom(_latestFrame);
            return true;
        }

        public bool TryCopyLatestRawFrame(TrackingFrameBuffer destination)
        {
            if (destination == null || !_hasLatestRawFrame)
            {
                return false;
            }

            destination.CopyFrom(_latestRawFrame);
            return true;
        }

        public void Dispose()
        {
            ClearState();
        }

        private bool UpdateHand(
            TrackedHandData rawHand,
            bool hasRawFeature,
            HandState state,
            SemanticHandTemporalHandDiagnostics diagnostics,
            TrackedHandData destination,
            float deltaTime)
        {
            if (diagnostics == null || destination == null)
            {
                return false;
            }

            diagnostics.Clear();

            bool hasRawHand = hasRawFeature && rawHand != null && rawHand.IsTracked;
            float rawConfidence = hasRawHand && IsFinite(rawHand.Confidence) ? Mathf.Clamp01(rawHand.Confidence) : 0f;
            diagnostics.HasRawHand = hasRawHand;
            diagnostics.RawConfidence = rawConfidence;

            if (hasRawHand && IsFiniteHand(rawHand))
            {
                FilterTrackedHand(state, rawHand, deltaTime);
                destination.CopyFrom(state.Filtered);
                destination.IsTracked = true;
                destination.Confidence = rawConfidence;
                diagnostics.Phase = SemanticHandTemporalFilterPhase.Tracked;
                diagnostics.HasFilteredHand = true;
                diagnostics.OutputConfidence = destination.Confidence;
                return true;
            }

            return AdvanceDropout(state, diagnostics, destination, deltaTime);
        }

        private void FilterTrackedHand(HandState state, TrackedHandData rawHand, float deltaTime)
        {
            float confidence = Mathf.Clamp01(rawHand.Confidence);
            if (!state.HasFiltered)
            {
                state.Filtered.CopyFrom(rawHand);
                state.Filtered.IsTracked = true;
                state.Filtered.Confidence = confidence;
                state.PreviousRaw.CopyFrom(rawHand);
                state.HasPreviousRaw = true;
                state.HasFiltered = true;
                state.Phase = SemanticHandTemporalFilterPhase.Tracked;
                state.MissingElapsedSeconds = 0f;
                return;
            }

            TrackingPose previousFilteredWrist = state.Filtered.WristPoseSourceSpace;
            TrackingPose previousRawWrist = state.HasPreviousRaw ? state.PreviousRaw.WristPoseSourceSpace : rawHand.WristPoseSourceSpace;
            state.Filtered.WristPoseSourceSpace = FilterWristPose(previousFilteredWrist, rawHand.WristPoseSourceSpace, previousRawWrist, confidence, deltaTime);
            state.Filtered.Thumb = FilterThumb(state.Filtered.Thumb, rawHand.Thumb, state.HasPreviousRaw ? state.PreviousRaw.Thumb : rawHand.Thumb, confidence, deltaTime);
            state.Filtered.Index = FilterFinger(state.Filtered.Index, rawHand.Index, state.HasPreviousRaw ? state.PreviousRaw.Index : rawHand.Index, confidence, deltaTime);
            state.Filtered.Middle = FilterFinger(state.Filtered.Middle, rawHand.Middle, state.HasPreviousRaw ? state.PreviousRaw.Middle : rawHand.Middle, confidence, deltaTime);
            state.Filtered.Ring = FilterFinger(state.Filtered.Ring, rawHand.Ring, state.HasPreviousRaw ? state.PreviousRaw.Ring : rawHand.Ring, confidence, deltaTime);
            state.Filtered.Little = FilterFinger(state.Filtered.Little, rawHand.Little, state.HasPreviousRaw ? state.PreviousRaw.Little : rawHand.Little, confidence, deltaTime);
            state.Filtered.IsTracked = true;
            state.Filtered.Confidence = confidence;
            state.PreviousRaw.CopyFrom(rawHand);
            state.HasPreviousRaw = true;
            state.HasFiltered = true;
            state.Phase = SemanticHandTemporalFilterPhase.Tracked;
            state.MissingElapsedSeconds = 0f;
        }

        private bool AdvanceDropout(
            HandState state,
            SemanticHandTemporalHandDiagnostics diagnostics,
            TrackedHandData destination,
            float deltaTime)
        {
            if (!state.HasFiltered)
            {
                state.Reset();
                destination.Clear();
                diagnostics.Phase = SemanticHandTemporalFilterPhase.Untracked;
                return false;
            }

            if (state.Phase != SemanticHandTemporalFilterPhase.Hold && state.Phase != SemanticHandTemporalFilterPhase.Relax)
            {
                state.MissingStart.CopyFrom(state.Filtered);
                state.DropoutStartConfidence = Mathf.Clamp01(state.Filtered.Confidence);
                state.FrozenWristPose = state.Filtered.WristPoseSourceSpace;
                state.MissingElapsedSeconds = 0f;
            }

            state.MissingElapsedSeconds += Mathf.Max(0f, deltaTime);

            float holdDuration = _settings.HoldDurationSeconds;
            float relaxDuration = _settings.RelaxDurationSeconds;
            float totalDuration = holdDuration + relaxDuration;
            float confidenceT = totalDuration > 1e-5f
                ? Mathf.Clamp01(state.MissingElapsedSeconds / totalDuration)
                : 1f;
            float outputConfidence = Mathf.Lerp(state.DropoutStartConfidence, 0f, confidenceT);

            if (state.MissingElapsedSeconds < holdDuration)
            {
                state.Phase = SemanticHandTemporalFilterPhase.Hold;
                state.Filtered.CopyFrom(state.MissingStart);
                state.Filtered.WristPoseSourceSpace = state.FrozenWristPose;
                state.Filtered.IsTracked = true;
                state.Filtered.Confidence = outputConfidence;
                destination.CopyFrom(state.Filtered);
                diagnostics.Phase = SemanticHandTemporalFilterPhase.Hold;
                diagnostics.HasFilteredHand = true;
                diagnostics.OutputConfidence = outputConfidence;
                diagnostics.HoldRemainingSeconds = Mathf.Max(0f, holdDuration - state.MissingElapsedSeconds);
                return true;
            }

            if (relaxDuration > 1e-5f && state.MissingElapsedSeconds < totalDuration)
            {
                float relaxT = Mathf.Clamp01((state.MissingElapsedSeconds - holdDuration) / relaxDuration);
                state.Phase = SemanticHandTemporalFilterPhase.Relax;
                state.Filtered.CopyFrom(state.MissingStart);
                state.Filtered.WristPoseSourceSpace = state.FrozenWristPose;
                state.Filtered.Thumb = SemanticThumbPose.Lerp(state.MissingStart.Thumb, _settings.RelaxedThumb, relaxT);
                state.Filtered.Index = SemanticFingerPose.Lerp(state.MissingStart.Index, default(SemanticFingerPose), relaxT);
                state.Filtered.Middle = SemanticFingerPose.Lerp(state.MissingStart.Middle, default(SemanticFingerPose), relaxT);
                state.Filtered.Ring = SemanticFingerPose.Lerp(state.MissingStart.Ring, default(SemanticFingerPose), relaxT);
                state.Filtered.Little = SemanticFingerPose.Lerp(state.MissingStart.Little, default(SemanticFingerPose), relaxT);
                state.Filtered.IsTracked = true;
                state.Filtered.Confidence = outputConfidence;
                destination.CopyFrom(state.Filtered);
                diagnostics.Phase = SemanticHandTemporalFilterPhase.Relax;
                diagnostics.HasFilteredHand = true;
                diagnostics.OutputConfidence = outputConfidence;
                diagnostics.RelaxProgress01 = relaxT;
                return true;
            }

            state.Reset();
            destination.Clear();
            diagnostics.Phase = SemanticHandTemporalFilterPhase.Untracked;
            diagnostics.OutputConfidence = 0f;
            return false;
        }

        private TrackingPose FilterWristPose(
            TrackingPose current,
            TrackingPose target,
            TrackingPose previousRaw,
            float confidence,
            float deltaTime)
        {
            Vector3 currentPosition = ToVector3(current.Position);
            Vector3 targetPosition = ToVector3(target.Position);
            Vector3 previousRawPosition = ToVector3(previousRaw.Position);
            float positionSpeed = deltaTime > 1e-5f ? Vector3.Distance(previousRawPosition, targetPosition) / deltaTime : 0f;
            float positionAlpha = ComputeBlendAlpha(_settings.WristPosition, positionSpeed, confidence, deltaTime);

            Quaternion currentRotation = NormalizeQuaternion(ToQuaternion(current.Rotation));
            Quaternion targetRotation = NormalizeQuaternion(ToQuaternion(target.Rotation));
            Quaternion previousRawRotation = NormalizeQuaternion(ToQuaternion(previousRaw.Rotation));
            float rotationSpeed = deltaTime > 1e-5f ? Quaternion.Angle(previousRawRotation, targetRotation) / deltaTime : 0f;
            float rotationAlpha = ComputeBlendAlpha(_settings.WristRotation, rotationSpeed, confidence, deltaTime);

            return new TrackingPose
            {
                Position = ToFloat3(Vector3.Lerp(currentPosition, targetPosition, positionAlpha)),
                Rotation = ToQuaternion(Quaternion.Slerp(currentRotation, targetRotation, rotationAlpha))
            };
        }

        private SemanticFingerPose FilterFinger(
            SemanticFingerPose current,
            SemanticFingerPose target,
            SemanticFingerPose previousRaw,
            float confidence,
            float deltaTime)
        {
            target.Clamp();
            previousRaw.Clamp();

            current.McpCurl = FilterScalar(
                current.McpCurl,
                target.McpCurl,
                Mathf.Abs(target.McpCurl - previousRaw.McpCurl) / Mathf.Max(deltaTime, 1e-5f),
                _settings.FingerCurl,
                confidence,
                deltaTime);
            current.PipCurl = FilterScalar(
                current.PipCurl,
                target.PipCurl,
                Mathf.Abs(target.PipCurl - previousRaw.PipCurl) / Mathf.Max(deltaTime, 1e-5f),
                _settings.FingerCurl,
                confidence,
                deltaTime);
            current.DipCurl = FilterScalar(
                current.DipCurl,
                target.DipCurl,
                Mathf.Abs(target.DipCurl - previousRaw.DipCurl) / Mathf.Max(deltaTime, 1e-5f),
                _settings.FingerCurl,
                confidence,
                deltaTime);
            current.McpSplay = FilterScalar(
                current.McpSplay,
                target.McpSplay,
                Mathf.Abs(target.McpSplay - previousRaw.McpSplay) / Mathf.Max(deltaTime, 1e-5f),
                _settings.FingerSplay,
                confidence,
                deltaTime);
            current.Clamp();
            return current;
        }

        private SemanticThumbPose FilterThumb(
            SemanticThumbPose current,
            SemanticThumbPose target,
            SemanticThumbPose previousRaw,
            float confidence,
            float deltaTime)
        {
            target.Clamp();
            previousRaw.Clamp();

            current.BaseCurl = FilterScalar(
                current.BaseCurl,
                target.BaseCurl,
                Mathf.Abs(target.BaseCurl - previousRaw.BaseCurl) / Mathf.Max(deltaTime, 1e-5f),
                _settings.ThumbCurl,
                confidence,
                deltaTime);
            current.TipCurl = FilterScalar(
                current.TipCurl,
                target.TipCurl,
                Mathf.Abs(target.TipCurl - previousRaw.TipCurl) / Mathf.Max(deltaTime, 1e-5f),
                _settings.ThumbCurl,
                confidence,
                deltaTime);
            current.AimPalmSpace = FilterDirection(
                current.AimPalmSpace,
                target.AimPalmSpace,
                previousRaw.AimPalmSpace,
                _settings.ThumbDirection,
                confidence,
                deltaTime);
            current.ProximalPalmSpace = FilterDirection(
                current.ProximalPalmSpace,
                target.ProximalPalmSpace,
                previousRaw.ProximalPalmSpace,
                _settings.ThumbDirection,
                confidence,
                deltaTime);
            current.IntermediatePalmSpace = FilterDirection(
                current.IntermediatePalmSpace,
                target.IntermediatePalmSpace,
                previousRaw.IntermediatePalmSpace,
                _settings.ThumbDirection,
                confidence,
                deltaTime);
            current.DistalPalmSpace = FilterDirection(
                current.DistalPalmSpace,
                target.DistalPalmSpace,
                previousRaw.DistalPalmSpace,
                _settings.ThumbDirection,
                confidence,
                deltaTime);
            current.Clamp();
            return current;
        }

        private float FilterScalar(
            float current,
            float target,
            float speed,
            SemanticHandTemporalChannelSettings channel,
            float confidence,
            float deltaTime)
        {
            float alpha = ComputeBlendAlpha(channel, speed, confidence, deltaTime);
            return Mathf.Lerp(current, target, alpha);
        }

        private float3 FilterDirection(
            float3 current,
            float3 target,
            float3 previousRaw,
            SemanticHandTemporalChannelSettings channel,
            float confidence,
            float deltaTime)
        {
            Vector3 currentDirection = NormalizeDirection(ToVector3(current), ToVector3(target));
            Vector3 targetDirection = NormalizeDirection(ToVector3(target), currentDirection);
            Vector3 previousRawDirection = NormalizeDirection(ToVector3(previousRaw), targetDirection);
            float speed = deltaTime > 1e-5f ? Vector3.Angle(previousRawDirection, targetDirection) / deltaTime : 0f;
            float alpha = ComputeBlendAlpha(channel, speed, confidence, deltaTime);
            Vector3 filtered = Vector3.Slerp(currentDirection, targetDirection, alpha);
            return ToFloat3(NormalizeDirection(filtered, targetDirection));
        }

        private float ComputeBlendAlpha(
            SemanticHandTemporalChannelSettings channel,
            float speed,
            float confidence,
            float deltaTime)
        {
            float motion01 = Mathf.InverseLerp(channel.MotionLow, channel.MotionHigh, Mathf.Max(0f, speed));
            float confidence01 = Mathf.InverseLerp(_settings.LowConfidenceThreshold, 1f, Mathf.Clamp01(confidence));
            float trackedHalfLife = Mathf.Lerp(channel.StableHalfLife, channel.ResponsiveHalfLife, motion01);
            float effectiveHalfLife = Mathf.Lerp(channel.LowConfidenceHalfLife, trackedHalfLife, confidence01);
            if (effectiveHalfLife <= 1e-4f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-Ln2 * Mathf.Max(0f, deltaTime) / effectiveHalfLife);
        }

        private float ResolveDeltaTimeSeconds(long captureTimestampUs)
        {
            float fallback = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, _settings.MaxDeltaTimeSeconds);
            if (captureTimestampUs <= 0)
            {
                return fallback;
            }

            if (_lastProcessedCaptureTimestampUs <= 0 || captureTimestampUs <= _lastProcessedCaptureTimestampUs)
            {
                _lastProcessedCaptureTimestampUs = captureTimestampUs;
                return fallback;
            }

            float delta = (captureTimestampUs - _lastProcessedCaptureTimestampUs) * 0.000001f;
            _lastProcessedCaptureTimestampUs = captureTimestampUs;
            return Mathf.Clamp(delta, 0.0001f, _settings.MaxDeltaTimeSeconds);
        }

        private bool IsFreshFrame(TrackingFrameBuffer frame)
        {
            if (frame.Sequence != 0)
            {
                if (_hasObservedSequence && frame.Sequence == _lastObservedSequence)
                {
                    return false;
                }

                _lastObservedSequence = frame.Sequence;
                _hasObservedSequence = true;
                return true;
            }

            if (frame.CaptureTimestampUs != 0)
            {
                if (_hasObservedCaptureTimestampUs && frame.CaptureTimestampUs == _lastObservedCaptureTimestampUs)
                {
                    return false;
                }

                _lastObservedCaptureTimestampUs = frame.CaptureTimestampUs;
                _hasObservedCaptureTimestampUs = true;
                return true;
            }

            return true;
        }

        private void SyncSessionInfo(ITrackingFrameSource source)
        {
            _sessionInfo.CopyFrom(source?.SessionInfo);
        }

        private void ClearState()
        {
            _sourceFrame.Clear();
            _latestRawFrame.Clear();
            _latestFrame.Clear();
            _sessionInfo.CopyFrom(null);
            _leftState.Reset();
            _rightState.Reset();
            _diagnostics?.Clear();
            _lastObservedSequence = 0;
            _lastObservedCaptureTimestampUs = 0;
            _lastProcessedCaptureTimestampUs = 0;
            _hasObservedSequence = false;
            _hasObservedCaptureTimestampUs = false;
            _hasLatestFrame = false;
            _hasLatestRawFrame = false;
        }

        private ITrackingFrameSource ResolveSource(bool logWarnings)
        {
            if (IsAlive(_runtimeSource))
            {
                return _runtimeSource;
            }

            _runtimeSource = null;

            if (_sourceBehaviour == null)
            {
                _lastInvalidSourceBehaviour = null;
                return null;
            }

            if (_sourceBehaviour is ITrackingFrameSource source)
            {
                _lastInvalidSourceBehaviour = null;
                return source;
            }

            if (logWarnings && !ReferenceEquals(_lastInvalidSourceBehaviour, _sourceBehaviour))
            {
                Debug.LogWarning(
                    $"{nameof(SemanticHandFrameTemporalFilter)} requires a {nameof(MonoBehaviour)} that implements {nameof(ITrackingFrameSource)}. " +
                    $"Assigned type '{_sourceBehaviour.GetType().Name}' will be ignored.",
                    this);
                _lastInvalidSourceBehaviour = _sourceBehaviour;
            }

            return null;
        }

        private static bool IsFiniteHand(TrackedHandData hand)
        {
            if (hand == null
                || !IsFinite(hand.Confidence)
                || !math.all(math.isfinite(hand.WristPoseSourceSpace.Position))
                || !math.all(math.isfinite(hand.WristPoseSourceSpace.Rotation.value))
                || !IsFinite(hand.Index.McpCurl)
                || !IsFinite(hand.Index.PipCurl)
                || !IsFinite(hand.Index.DipCurl)
                || !IsFinite(hand.Index.McpSplay)
                || !IsFinite(hand.Middle.McpCurl)
                || !IsFinite(hand.Middle.PipCurl)
                || !IsFinite(hand.Middle.DipCurl)
                || !IsFinite(hand.Middle.McpSplay)
                || !IsFinite(hand.Ring.McpCurl)
                || !IsFinite(hand.Ring.PipCurl)
                || !IsFinite(hand.Ring.DipCurl)
                || !IsFinite(hand.Ring.McpSplay)
                || !IsFinite(hand.Little.McpCurl)
                || !IsFinite(hand.Little.PipCurl)
                || !IsFinite(hand.Little.DipCurl)
                || !IsFinite(hand.Little.McpSplay)
                || !IsFinite(hand.Thumb.BaseCurl)
                || !IsFinite(hand.Thumb.TipCurl)
                || !math.all(math.isfinite(hand.Thumb.AimPalmSpace))
                || !math.all(math.isfinite(hand.Thumb.ProximalPalmSpace))
                || !math.all(math.isfinite(hand.Thumb.IntermediatePalmSpace))
                || !math.all(math.isfinite(hand.Thumb.DistalPalmSpace)))
            {
                return false;
            }

            return true;
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || !IsFinite(value.w))
            {
                return Quaternion.identity;
            }

            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude < 1e-6f)
            {
                return Quaternion.identity;
            }

            return new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
        }

        private static Vector3 NormalizeDirection(Vector3 value, Vector3 fallback)
        {
            if (value.sqrMagnitude > 1e-8f && IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z))
            {
                return value.normalized;
            }

            if (fallback.sqrMagnitude > 1e-8f && IsFinite(fallback.x) && IsFinite(fallback.y) && IsFinite(fallback.z))
            {
                return fallback.normalized;
            }

            return Vector3.forward;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Quaternion ToQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            Quaternion normalized = NormalizeQuaternion(value);
            return new quaternion(normalized.x, normalized.y, normalized.z, normalized.w);
        }

        private static bool AreSameSource(ITrackingFrameSource lhs, ITrackingFrameSource rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (lhs is Object lhsObject && rhs is Object rhsObject)
            {
                return lhsObject == rhsObject;
            }

            return false;
        }

        private static bool IsAlive(ITrackingFrameSource source)
        {
            if (ReferenceEquals(source, null))
            {
                return false;
            }

            if (source is Object unityObject)
            {
                return unityObject != null;
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class HandState
        {
            public readonly TrackedHandData Filtered = new TrackedHandData();
            public readonly TrackedHandData PreviousRaw = new TrackedHandData();
            public readonly TrackedHandData MissingStart = new TrackedHandData();

            public bool HasFiltered;
            public bool HasPreviousRaw;
            public SemanticHandTemporalFilterPhase Phase;
            public float MissingElapsedSeconds;
            public float DropoutStartConfidence;
            public TrackingPose FrozenWristPose;

            public void Reset()
            {
                Filtered.Clear();
                PreviousRaw.Clear();
                MissingStart.Clear();
                HasFiltered = false;
                HasPreviousRaw = false;
                Phase = SemanticHandTemporalFilterPhase.Untracked;
                MissingElapsedSeconds = 0f;
                DropoutStartConfidence = 0f;
                FrozenWristPose = TrackingPose.Identity;
            }
        }
    }
}
