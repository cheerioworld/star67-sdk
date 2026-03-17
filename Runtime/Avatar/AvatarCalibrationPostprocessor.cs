using System.Threading;
using System.Threading.Tasks;
using Star67.Avatar.Calibration;

namespace Star67.Avatar
{
    public sealed class AvatarCalibrationPostprocessor : IAvatarLoaderPostprocessor
    {
        private readonly IAvatarCalibrationService calibrationService;

        public AvatarCalibrationPostprocessor()
            : this(new AvatarCalibrationService())
        {
        }

        public AvatarCalibrationPostprocessor(IAvatarCalibrationService calibrationService)
        {
            this.calibrationService = calibrationService;
        }

        public int Order => 150;

        public bool CanProcess(IAvatar avatar)
        {
            if (avatar?.Rig?.Root == null || avatar.Descriptor == null)
            {
                return false;
            }

            AvatarType type = avatar.Descriptor.Type;
            return type == AvatarType.Basis || type == AvatarType.Genies;
        }

        public Task ProcessAsync(IAvatar avatar, CancellationToken ct)
        {
            if (!CanProcess(avatar) || calibrationService == null)
            {
                return Task.CompletedTask;
            }

            return calibrationService.CalibrateAsync(avatar, ct);
        }
    }
}
