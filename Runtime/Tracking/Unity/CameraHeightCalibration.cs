using Star67.Avatar.Calibration;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    public class CameraHeightCalibration: ITrackingFrameApplier
    {
        private AvatarCalibrationService calibrationService;
        private Transform cameraOffset;
        private AvatarCalibrationState currentCalibration;
        
        public CameraHeightCalibration(AvatarCalibrationService calibrationService, Transform cameraOffset)
        {
            this.calibrationService = calibrationService;
            this.cameraOffset = cameraOffset;
            calibrationService.AvatarCalibrated += OnAvatarCalibrated;
        }

        private void OnAvatarCalibrated(AvatarCalibrationState state)
        {
            Debug.Log("OnAvatarCalibrated in CameraHeightCalibration");
            currentCalibration = state;
        }

        public TrackingFeatureFlags RequiredFeatures => TrackingFeatureFlags.CameraWorldPose;
        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            
        }

        public void ResetState()
        {
            
        }
    }
}