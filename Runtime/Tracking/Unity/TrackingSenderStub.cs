using UnityEngine;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class TrackingSenderStub : MonoBehaviour
    {
        private const float DefaultHeadDepth = 0.45f;

        [Header("Network")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private int localDataPort;
        [SerializeField] private string deviceName = "Unity Tracking Sender Stub";
        [SerializeField] private string appVersion = "sender-stub";

        [Header("Frame")]
        [SerializeField] private float sendRateHz = 30f;
        [SerializeField] private bool sendFace = true;
        [SerializeField] private bool sendHands = true;
        [SerializeField] private bool animateCameraWorldPose;
        [SerializeField] private Transform cameraWorldSource;
        [SerializeField] private Transform headPoseSource;

        private readonly TrackingFrameBuffer _frame = new TrackingFrameBuffer();

        private UdpTrackingSender _sender;
        private double _nextSendTime;

        public TrackingConnectionState State => _sender?.State ?? TrackingConnectionState.Stopped;
        public int LocalPort => _sender?.LocalDataPort ?? 0;
        public string RemoteEndpoint => _sender?.RemoteEndPoint?.ToString() ?? string.Empty;

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

            SyncSessionInfo();
            _sender.Update();

            double now = Time.unscaledTimeAsDouble;
            double sendInterval = 1d / Mathf.Max(1f, sendRateHz);
            if (now < _nextSendTime)
            {
                return;
            }

            _nextSendTime = now + sendInterval;
            PopulateFrame((float)now);
            _sender.SendFrame(_frame);
        }

        public void StartSending()
        {
            if (_sender != null)
            {
                return;
            }

            _sender = new UdpTrackingSender(preferredLocalPort: localDataPort);
            SyncSessionInfo();
            _sender.Start();
            _nextSendTime = Time.unscaledTimeAsDouble;
        }

        public void StopSending()
        {
            _sender?.Dispose();
            _sender = null;
        }

        private void OnValidate()
        {
            if (sendRateHz < 1f)
            {
                sendRateHz = 1f;
            }
        }

        private void SyncSessionInfo()
        {
            if (_sender == null)
            {
                return;
            }

            _sender.SessionInfo.ProtocolVersion = TrackingProtocol.ProtocolVersion;
            _sender.SessionInfo.DeviceName = string.IsNullOrWhiteSpace(deviceName) ? Application.productName : deviceName;
            _sender.SessionInfo.AppVersion = string.IsNullOrWhiteSpace(appVersion) ? Application.unityVersion : appVersion;
            _sender.SessionInfo.NominalFps = Mathf.Max(1f, sendRateHz);
            _sender.SessionInfo.AvailableFeatures = BuildFeatureFlags();
        }

        private void PopulateFrame(float time)
        {
            _frame.Clear();
            _frame.Features = BuildFeatureFlags();
            _frame.CameraWorldPose = ResolveCameraWorldPose(time);
            _frame.HeadPoseCameraSpace = ResolveHeadPose(time);

            if ((_frame.Features & TrackingFeatureFlags.Face) != 0)
            {
                PopulateFace(time);
            }

            if ((_frame.Features & TrackingFeatureFlags.LeftHand) != 0)
            {
                PopulateHand(time, true, _frame.LeftHand);
            }

            if ((_frame.Features & TrackingFeatureFlags.RightHand) != 0)
            {
                PopulateHand(time, false, _frame.RightHand);
            }
        }

        private TrackingFeatureFlags BuildFeatureFlags()
        {
            TrackingFeatureFlags features = TrackingFeatureFlags.CameraWorldPose | TrackingFeatureFlags.HeadPose;
            if (sendFace)
            {
                features |= TrackingFeatureFlags.Face;
            }

            if (sendHands)
            {
                features |= TrackingFeatureFlags.LeftHand | TrackingFeatureFlags.RightHand;
            }

            return features;
        }

        private TrackingPose ResolveCameraWorldPose(float time)
        {
            if (cameraWorldSource != null)
            {
                return ToTrackingPose(cameraWorldSource.position, cameraWorldSource.rotation);
            }

            if (!animateCameraWorldPose)
            {
                return TrackingPose.Identity;
            }

            Vector3 position = new Vector3(
                Mathf.Sin(time * 0.37f) * 0.2f,
                Mathf.Cos(time * 0.23f) * 0.04f,
                Mathf.Sin(time * 0.19f) * 0.08f);
            Quaternion rotation = Quaternion.Euler(
                Mathf.Sin(time * 0.15f) * 4f,
                Mathf.Sin(time * 0.31f) * 10f,
                0f);
            return ToTrackingPose(position, rotation);
        }

        private TrackingPose ResolveHeadPose(float time)
        {
            if (headPoseSource != null)
            {
                if (cameraWorldSource != null)
                {
                    Vector3 localPosition = cameraWorldSource.InverseTransformPoint(headPoseSource.position);
                    Quaternion localRotation = Quaternion.Inverse(cameraWorldSource.rotation) * headPoseSource.rotation;
                    return ToTrackingPose(localPosition, localRotation);
                }

                return ToTrackingPose(headPoseSource.localPosition, headPoseSource.localRotation);
            }

            Vector3 position = new Vector3(
                Mathf.Sin(time * 0.85f) * 0.025f,
                0.015f + Mathf.Sin(time * 0.57f) * 0.012f,
                DefaultHeadDepth + Mathf.Sin(time * 0.43f) * 0.01f);
            Quaternion rotation = Quaternion.Euler(
                Mathf.Sin(time * 0.91f) * 8f,
                Mathf.Sin(time * 0.61f) * 18f,
                Mathf.Sin(time * 0.74f) * 5f);
            return ToTrackingPose(position, rotation);
        }

        private void PopulateFace(float time)
        {
            SetBlendshape(Star67.FaceBlendshapeLocation.JawOpen, 0.1f + Oscillate01(time * 1.4f) * 0.35f);
            SetBlendshape(Star67.FaceBlendshapeLocation.MouthSmileLeft, 0.15f + Oscillate01(time * 0.9f) * 0.35f);
            SetBlendshape(Star67.FaceBlendshapeLocation.MouthSmileRight, 0.15f + Oscillate01(time * 0.9f + 0.45f) * 0.35f);
            SetBlendshape(Star67.FaceBlendshapeLocation.MouthFunnel, Oscillate01(time * 0.7f + 1.1f) * 0.18f);
            SetBlendshape(Star67.FaceBlendshapeLocation.BrowInnerUp, Oscillate01(time * 0.5f + 0.7f) * 0.25f);
            SetBlendshape(Star67.FaceBlendshapeLocation.CheekPuff, Oscillate01(time * 0.65f + 2.1f) * 0.2f);
            SetBlendshape(Star67.FaceBlendshapeLocation.EyeBlinkLeft, BlinkPulse(time * 1.7f));
            SetBlendshape(Star67.FaceBlendshapeLocation.EyeBlinkRight, BlinkPulse(time * 1.7f + 0.35f));
        }

        private void PopulateHand(float time, bool isLeft, TrackedHandData hand)
        {
            hand.Clear();
            hand.IsTracked = true;
            hand.Confidence = 1f;

            float side = isLeft ? -1f : 1f;
            float phase = isLeft ? 0f : 0.8f;
            Vector3 wrist = new Vector3(
                side * (0.18f + Mathf.Sin(time * 0.42f + phase) * 0.025f),
                -0.15f + Mathf.Sin(time * 0.71f + phase) * 0.015f,
                0.42f + Mathf.Sin(time * 0.37f + phase) * 0.02f);

            hand.SetJointPosition(HandJointId.Wrist, wrist.x, wrist.y, wrist.z);

            float curl = 0.2f + Oscillate01(time * 1.3f + phase) * 0.35f;
            PopulateThumb(hand, wrist, side, curl);
            PopulateFinger(hand, HandJointId.IndexMcp, HandJointId.IndexPip, HandJointId.IndexDip, HandJointId.IndexTip, wrist, side, 0.018f, curl * 0.8f);
            PopulateFinger(hand, HandJointId.MiddleMcp, HandJointId.MiddlePip, HandJointId.MiddleDip, HandJointId.MiddleTip, wrist, side, 0.005f, curl * 0.9f);
            PopulateFinger(hand, HandJointId.RingMcp, HandJointId.RingPip, HandJointId.RingDip, HandJointId.RingTip, wrist, side, -0.009f, curl);
            PopulateFinger(hand, HandJointId.PinkyMcp, HandJointId.PinkyPip, HandJointId.PinkyDip, HandJointId.PinkyTip, wrist, side, -0.022f, curl * 1.1f);
        }

        private void PopulateThumb(TrackedHandData hand, Vector3 wrist, float side, float curl)
        {
            Vector3 cmc = wrist + new Vector3(side * 0.018f, 0.008f, 0f);
            Vector3 mcp = cmc + new Vector3(side * 0.02f, 0.014f, 0.01f + curl * 0.005f);
            Vector3 ip = mcp + new Vector3(side * 0.018f, 0.012f, 0.012f + curl * 0.008f);
            Vector3 tip = ip + new Vector3(side * 0.016f, 0.009f, 0.018f + curl * 0.01f);

            hand.SetJointPosition(HandJointId.ThumbCmc, cmc.x, cmc.y, cmc.z);
            hand.SetJointPosition(HandJointId.ThumbMcp, mcp.x, mcp.y, mcp.z);
            hand.SetJointPosition(HandJointId.ThumbIp, ip.x, ip.y, ip.z);
            hand.SetJointPosition(HandJointId.ThumbTip, tip.x, tip.y, tip.z);
        }

        private static void PopulateFinger(
            TrackedHandData hand,
            HandJointId mcpId,
            HandJointId pipId,
            HandJointId dipId,
            HandJointId tipId,
            Vector3 wrist,
            float side,
            float lateralOffset,
            float curl)
        {
            Vector3 mcp = wrist + new Vector3(side * lateralOffset, 0.038f, 0.002f);
            Vector3 pip = mcp + new Vector3(side * lateralOffset * 0.15f, 0.03f, 0.008f + curl * 0.012f);
            Vector3 dip = pip + new Vector3(side * lateralOffset * 0.1f, 0.02f, 0.01f + curl * 0.018f);
            Vector3 tip = dip + new Vector3(side * lateralOffset * 0.08f, 0.018f, 0.012f + curl * 0.024f);

            hand.SetJointPosition(mcpId, mcp.x, mcp.y, mcp.z);
            hand.SetJointPosition(pipId, pip.x, pip.y, pip.z);
            hand.SetJointPosition(dipId, dip.x, dip.y, dip.z);
            hand.SetJointPosition(tipId, tip.x, tip.y, tip.z);
        }

        private void SetBlendshape(Star67.FaceBlendshapeLocation location, float weight)
        {
            _frame.FaceBlendshapes[(int)location].weight = Mathf.Clamp01(weight);
        }

        private static float Oscillate01(float value)
        {
            return Mathf.Sin(value) * 0.5f + 0.5f;
        }

        private static float BlinkPulse(float value)
        {
            float pulse = Mathf.Sin(value);
            if (pulse <= 0.75f)
            {
                return 0f;
            }

            return Mathf.Clamp01((pulse - 0.75f) / 0.25f);
        }

        private static TrackingPose ToTrackingPose(Vector3 position, Quaternion rotation)
        {
            return TrackingPose.FromPositionAndRotation(
                position.x,
                position.y,
                position.z,
                rotation.x,
                rotation.y,
                rotation.z,
                rotation.w);
        }
    }
}
