using UnityEngine;

namespace Star67.Avatar.Calibration
{
    public sealed class AvatarCalibrationContext
    {
        public AvatarCalibrationContext(IAvatar avatar, AvatarCalibrationState state)
        {
            Avatar = avatar;
            State = state;
            Rig = avatar?.Rig;
            Root = Rig?.Root;
            Animator = Rig?.Animator;
        }

        public IAvatar Avatar { get; }
        public IAvatarRig Rig { get; }
        public Transform Root { get; }
        public Animator Animator { get; }
        public AvatarCalibrationState State { get; }
    }
}
