using System;
using Star67.Tracking.Unity;
using UnityEditor;
using UnityEngine;

namespace Star67.Tracking.Editor
{
    public static class TrackingPreviewEditorUtilities
    {
        public static void EnsureStar67PreviewSetup(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            TrackingPreviewController controller = root.GetComponent<TrackingPreviewController>() ?? Undo.AddComponent<TrackingPreviewController>(root);
            TrackingTargetRig rig = root.GetComponent<TrackingTargetRig>() ?? Undo.AddComponent<TrackingTargetRig>(root);
            TrackingTargetRigDriver rigDriver = root.GetComponent<TrackingTargetRigDriver>() ?? Undo.AddComponent<TrackingTargetRigDriver>(root);
            Star67AvatarFaceBlendshapeDriver faceDriver = root.GetComponent<Star67AvatarFaceBlendshapeDriver>() ?? Undo.AddComponent<Star67AvatarFaceBlendshapeDriver>(root);

            Transform targetsRoot = EnsureChild(root.transform, "Tracking Preview Targets");
            rig.CameraWorldTarget = EnsureChild(targetsRoot, "CameraWorld");
            rig.HeadWorldTarget = EnsureChild(targetsRoot, "HeadWorld");
            rig.LeftWristTarget = EnsureChild(targetsRoot, "LeftWrist");
            rig.RightWristTarget = EnsureChild(targetsRoot, "RightWrist");

            Transform leftHandRoot = EnsureChild(targetsRoot, "LeftHand");
            Transform rightHandRoot = EnsureChild(targetsRoot, "RightHand");
            for (int i = 0; i < TrackingProtocol.HandJointCount; i++)
            {
                rig.LeftHandJointTargets[i] = EnsureChild(leftHandRoot, ((HandJointId)i).ToString());
                rig.RightHandJointTargets[i] = EnsureChild(rightHandRoot, ((HandJointId)i).ToString());
            }

            rigDriver.Rig = rig;
            faceDriver.Root = root.transform;
            controller.AutoFindAppliers = true;
            controller.RefreshAppliers();

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(rigDriver);
            EditorUtility.SetDirty(faceDriver);
            EditorUtility.SetDirty(controller);
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, $"Create {childName}");
            child.SetParent(parent, false);
            return child;
        }
    }
}
