using System.IO;
using NUnit.Framework;

namespace Star67.Sdk.Tests
{
    public class TrackingRecordingTests
    {
        [Test]
        public void RecordingWriterAndPlayer_RoundTripFramePacket()
        {
            string path = Path.Combine(Path.GetTempPath(), $"star67-{System.Guid.NewGuid():N}.s67trk");
            byte[] packetBuffer = new byte[Tracking.TrackingProtocol.MaxPacketSize];

            try
            {
                var frame = new Tracking.TrackingFrameBuffer
                {
                    SessionToken = 7u,
                    Sequence = 1ul,
                    CaptureTimestampUs = 5_000,
                    Features = Tracking.TrackingFeatureFlags.Face | Tracking.TrackingFeatureFlags.CameraWorldPose | Tracking.TrackingFeatureFlags.HeadPose
                };
                frame.FaceBlendshapes[(int)FaceBlendshapeLocation.MouthSmileLeft].weight = 0.42f;

                Assert.That(Tracking.TrackingPacketCodec.TryWriteFramePacket(frame, packetBuffer, out int packetLength), Is.True);

                using (var writer = new Tracking.TrackingRecordingWriter())
                {
                    writer.Start(path, new Tracking.TrackingRecordingHeader
                    {
                        SessionToken = frame.SessionToken,
                        SessionInfo = new Tracking.TrackingSessionInfo
                        {
                            DeviceName = "UnitTestDevice",
                            AppVersion = "1.0.0",
                            AvailableFeatures = frame.Features
                        }
                    });
                    writer.OnFramePacket(new System.ReadOnlySpan<byte>(packetBuffer, 0, packetLength), frame.CaptureTimestampUs);
                    writer.Stop();
                }

                using var player = new Tracking.TrackingRecordingPlayer(path);
                player.Seek(0f);
                var output = new Tracking.TrackingFrameBuffer();
                Assert.That(player.TryCopyLatestFrame(output), Is.True);
                Assert.That(output.FaceBlendshapes[(int)FaceBlendshapeLocation.MouthSmileLeft].weight, Is.EqualTo(0.42f).Within(0.0001f));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
