using System;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    public static class TrackingPreviewSetupUtilities
    {
        public const string TargetsRootName = "Tracking Preview Targets";
        public const string CameraWorldTargetName = "CameraWorld";
        public const string HeadWorldTargetName = "HeadWorld";
        public const string LeftWristTargetName = "LeftWrist";
        public const string RightWristTargetName = "RightWrist";
        public const string LeftHandRootName = "LeftHand";
        public const string RightHandRootName = "RightHand";

        public static TrackingTargetRig EnsureTrackingTargetRig(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            TrackingTargetRig rig = root.GetComponent<TrackingTargetRig>();
            if (rig == null)
            {
                rig = root.AddComponent<TrackingTargetRig>();
            }

            return ConfigureTrackingTargetRig(root.transform, rig, EnsureChildRuntime);
        }

        public static TrackingPreviewController EnsurePreviewController(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            TrackingTargetRig rig = EnsureTrackingTargetRig(root);
            TrackingPreviewController controller = root.GetComponent<TrackingPreviewController>();
            if (controller == null)
            {
                controller = root.AddComponent<TrackingPreviewController>();
            }

            TrackingTargetRigDriver rigDriver = root.GetComponent<TrackingTargetRigDriver>();
            if (rigDriver == null)
            {
                rigDriver = root.AddComponent<TrackingTargetRigDriver>();
            }

            Star67AvatarFaceBlendshapeDriver faceDriver = root.GetComponent<Star67AvatarFaceBlendshapeDriver>();
            if (faceDriver == null)
            {
                faceDriver = root.AddComponent<Star67AvatarFaceBlendshapeDriver>();
            }

            return ConfigurePreviewController(root.transform, rig, controller, rigDriver, faceDriver);
        }

        public static TrackingTargetRig ConfigureTrackingTargetRig(
            Transform root,
            TrackingTargetRig rig,
            Func<Transform, string, Transform> ensureChild)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (rig == null)
            {
                throw new ArgumentNullException(nameof(rig));
            }

            if (ensureChild == null)
            {
                throw new ArgumentNullException(nameof(ensureChild));
            }

            Transform targetsRoot = ensureChild(root, TargetsRootName);
            rig.CameraWorldTarget = ensureChild(targetsRoot, CameraWorldTargetName);
            rig.HeadWorldTarget = ensureChild(targetsRoot, HeadWorldTargetName);
            rig.LeftWristTarget = ensureChild(targetsRoot, LeftWristTargetName);
            rig.RightWristTarget = ensureChild(targetsRoot, RightWristTargetName);

            Transform leftHandRoot = ensureChild(targetsRoot, LeftHandRootName);
            Transform rightHandRoot = ensureChild(targetsRoot, RightHandRootName);

            Transform[] leftHandJointTargets = rig.LeftHandJointTargets;
            Transform[] rightHandJointTargets = rig.RightHandJointTargets;
            for (int i = 0; i < TrackingProtocol.HandJointCount; i++)
            {
                string jointName = ((HandJointId)i).ToString();
                leftHandJointTargets[i] = ensureChild(leftHandRoot, jointName);
                rightHandJointTargets[i] = ensureChild(rightHandRoot, jointName);
            }

            return rig;
        }

        public static TrackingPreviewController ConfigurePreviewController(
            Transform root,
            TrackingTargetRig rig,
            TrackingPreviewController controller,
            TrackingTargetRigDriver rigDriver,
            Star67AvatarFaceBlendshapeDriver faceDriver)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (rig == null)
            {
                throw new ArgumentNullException(nameof(rig));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (rigDriver == null)
            {
                throw new ArgumentNullException(nameof(rigDriver));
            }

            if (faceDriver == null)
            {
                throw new ArgumentNullException(nameof(faceDriver));
            }

            rigDriver.Rig = rig;
            faceDriver.Root = root;
            controller.AutoFindAppliers = true;
            controller.RefreshAppliers();
            return controller;
        }

        private static Transform EnsureChildRuntime(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
