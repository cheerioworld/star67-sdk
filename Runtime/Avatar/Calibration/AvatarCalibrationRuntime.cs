using UnityEngine;

namespace Star67.Avatar.Calibration
{
    [DisallowMultipleComponent]
    public sealed class AvatarCalibrationRuntime : MonoBehaviour
    {
        [System.NonSerialized] private AvatarCalibrationState state = new AvatarCalibrationState();

        public AvatarCalibrationState State => state ??= new AvatarCalibrationState();

        public void SetState(AvatarCalibrationState calibrationState)
        {
            state = calibrationState ?? new AvatarCalibrationState();
        }
    }
}
