using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class TrackingFrameSourceUdpSender : MonoBehaviour
    {
        [Header("Network")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private int localDataPort;

        [Header("Source")]
        [SerializeField] private MonoBehaviour sourceBehaviour;

        [Header("Transport")]
        [SerializeField] private float sendRateHz = 30f;

        private readonly TrackingFrameBuffer _sampleFrame = new TrackingFrameBuffer();
        private readonly TrackingFrameBuffer _pendingFrame = new TrackingFrameBuffer();

        private UdpTrackingSender _sender;
        private ITrackingFrameSource _runtimeSource;
        private ITrackingFrameSource _lastResolvedSource;
        private MonoBehaviour _lastInvalidSourceBehaviour;
        private double _nextSendTime;
        private ulong _lastObservedSequence;
        private long _lastObservedCaptureTimestampUs;
        private bool _hasObservedSequence;
        private bool _hasObservedCaptureTimestampUs;
        private bool _hasPendingFrame;

        public TrackingConnectionState State => _sender?.State ?? TrackingConnectionState.Stopped;
        public int LocalPort => _sender?.LocalDataPort ?? 0;
        public string RemoteEndpoint => _sender?.RemoteEndPoint?.ToString() ?? string.Empty;
        public ITrackingFrameSource ActiveSource => ResolveSource(logWarnings: false);

        private void OnEnable()
        {
            if (Application.isPlaying && autoStart)
            {
                StartSending();
            }
        }

        private void OnDisable()
        {
            StopSending();
        }

        private void Update()
        {
            if (_sender == null)
            {
                return;
            }

            ITrackingFrameSource source = ResolveSource(logWarnings: true);

            if (!AreSameSource(_lastResolvedSource, source))
            {
                ResetSourceState();
                _lastResolvedSource = source;
            }

            if (source != null)
            {
                source.Update();
                SyncSessionInfo(source);
                if (source.TryCopyLatestFrame(_sampleFrame) && IsFreshFrame(_sampleFrame))
                {
                    _pendingFrame.CopyFrom(_sampleFrame);
                    _hasPendingFrame = true;
                }
            }
            else
            {
                SyncSessionInfo(null);
            }

            _sender.Update();
            if (!_hasPendingFrame)
            {
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            if (now < _nextSendTime)
            {
                return;
            }

            if (_sender.SendFrame(_pendingFrame))
            {
                _hasPendingFrame = false;
                _nextSendTime = now + GetSendIntervalSeconds();
            }
        }

        public void SetSource(ITrackingFrameSource source)
        {
            if (AreSameSource(_runtimeSource, source))
            {
                return;
            }

            _runtimeSource = IsAlive(source) ? source : null;
            ResetSourceState();
            _lastResolvedSource = null;
        }

        public void StartSending()
        {
            if (_sender != null)
            {
                return;
            }

            _sender = new UdpTrackingSender(preferredLocalPort: localDataPort);
            SyncSessionInfo(ResolveSource(logWarnings: true));
            _sender.Start();
            _nextSendTime = Time.unscaledTimeAsDouble;
        }

        public void StopSending()
        {
            _sender?.Dispose();
            _sender = null;
            _lastResolvedSource = null;
            ResetSourceState();
        }

        private void OnValidate()
        {
            if (sendRateHz < 1f)
            {
                sendRateHz = 1f;
            }
        }

        private ITrackingFrameSource ResolveSource(bool logWarnings)
        {
            if (IsAlive(_runtimeSource))
            {
                return _runtimeSource;
            }

            _runtimeSource = null;

            if (sourceBehaviour == null)
            {
                _lastInvalidSourceBehaviour = null;
                return null;
            }

            if (sourceBehaviour is ITrackingFrameSource source)
            {
                _lastInvalidSourceBehaviour = null;
                return source;
            }

            if (logWarnings && !ReferenceEquals(_lastInvalidSourceBehaviour, sourceBehaviour))
            {
                Debug.LogWarning(
                    $"{nameof(TrackingFrameSourceUdpSender)} requires a {nameof(MonoBehaviour)} that implements {nameof(ITrackingFrameSource)}. " +
                    $"Assigned type '{sourceBehaviour.GetType().Name}' will be ignored.",
                    this);
                _lastInvalidSourceBehaviour = sourceBehaviour;
            }

            return null;
        }

        private void SyncSessionInfo(ITrackingFrameSource source)
        {
            if (_sender == null)
            {
                return;
            }

            _sender.SessionInfo.CopyFrom(source?.SessionInfo);
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

        private void ResetSourceState()
        {
            _sampleFrame.Clear();
            _pendingFrame.Clear();
            _hasPendingFrame = false;
            _lastObservedSequence = 0;
            _lastObservedCaptureTimestampUs = 0;
            _hasObservedSequence = false;
            _hasObservedCaptureTimestampUs = false;
            _nextSendTime = Time.unscaledTimeAsDouble;
        }

        private float GetSendIntervalSeconds()
        {
            return 1f / Mathf.Max(1f, sendRateHz);
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
    }
}
