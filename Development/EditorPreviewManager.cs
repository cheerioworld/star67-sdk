using System.Threading;
using Star67.Avatar;
using Star67.Avatar.Calibration;
using Star67.Sdk.Avatar;
using Star67.Sdk.Tracking.Editor;
using Star67.Tracking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class EditorPreviewManager : MonoBehaviour
    {
        public const string ObjectName = "EditorPreviewManager";

        [SerializeField] private TrackingTargetRig trackingTargetRig;
        [SerializeField] private TrackingTargetRigDriver trackingTargetRigDriver;
        [SerializeField] private TrackingPreviewController trackingPreviewController;
        // [SerializeField] private Star67AvatarFaceBlendshapeDriver faceBlendshapeDriver;
        [SerializeField] private string statusMessage;
        [SerializeField] private EditorTrackingPipeline trackingPipeline;

        [SerializeField] private new Camera camera;

        [System.NonSerialized] private IAvatar currentAvatar;
        [System.NonSerialized] private Transform currentAvatarRoot;
        [System.NonSerialized] private AvatarCalibrationService calibrationService;
        [System.NonSerialized] private Star67AvatarLocalLoader localAvatarLoader;

        public TrackingTargetRig TrackingTargetRig => trackingTargetRig;
        public TrackingTargetRigDriver TrackingTargetRigDriver => trackingTargetRigDriver;
        public TrackingPreviewController PreviewController => trackingPreviewController;
        // public Star67AvatarFaceBlendshapeDriver FaceBlendshapeDriver => faceBlendshapeDriver;
        public IAvatar CurrentAvatar => currentAvatar;
        public Transform CurrentAvatarRoot => currentAvatarRoot;
        public string StatusMessage => statusMessage;

        private Transform _camera;

        private void Awake()
        {
            _camera = Camera.main.transform;
            EnsureOwnedComponents();
            EnsureServices();
            camera = Camera.main;
            trackingPipeline = GetComponent<EditorTrackingPipeline>() ?? gameObject.AddComponent<EditorTrackingPipeline>();
        }

        private void OnDestroy()
        {
            ClearAvatarBinding();

            if (trackingPreviewController != null && trackingPreviewController.Source != null)
            {
                trackingPreviewController.SetSource(null);
            }
        }

        public void BindAvatar(IAvatar avatar)
        {
            Debug.Log("Binding avatar " + avatar);
            if (avatar?.Rig?.Root == null)
            {
                throw new System.ArgumentNullException(nameof(avatar));
            }

            EnsureOwnedComponents();
            EnsureServices();

            if (currentAvatarRoot != null && currentAvatarRoot != avatar.Rig.Root)
            {
                ClearAvatarBinding();
            }

            currentAvatar = avatar;
            currentAvatarRoot = avatar.Rig.Root;

            if (gameObject.scene.IsValid() && currentAvatarRoot.gameObject.scene.IsValid() && gameObject.scene != currentAvatarRoot.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(gameObject, currentAvatarRoot.gameObject.scene);
            }

            trackingPipeline.SetAvatar(avatar);
            // TrackingPreviewSetupUtilities.ConfigureAvatarFaceDriver(faceBlendshapeDriver, avatar);
        }

        public void ClearAvatarBinding()
        {
            if (trackingPipeline != null)
            {
                trackingPipeline.SetAvatar(null);
            }

            currentAvatar = null;
            currentAvatarRoot = null;
        }

        public void SetSource(ITrackingFrameSource source)
        {
            Debug.Log("Setting source");
            EnsureOwnedComponents();
            trackingPipeline.SetSource(source);
        }

        public void SetStatusMessage(string message)
        {
            statusMessage = message ?? string.Empty;
        }

        public static EditorPreviewManager FindActive()
        {
            return Object.FindAnyObjectByType<EditorPreviewManager>();
        }

        public static bool TryResolveOrCreateForPlayMode(out EditorPreviewManager manager, out string resolvedStatusMessage)
        {
            manager = FindActive();
            if (manager != null && manager.CurrentAvatarRoot != null)
            {
                resolvedStatusMessage = manager.StatusMessage;
                return true;
            }

            if (manager == null)
            {
                var rootObject = new GameObject(ObjectName);
                manager = rootObject.AddComponent<EditorPreviewManager>();
            }

            manager.EnsureOwnedComponents();
            manager.EnsureServices();

            IAvatar avatar;
            try
            {
                avatar = manager.localAvatarLoader.LoadAvatarAsync(
                    new AvatarDescriptor { Type = AvatarType.Basis },
                    parent: null,
                    ct: CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (System.Exception exception)
            {
                resolvedStatusMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "Failed to load a Star67Avatar in loaded scenes."
                    : exception.Message;
                if (manager != null)
                {
                    manager.ClearAvatarBinding();
                    Object.Destroy(manager.gameObject);
                    manager = null;
                }

                return false;
            }

            if (avatar?.Rig?.Root == null)
            {
                resolvedStatusMessage = "No Star67Avatar found in loaded scenes.";
                if (manager != null)
                {
                    manager.ClearAvatarBinding();
                    Object.Destroy(manager.gameObject);
                    manager = null;
                }

                return false;
            }
            manager.BindAvatar(avatar);

            string avatarName = avatar.Rig.Root.name;
            resolvedStatusMessage = $"Resolved Star67Avatar '{avatarName}'.";
            manager.SetStatusMessage(resolvedStatusMessage);
            Debug.Log($"EditorPreviewCompositionRoot: {resolvedStatusMessage}");
            return true;
        }

        private void EnsureOwnedComponents()
        {
            TrackingPreviewSetupUtilities.EnsurePreviewComponents(
                gameObject,
                out trackingTargetRig,
                out trackingTargetRigDriver,
                out trackingPreviewController);
        }

        private void EnsureServices()
        {
            if (calibrationService != null && localAvatarLoader != null)
            {
                return;
            }

            calibrationService = new AvatarCalibrationService(
                new IAvatarCalibrationStep[]
                {
                    new HumanoidReferencePoseCalibrationStep(),
                    new EyeHeightCalibrationStep()
                },
                new IAvatarCalibrationPoseGuard[]
                {
                    new FinalIkAvatarCalibrationPoseGuard()
                });
            localAvatarLoader = new Star67AvatarLocalLoader(
                new IAvatarLoaderPostprocessor[]
                {
                    new AvatarCalibrationPostprocessor(calibrationService),
                    // new VrikAvatarLoaderPostprocessor(trackingTargetRig)
                });
        }
    }
}
