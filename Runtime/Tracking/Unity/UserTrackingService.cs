using UnityEngine;
using Star67.Avatar.Calibration;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class UserTrackingService : MonoBehaviour
    {
        [SerializeField] private TrackingTargetRig rig;
        [SerializeField] private TrackingTargetRigDriver rigDriver;
        [SerializeField] private TrackingPreviewController previewController;
        [System.NonSerialized] private Transform currentAvatarRoot;
        [System.NonSerialized] private Star67AvatarFaceBlendshapeDriver currentFaceDriver;
        [System.NonSerialized] private AvatarCalibrationRuntime currentCalibrationRuntime;

        public TrackingTargetRig Rig => rig;
        public TrackingTargetRigDriver RigDriver => rigDriver;
        public TrackingPreviewController PreviewController => previewController;
        public Transform CurrentAvatarRoot => currentAvatarRoot;
        public Star67AvatarFaceBlendshapeDriver CurrentFaceDriver => currentFaceDriver;
        public AvatarCalibrationRuntime CurrentCalibrationRuntime
        {
            get
            {
                if (currentAvatarRoot == null)
                {
                    currentCalibrationRuntime = null;
                    return null;
                }

                if (currentCalibrationRuntime == null || currentCalibrationRuntime.transform != currentAvatarRoot)
                {
                    currentCalibrationRuntime = currentAvatarRoot.GetComponent<AvatarCalibrationRuntime>();
                }

                return currentCalibrationRuntime;
            }
        }

        private void Awake()
        {
            TrackingPreviewSetupUtilities.EnsureUserTrackingService(gameObject);
        }

        public void BindAvatar(Transform avatarRoot, Star67AvatarFaceBlendshapeDriver faceDriver)
        {
            currentAvatarRoot = avatarRoot;
            currentFaceDriver = faceDriver;
            currentCalibrationRuntime = avatarRoot != null ? avatarRoot.GetComponent<AvatarCalibrationRuntime>() : null;
            RefreshPreviewAppliers();
        }

        public void ClearAvatarBinding(Transform avatarRoot = null)
        {
            if (avatarRoot != null && currentAvatarRoot != avatarRoot)
            {
                return;
            }

            currentAvatarRoot = null;
            currentFaceDriver = null;
            currentCalibrationRuntime = null;
            RefreshPreviewAppliers();
        }

        internal void ConfigureOwnedComponents(
            TrackingTargetRig configuredRig,
            TrackingTargetRigDriver configuredRigDriver,
            TrackingPreviewController configuredPreviewController)
        {
            rig = configuredRig;
            rigDriver = configuredRigDriver;
            previewController = configuredPreviewController;
            RefreshPreviewAppliers();
        }

        internal void RefreshPreviewAppliers()
        {
            if (previewController == null)
            {
                return;
            }

            int count = rigDriver != null ? 1 : 0;
            if (currentFaceDriver != null)
            {
                count++;
            }

            var appliers = new MonoBehaviour[count];
            int index = 0;
            if (rigDriver != null)
            {
                appliers[index++] = rigDriver;
            }

            if (currentFaceDriver != null)
            {
                appliers[index] = currentFaceDriver;
            }

            previewController.AutoFindAppliers = false;
            previewController.ApplierBehaviours = appliers;
            previewController.RefreshAppliers();
        }
    }
}
