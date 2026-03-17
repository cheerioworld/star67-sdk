using System;
using System.Threading;
using System.Threading.Tasks;

namespace Star67.Avatar.Calibration
{
    public interface IAvatarCalibrationService
    {
        event Action<AvatarCalibrationState> AvatarCalibrated;
        Task<AvatarCalibrationRuntime> CalibrateAsync(IAvatar avatar, CancellationToken cancellationToken = default);
    }
}
