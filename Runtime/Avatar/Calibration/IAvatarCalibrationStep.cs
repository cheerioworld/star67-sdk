namespace Star67.Avatar.Calibration
{
    public interface IAvatarCalibrationStep
    {
        int Order { get; }
        bool CanCalibrate(IAvatar avatar);
        void Calibrate(AvatarCalibrationContext context);
    }
}
