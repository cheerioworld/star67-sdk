using System;
using Star67.Avatar.Calibration;
using UnityEngine;

namespace Star67.Avatar
{
    public class AvatarCalibrationController : AvatarComponent
    {
        private IAvatarCalibrationService _calibrationService;
        private AvatarCalibrationState _state = new();

        public AvatarCalibrationController()
            : this(new AvatarCalibrationService())
        {
        }

        public AvatarCalibrationController(IAvatarCalibrationService calibrationService)
        {
            _calibrationService = calibrationService ?? new AvatarCalibrationService();
        }

        public override string Name => "AvatarCalibrationController";
        public event Action<AvatarCalibrationState> CalibrationChanged;

        public AvatarCalibrationState State => _state ??= new AvatarCalibrationState();
        public bool IsCalibrated => State.IsCalibrated;
        public float CameraHeightOffset => State.HasEyeHeight ? State.EyeHeightMeters - 0.3f : 0f;

        protected override bool TryInitialize()
        {
            _state ??= new AvatarCalibrationState();
            return true;
        }

        public void Calibrate()
        {
            Debug.Log("Calibrate called 1");
            _state = _calibrationService?.Calibrate(Avatar) ?? new AvatarCalibrationState();
            CalibrationChanged?.Invoke(State);
        }

        public void SetCalibrationService(IAvatarCalibrationService calibrationService)
        {
            _calibrationService = calibrationService ?? new AvatarCalibrationService();
        }

        protected override void OnRemoved()
        {
            _state = new AvatarCalibrationState();
        }
    }
}
