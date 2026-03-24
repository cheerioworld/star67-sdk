using System;
using Star67.Avatar;
using Star67.Avatar.Calibration;
using Star67.Sdk.Avatar;
using Star67.Tracking;
using UnityEngine;

namespace Star67.Sdk.Tracking.Editor
{
    public class EditorTrackingPipeline: MonoBehaviour
    {
        private ITrackingFrameSource _source;
        private IAvatar _avatar;

        private readonly TrackingFrameBuffer _frameBuffer = new TrackingFrameBuffer();
        private IAvatarFaceBlendshapeDriver _faceSink;
        private AvatarVRIKTargetsDriver _ikTargetsSink;
        private AvatarCalibrationController _calibrationController;
        private readonly AvatarHeightCalibratedCameraPoseProcessor _cameraPoseProcessor = new();
        private Transform _cameraTransform;

        private Context _context = new();

        void Awake()
        {
            _cameraTransform = Camera.main.transform;
        }

        public void SetSource(ITrackingFrameSource source)
        {
            Debug.Log("Source set");
            _source = source;
        }

        public void SetAvatar(IAvatar avatar)
        {
            UnbindCalibrationController();
            _avatar = avatar;
            _faceSink = null;
            _ikTargetsSink = null;
            RefreshContext();

            if (_avatar == null)
            {
                return;
            }

            if (!_avatar.Components.TryGet(out _faceSink))
            {
                Debug.LogError("Failed to bind face sink/IAvatarFaceBlendshapeDriver");
            }

            if (!_avatar.Components.TryGet(out _ikTargetsSink))
            {
                Debug.LogError("Failed to bind ikTargetsSink/AvatarVRIKTargetsDriver");
            }

            if (!_avatar.Components.TryGet(out _calibrationController))
            {
                Debug.LogWarning("Failed to bind AvatarCalibrationController");
            }
            else
            {
                _calibrationController.CalibrationChanged += OnCalibrationChanged;
            }

            RefreshContext();
        }

        private void Update()
        {
            if (_source == null) return;
            if (_avatar == null) return;

            _source.Update();

            if (!_source.TryCopyLatestFrame(_frameBuffer))
            {
                return;
            }
            if (_faceSink != null)
            {
                _faceSink.Apply(_frameBuffer.FaceBlendshapes);
            }

            if (_ikTargetsSink != null)
            {
                Pose calibratedCameraPose = _cameraPoseProcessor.Process(_frameBuffer.CameraWorldPose, _context);
                _cameraTransform.SetPositionAndRotation(calibratedCameraPose.position, calibratedCameraPose.rotation);
                _ikTargetsSink.Apply(_frameBuffer, calibratedCameraPose);
            }
        }

        private void OnDestroy()
        {
            UnbindCalibrationController();
        }

        private void OnCalibrationChanged(AvatarCalibrationState _)
        {
            RefreshContext();
        }

        private void RefreshContext()
        {
            _context.CameraHeightOffset = _calibrationController != null ? _calibrationController.CameraHeightOffset : 0f;
        }

        private void UnbindCalibrationController()
        {
            if (_calibrationController != null)
            {
                _calibrationController.CalibrationChanged -= OnCalibrationChanged;
                _calibrationController = null;
            }
        }
        
        private sealed class Context
        {
            public float CameraHeightOffset;
        }

        private sealed class AvatarHeightCalibratedCameraPoseProcessor
        {
            public Pose Process(TrackingPose cameraPose, Context context)
            {
                TrackingVector3Value positionValue = cameraPose.GetPositionValue();
                TrackingQuaternionValue rotationValue = cameraPose.GetRotationValue();
                Vector3 position = new Vector3(positionValue.X, positionValue.Y, positionValue.Z);
                Quaternion rotation = new Quaternion(
                    rotationValue.X,
                    rotationValue.Y,
                    rotationValue.Z,
                    rotationValue.W);

                float heightOffset = context != null ? context.CameraHeightOffset : 0f;
                if (Mathf.Abs(heightOffset) > Mathf.Epsilon)
                {
                    position += Vector3.up * heightOffset;
                }

                return new Pose(position, rotation);
            }
        }
    }
}
