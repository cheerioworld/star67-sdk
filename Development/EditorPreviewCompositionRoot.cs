using System.Threading;
using Star67.Avatar;
using Star67.Avatar.Calibration;
using Star67.Sdk.Avatar;
using Star67.Tracking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class EditorPreviewCompositionRoot : MonoBehaviour
    {
        public const string ObjectName = "EditorPreviewCompositionRoot";

        [SerializeField] private TrackingTargetRig trackingTargetRig;
        [SerializeField] private TrackingTargetRigDriver trackingTargetRigDriver;
        [SerializeField] private TrackingPreviewController trackingPreviewController;
        [SerializeField] private Star67AvatarFaceBlendshapeDriver faceBlendshapeDriver;
        [SerializeField] private string statusMessage;

        [SerializeField] private Camera camera;

        [System.NonSerialized] private IAvatar currentAvatar;
        [System.NonSerialized] private Transform currentAvatarRoot;
        [System.NonSerialized] private AvatarCalibrationRuntime currentCalibrationRuntime;
        [System.NonSerialized] private AvatarCalibrationService calibrationService;
        [System.NonSerialized] private Star67AvatarLocalLoader localAvatarLoader;

        public TrackingTargetRig TrackingTargetRig => trackingTargetRig;
        public TrackingTargetRigDriver TrackingTargetRigDriver => trackingTargetRigDriver;
        public TrackingPreviewController PreviewController => trackingPreviewController;
        public Star67AvatarFaceBlendshapeDriver FaceBlendshapeDriver => faceBlendshapeDriver;
        public IAvatar CurrentAvatar => currentAvatar;
        public Transform CurrentAvatarRoot => currentAvatarRoot;
        public string StatusMessage => statusMessage;

        private Transform _camera;

        private void Awake()
        {
            _camera = Camera.main.transform;
            EnsureOwnedComponents();
            EnsureServices();
            calibrationService.AvatarCalibrated += OnAvatarCalibrated;
            camera = Camera.main;
        }

        void OnAvatarCalibrated(AvatarCalibrationState calibrationState)
        {
            Debug.Log("Calibration set on trackingtargetrigdriver " + calibrationState.EyeHeightMeters);
            trackingTargetRigDriver.SetAvatarHeight(calibrationState.EyeHeightMeters);
        }

        private void OnDestroy()
        {
            ClearAvatarBinding();
            // calibrationService.OnAvatarCalibrated -= OnAvatarCalibrated;

            if (trackingPreviewController != null && trackingPreviewController.Source != null)
            {
                trackingPreviewController.SetSource(null);
            }
        }

        public void BindAvatar(IAvatar avatar)
        {
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
            currentCalibrationRuntime = null;

            if (gameObject.scene.IsValid() && currentAvatarRoot.gameObject.scene.IsValid() && gameObject.scene != currentAvatarRoot.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(gameObject, currentAvatarRoot.gameObject.scene);
            }

            currentCalibrationRuntime = currentAvatarRoot.GetComponent<AvatarCalibrationRuntime>();
            TrackingPreviewSetupUtilities.ConfigureAvatarFaceDriver(faceBlendshapeDriver, avatar);
        }

        public void ClearAvatarBinding()
        {
            if (faceBlendshapeDriver != null)
            {
                faceBlendshapeDriver.ClearBinding();
            }

            currentAvatar = null;
            currentAvatarRoot = null;
            currentCalibrationRuntime = null;
        }

        public void SetSource(ITrackingFrameSource source)
        {
            EnsureOwnedComponents();
            trackingPreviewController.SetSource(source);
        }

        public void SetStatusMessage(string message)
        {
            statusMessage = message ?? string.Empty;
        }

        public static EditorPreviewCompositionRoot FindActive()
        {
            return Object.FindAnyObjectByType<EditorPreviewCompositionRoot>();
        }

        public static bool TryResolveOrCreateForPlayMode(out EditorPreviewCompositionRoot compositionRoot, out string resolvedStatusMessage)
        {
            compositionRoot = FindActive();
            if (compositionRoot != null && compositionRoot.CurrentAvatarRoot != null)
            {
                resolvedStatusMessage = compositionRoot.StatusMessage;
                return true;
            }

            if (compositionRoot == null)
            {
                var rootObject = new GameObject(ObjectName);
                compositionRoot = rootObject.AddComponent<EditorPreviewCompositionRoot>();
            }

            compositionRoot.EnsureOwnedComponents();
            compositionRoot.EnsureServices();

            IAvatar avatar;
            try
            {
                avatar = compositionRoot.localAvatarLoader.LoadAvatarAsync(
                    new AvatarDescriptor { Type = AvatarType.Basis },
                    parent: null,
                    ct: CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (System.Exception exception)
            {
                resolvedStatusMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "Failed to load a Star67Avatar in loaded scenes."
                    : exception.Message;
                if (compositionRoot != null)
                {
                    compositionRoot.ClearAvatarBinding();
                    Object.Destroy(compositionRoot.gameObject);
                    compositionRoot = null;
                }

                return false;
            }

            if (avatar?.Rig?.Root == null)
            {
                resolvedStatusMessage = "No Star67Avatar found in loaded scenes.";
                if (compositionRoot != null)
                {
                    compositionRoot.ClearAvatarBinding();
                    Object.Destroy(compositionRoot.gameObject);
                    compositionRoot = null;
                }

                return false;
            }
            compositionRoot.BindAvatar(avatar);

            string avatarName = avatar is SceneStar67AvatarAdapter sceneAvatar
                ? sceneAvatar.AvatarName
                : avatar.Rig.Root.name;
            resolvedStatusMessage = $"Resolved Star67Avatar '{avatarName}'.";
            compositionRoot.SetStatusMessage(resolvedStatusMessage);
            Debug.Log($"EditorPreviewCompositionRoot: {resolvedStatusMessage}");
            return true;
        }

        private void EnsureOwnedComponents()
        {
            TrackingPreviewSetupUtilities.EnsurePreviewComponents(
                gameObject,
                out trackingTargetRig,
                out trackingTargetRigDriver,
                out trackingPreviewController,
                out faceBlendshapeDriver);
            RefreshPreviewAppliers();
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
                    new VrikAvatarLoaderPostprocessor(trackingTargetRig)
                });
        }

        private void RefreshPreviewAppliers()
        {
            if (trackingPreviewController == null)
            {
                return;
            }

            trackingPreviewController.AutoFindAppliers = false;
            trackingPreviewController.ApplierBehaviours = ResolvePreviewApplierBehaviours();
            trackingPreviewController.RefreshAppliers();
        }

        private MonoBehaviour[] ResolvePreviewApplierBehaviours()
        {
            int count = CountPreviewApplier(trackingTargetRigDriver) + CountPreviewApplier(faceBlendshapeDriver);
            if (count == 0)
            {
                return System.Array.Empty<MonoBehaviour>();
            }

            var appliers = new MonoBehaviour[count];
            int index = 0;
            AddPreviewApplier(appliers, ref index, trackingTargetRigDriver);
            AddPreviewApplier(appliers, ref index, faceBlendshapeDriver);
            return appliers;
        }

        void Update()
        {
            _camera.transform.position = trackingTargetRig.CameraWorldTarget.position;
            _camera.transform.rotation = trackingTargetRig.CameraWorldTarget.rotation;
        }

        private static int CountPreviewApplier(MonoBehaviour behaviour)
        {
            return behaviour is ITrackingFrameApplier ? 1 : 0;
        }

        private static void AddPreviewApplier(MonoBehaviour[] appliers, ref int index, MonoBehaviour behaviour)
        {
            if (behaviour is not ITrackingFrameApplier)
            {
                return;
            }

            appliers[index++] = behaviour;
        }
    }
}
