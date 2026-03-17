using System;
using Star67.Avatar.Calibration;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    public class AvatarTrackingDriver : ITrackingFrameApplier, IDisposable
    {
        public TrackingFeatureFlags RequiredFeatures { get; }
        
        private TrackingTargetRigDriver _targetRigDriver;
        private IAvatarCalibrationService _calibrationService;
        
        private AvatarCalibrationState _calibrationState;

        public AvatarTrackingDriver(
            TrackingTargetRigDriver targetRigDriver,
            IAvatarCalibrationService calibrationService
        )
        {
            _targetRigDriver = targetRigDriver;
            _calibrationService = calibrationService;
            _calibrationService.AvatarCalibrated += OnAvatarCalibrated;
        }
        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            _targetRigDriver.ApplyFrame(frame);
            
        }
        
        void OnAvatarCalibrated(AvatarCalibrationState calibrationState)
        {
            _calibrationState = calibrationState;
        }

        public void ResetState()
        {
            
        }

        public void Dispose()
        {
            _calibrationService.AvatarCalibrated -= OnAvatarCalibrated;
        }
    }
}