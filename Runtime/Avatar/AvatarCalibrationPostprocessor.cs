using System.Threading;
using System.Threading.Tasks;
using Star67.Avatar.Calibration;
using UnityEngine;

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

            if (!avatar.Components.TryGet(out AvatarCalibrationController calibrationController))
            {
                calibrationController = new AvatarCalibrationController(calibrationService);
                if (!avatar.Components.Add(calibrationController))
                {
                    Debug.LogWarning($"AvatarCalibrationPostprocessor: Failed to add {nameof(AvatarCalibrationController)} to avatar '{avatar.Rig.Root.name}'.");
                    return Task.CompletedTask;
                }
            }
            else
            {
                calibrationController.SetCalibrationService(calibrationService);
            }

            calibrationController.Calibrate();
            return Task.CompletedTask;
        }
    }
}
