using NUnit.Framework;
using Star67.Tracking.Unity;
using UnityEngine;

namespace Star67.Sdk.Tests
{
    public class TrackingTargetRigDriverTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void ApplyFrame_WritesWorldTargets()
        {
            _root = new GameObject("TrackingRoot");
            TrackingTargetRig rig = _root.AddComponent<TrackingTargetRig>();
            TrackingTargetRigDriver driver = _root.AddComponent<TrackingTargetRigDriver>();
            driver.Rig = rig;

            rig.CameraWorldTarget = new GameObject("Camera").transform;
            rig.CameraWorldTarget.SetParent(_root.transform, false);
            rig.HeadWorldTarget = new GameObject("Head").transform;
            rig.HeadWorldTarget.SetParent(_root.transform, false);

            var frame = new Tracking.TrackingFrameBuffer
            {
                Features = Tracking.TrackingFeatureFlags.CameraWorldPose | Tracking.TrackingFeatureFlags.HeadPose
            };
            frame.CameraWorldPose = Tracking.TrackingPose.FromPositionAndRotation(1f, 0f, 0f, 0f, 0f, 0f, 1f);
            frame.HeadPoseCameraSpace = Tracking.TrackingPose.FromPositionAndRotation(0f, 2f, 0f, 0f, 0f, 0f, 1f);

            driver.ApplyFrame(frame);

            Assert.That(rig.CameraWorldTarget.position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(rig.HeadWorldTarget.position, Is.EqualTo(new Vector3(1f, 2f, 0f)));
            Assert.That(rig.State.CameraTracked, Is.True);
            Assert.That(rig.State.HeadTracked, Is.True);
        }
    }
}
