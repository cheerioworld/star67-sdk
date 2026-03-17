using System;

namespace Star67.Avatar.Calibration
{
    public interface IAvatarCalibrationPoseGuard
    {
        bool CanGuard(IAvatar avatar);
        IDisposable Enter(IAvatar avatar);
    }
}
