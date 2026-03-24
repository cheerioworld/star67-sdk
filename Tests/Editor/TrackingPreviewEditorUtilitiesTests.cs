using NUnit.Framework;
using Star67.Tracking.Editor;
using Star67.Tracking.Unity;
using UnityEngine;

namespace Star67.Sdk.Editor.Tests
{
    public class TrackingPreviewEditorUtilitiesTests
    {
        private const int ExpectedHandJointCount = 21;
        private GameObject _previewRoot;

        [TearDown]
        public void TearDown()
        {
            if (_previewRoot != null)
            {
                Object.DestroyImmediate(_previewRoot);
            }

            EditorPreviewManager existingRoot = Object.FindAnyObjectByType<EditorPreviewManager>();
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot.gameObject);
            }
        }

        [Test]
        public void EnsurePreviewController_AddsPreviewComponentsAndTargetsToOwner()
        {
            _previewRoot = new GameObject("PreviewRoot");
            TrackingPreviewController controller = TrackingPreviewSetupUtilities.EnsurePreviewController(_previewRoot);

            Assert.That(controller, Is.Not.Null);
            Assert.That(_previewRoot.GetComponent<TrackingTargetRig>(), Is.Not.Null);
            Assert.That(_previewRoot.GetComponent<TrackingTargetRigDriver>(), Is.Not.Null);
            Assert.That(_previewRoot.GetComponent<Star67AvatarFaceBlendshapeDriver>(), Is.Not.Null);

            TrackingTargetRig rig = _previewRoot.GetComponent<TrackingTargetRig>();
            Assert.That(rig.CameraWorldTarget, Is.Not.Null);
            Assert.That(rig.HeadWorldTarget, Is.Not.Null);
            Assert.That(rig.LeftHandJointTargets.Length, Is.EqualTo(ExpectedHandJointCount));
            Assert.That(rig.RightHandJointTargets.Length, Is.EqualTo(ExpectedHandJointCount));
            Assert.That(rig.LeftHandJointTargets[0], Is.Not.Null);
            Assert.That(controller.AutoFindAppliers, Is.False);
        }

        [Test]
        public void EnsurePreviewController_IsIdempotent()
        {
            _previewRoot = new GameObject("PreviewRoot");

            TrackingPreviewSetupUtilities.EnsurePreviewController(_previewRoot);
            TrackingPreviewSetupUtilities.EnsurePreviewController(_previewRoot);

            Assert.That(_previewRoot.GetComponents<TrackingPreviewController>().Length, Is.EqualTo(1));
            Assert.That(_previewRoot.GetComponents<TrackingTargetRig>().Length, Is.EqualTo(1));
            Assert.That(_previewRoot.GetComponents<TrackingTargetRigDriver>().Length, Is.EqualTo(1));
            Assert.That(_previewRoot.GetComponents<Star67AvatarFaceBlendshapeDriver>().Length, Is.EqualTo(1));
            Assert.That(_previewRoot.transform.Find(TrackingPreviewSetupUtilities.TargetsRootName), Is.Not.Null);
        }

        [Test]
        public void EnsureStar67PreviewSetup_DoesNotCreatePreviewRootOutsidePlayMode()
        {
            _previewRoot = new GameObject("AvatarRoot");

            EditorPreviewManager manager = TrackingPreviewEditorUtilities.EnsureStar67PreviewSetup(_previewRoot);

            Assert.That(manager, Is.Null);
            Assert.That(Object.FindAnyObjectByType<EditorPreviewManager>(), Is.Null);
        }
    }
}
