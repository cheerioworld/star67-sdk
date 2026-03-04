using NUnit.Framework;

namespace Star67.Sdk.Tests
{
    public class TrackingPacketCodecTests
    {
        [Test]
        public void FramePacket_RoundTripsFaceCameraHeadAndHands()
        {
            var frame = new Tracking.TrackingFrameBuffer
            {
                SessionToken = 1234u,
                Sequence = 77ul,
                CaptureTimestampUs = 1234567,
                Features = Tracking.TrackingFeatureFlags.Face | Tracking.TrackingFeatureFlags.CameraWorldPose | Tracking.TrackingFeatureFlags.HeadPose | Tracking.TrackingFeatureFlags.LeftHand
            };

            frame.CameraWorldPose = Tracking.TrackingPose.FromPositionAndEuler(1f, 2f, 3f, 0.1f, 0.2f, 0.3f);
            frame.HeadPoseCameraSpace = Tracking.TrackingPose.FromPositionAndEuler(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f);
            frame.FaceBlendshapes[(int)FaceBlendshapeLocation.JawOpen].weight = 0.75f;
            frame.LeftHand.IsTracked = true;
            frame.LeftHand.Confidence = 0.9f;
            frame.LeftHand.SetJointPosition(0, 0.5f, 0.6f, 0.7f);
            frame.LeftHand.SetJointPosition(1, 0.8f, 0.9f, 1f);

            byte[] buffer = new byte[Tracking.TrackingProtocol.MaxPacketSize];
            Assert.That(Tracking.TrackingPacketCodec.TryWriteFramePacket(frame, buffer, out int written), Is.True);

            var decoded = new Tracking.TrackingFrameBuffer();
            Assert.That(Tracking.TrackingPacketCodec.TryReadFramePacket(new System.ReadOnlySpan<byte>(buffer, 0, written), decoded), Is.True);

            Assert.That(decoded.SessionToken, Is.EqualTo(frame.SessionToken));
            Assert.That(decoded.Sequence, Is.EqualTo(frame.Sequence));
            Assert.That(decoded.CaptureTimestampUs, Is.EqualTo(frame.CaptureTimestampUs));
            Assert.That(decoded.Features, Is.EqualTo(frame.Features));
            Assert.That(decoded.FaceBlendshapes[(int)FaceBlendshapeLocation.JawOpen].weight, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(decoded.LeftHand.IsTracked, Is.True);
            Assert.That(decoded.LeftHand.Confidence, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(decoded.LeftHand.GetJointPositionValue(0).X, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void FramePacket_AllowsMissingHands()
        {
            var frame = new Tracking.TrackingFrameBuffer
            {
                SessionToken = 99u,
                Sequence = 10ul,
                CaptureTimestampUs = 2000,
                Features = Tracking.TrackingFeatureFlags.Face | Tracking.TrackingFeatureFlags.CameraWorldPose | Tracking.TrackingFeatureFlags.HeadPose
            };

            byte[] buffer = new byte[Tracking.TrackingProtocol.MaxPacketSize];
            Assert.That(Tracking.TrackingPacketCodec.TryWriteFramePacket(frame, buffer, out int written), Is.True);

            var decoded = new Tracking.TrackingFrameBuffer();
            Assert.That(Tracking.TrackingPacketCodec.TryReadFramePacket(new System.ReadOnlySpan<byte>(buffer, 0, written), decoded), Is.True);
            Assert.That(decoded.LeftHand.IsTracked, Is.False);
            Assert.That(decoded.RightHand.IsTracked, Is.False);
        }
    }
}
