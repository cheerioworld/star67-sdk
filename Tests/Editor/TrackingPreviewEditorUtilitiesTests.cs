using NUnit.Framework;
using Star67.Tracking.Editor;
using Star67.Tracking.Unity;
using UnityEngine;

namespace Star67.Sdk.Editor.Tests
{
    public class TrackingPreviewEditorUtilitiesTests
    {
        private const int ExpectedHandJointCount = 21;
        private GameObject _avatarRoot;

        [TearDown]
        public void TearDown()
        {
            if (_avatarRoot != null)
            {
                Object.DestroyImmediate(_avatarRoot);
            }
        }

        [Test]
        public void EnsureStar67PreviewSetup_AddsPreviewComponentsAndTargets()
        {
            _avatarRoot = new GameObject("AvatarRoot");
            TrackingPreviewEditorUtilities.EnsureStar67PreviewSetup(_avatarRoot);
            TrackingPreviewController controller = _avatarRoot.GetComponent<TrackingPreviewController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(_avatarRoot.GetComponent<TrackingTargetRig>(), Is.Not.Null);
            Assert.That(_avatarRoot.GetComponent<TrackingTargetRigDriver>(), Is.Not.Null);
            Assert.That(_avatarRoot.GetComponent<Star67AvatarFaceBlendshapeDriver>(), Is.Not.Null);

            TrackingTargetRig rig = _avatarRoot.GetComponent<TrackingTargetRig>();
            Assert.That(rig.CameraWorldTarget, Is.Not.Null);
            Assert.That(rig.HeadWorldTarget, Is.Not.Null);
            Assert.That(rig.LeftHandJointTargets.Length, Is.EqualTo(ExpectedHandJointCount));
            Assert.That(rig.RightHandJointTargets.Length, Is.EqualTo(ExpectedHandJointCount));
            Assert.That(rig.LeftHandJointTargets[0], Is.Not.Null);
        }
    }
}
