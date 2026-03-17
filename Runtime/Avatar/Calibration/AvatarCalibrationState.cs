using System;
using UnityEngine;

namespace Star67.Avatar.Calibration
{
    [Serializable]
    public sealed class AvatarCalibrationState
    {
        [SerializeField] private bool isCalibrated;
        [SerializeField] private AvatarReferencePose referencePose = new AvatarReferencePose();
        [SerializeField] private bool hasEyeHeight;
        [SerializeField] private Vector3 eyeLocalPosition;
        [SerializeField] private float eyeHeightMeters;

        public bool IsCalibrated
        {
            get => isCalibrated;
            set => isCalibrated = value;
        }

        public AvatarReferencePose ReferencePose
        {
            get => referencePose ??= new AvatarReferencePose();
            set => referencePose = value ?? new AvatarReferencePose();
        }

        public bool HasEyeHeight
        {
            get => hasEyeHeight;
            set => hasEyeHeight = value;
        }

        public Vector3 EyeLocalPosition
        {
            get => eyeLocalPosition;
            set => eyeLocalPosition = value;
        }

        public float EyeHeightMeters
        {
            get => eyeHeightMeters;
            set => eyeHeightMeters = value;
        }
    }
}
