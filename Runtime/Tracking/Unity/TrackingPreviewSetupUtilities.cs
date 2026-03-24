using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Star67.Tracking.Unity
{
    public static class TrackingPreviewSetupUtilities
    {
        public const string UserTrackingServiceObjectName = "UserTrackingService";
        public const string TargetsRootName = "Tracking Preview Targets";
        public const string CameraWorldTargetName = "CameraWorld";
        public const string HeadWorldTargetName = "HeadWorld";
        public const string LeftWristTargetName = "LeftWrist";
        public const string RightWristTargetName = "RightWrist";
        public const string LeftHandRootName = "LeftHand";
        public const string RightHandRootName = "RightHand";

        public static UserTrackingService EnsureSceneUserTrackingService(Scene scene)
        {
            UserTrackingService service = FindSceneUserTrackingService(scene);
            if (service != null)
            {
                return EnsureUserTrackingService(service.gameObject);
            }

            var serviceObject = new GameObject(UserTrackingServiceObjectName);
            if (scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(serviceObject, scene);
            }

            return EnsureUserTrackingService(serviceObject);
        }

        public static UserTrackingService FindSceneUserTrackingService(Scene scene)
        {
            UserTrackingService[] services = UnityEngine.Object.FindObjectsByType<UserTrackingService>(FindObjectsSortMode.None);
            if (!scene.IsValid())
            {
                return services.Length > 0 ? services[0] : null;
            }

            for (int i = 0; i < services.Length; i++)
            {
                UserTrackingService service = services[i];
                if (service != null && service.gameObject.scene == scene)
                {
                    return service;
                }
            }

            return null;
        }

        public static UserTrackingService EnsureUserTrackingService(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            UserTrackingService service = root.GetComponent<UserTrackingService>();
            if (service == null)
            {
                service = root.AddComponent<UserTrackingService>();
            }

            TrackingTargetRig rig = root.GetComponent<TrackingTargetRig>();
            if (rig == null)
            {
                rig = root.AddComponent<TrackingTargetRig>();
            }

            TrackingTargetRigDriver rigDriver = root.GetComponent<TrackingTargetRigDriver>();
            if (rigDriver == null)
            {
                rigDriver = root.AddComponent<TrackingTargetRigDriver>();
            }

            TrackingPreviewController controller = root.GetComponent<TrackingPreviewController>();
            if (controller == null)
            {
                controller = root.AddComponent<TrackingPreviewController>();
            }

            ConfigureTrackingTargetRig(root.transform, rig, EnsureChildRuntime);
            ConfigureUserTrackingService(service, rig, rigDriver, controller);
            return service;
        }

        public static void EnsurePreviewComponents(
            GameObject owner,
            out TrackingTargetRig rig,
            out TrackingTargetRigDriver rigDriver,
            out TrackingPreviewController controller)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            rig = owner.GetComponent<TrackingTargetRig>();
            if (rig == null)
            {
                rig = owner.AddComponent<TrackingTargetRig>();
            }

            rigDriver = owner.GetComponent<TrackingTargetRigDriver>();
            if (rigDriver == null)
            {
                rigDriver = owner.AddComponent<TrackingTargetRigDriver>();
            }

            controller = owner.GetComponent<TrackingPreviewController>();
            if (controller == null)
            {
                controller = owner.AddComponent<TrackingPreviewController>();
            }

            ConfigureTrackingTargetRig(owner.transform, rig, EnsureChildRuntime);
            rigDriver.Rig = rig;
            controller.AutoFindAppliers = false;
        }

        public static TrackingPreviewController EnsurePreviewController(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            EnsurePreviewComponents(root, out _, out _, out TrackingPreviewController controller);
            return controller;
        }

        public static Star67AvatarFaceBlendshapeDriver EnsureAvatarFaceDriver(GameObject owner, IAvatar avatar = null)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            Star67AvatarFaceBlendshapeDriver faceDriver = owner.GetComponent<Star67AvatarFaceBlendshapeDriver>();
            if (faceDriver == null)
            {
                faceDriver = owner.AddComponent<Star67AvatarFaceBlendshapeDriver>();
            }

            return ConfigureAvatarFaceDriver(faceDriver, avatar);
        }

        public static Star67AvatarFaceBlendshapeDriver ConfigureAvatarFaceDriver(
            Star67AvatarFaceBlendshapeDriver faceDriver,
            IAvatar avatar = null)
        {
            if (faceDriver == null)
            {
                throw new ArgumentNullException(nameof(faceDriver));
            }

            if (avatar == null)
            {
                faceDriver.ClearBinding();
                return faceDriver;
            }

            faceDriver.BindAvatar(avatar);
            return faceDriver;
        }

        public static Star67AvatarFaceBlendshapeDriver BindAvatar(UserTrackingService service, IAvatar avatar)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (avatar?.Rig?.Root == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            EnsureUserTrackingService(service.gameObject);
            Star67AvatarFaceBlendshapeDriver faceDriver = EnsureAvatarFaceDriver(avatar.Rig.Root.gameObject, avatar);
            service.BindAvatar(avatar.Rig.Root, faceDriver);
            return faceDriver;
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

        public static UserTrackingService ConfigureUserTrackingService(
            UserTrackingService service,
            TrackingTargetRig rig,
            TrackingTargetRigDriver rigDriver,
            TrackingPreviewController controller)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (rig == null)
            {
                throw new ArgumentNullException(nameof(rig));
            }

            if (rigDriver == null)
            {
                throw new ArgumentNullException(nameof(rigDriver));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            rigDriver.Rig = rig;
            service.ConfigureOwnedComponents(rig, rigDriver, controller);
            return service;
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
