using Basis.Scripts.BasisSdk;
using Basis.Scripts.BasisSdk.Helpers;
using UnityEngine;
using UnityEngine.Scripting;

namespace Star67.Avatar.Calibration
{
    public sealed class EyeHeightCalibrationStep : IAvatarCalibrationStep
    {
        public int Order => 200;

        public bool CanCalibrate(IAvatar avatar)
        {
            return avatar?.Rig?.Root != null;
        }
        
        [Preserve]
        public EyeHeightCalibrationStep() {}

        public void Calibrate(AvatarCalibrationContext context)
        {
            if (context?.Rig?.Root == null)
            {
                return;
            }

            if (TryResolveEyeFromBones(context, out Vector3 eyeLocalPosition)
                || TryResolveBasisEyePosition(context, out eyeLocalPosition)
                || TryResolveHeadFallback(context, out eyeLocalPosition))
            {
                context.State.EyeLocalPosition = eyeLocalPosition;
                context.State.EyeHeightMeters = eyeLocalPosition.y;
                context.State.HasEyeHeight = true;
            }
            else
            {
                context.State.HasEyeHeight = false;
            }
        }

        private static bool TryResolveEyeFromBones(AvatarCalibrationContext context, out Vector3 eyeLocalPosition)
        {
            bool hasLeft = context.State.ReferencePose.TryGetPose(HumanBodyBones.LeftEye, out AvatarBoneReferencePose leftEye);
            bool hasRight = context.State.ReferencePose.TryGetPose(HumanBodyBones.RightEye, out AvatarBoneReferencePose rightEye);

            if (hasLeft && hasRight)
            {
                eyeLocalPosition = Vector3.Lerp(leftEye.RootSpacePosition, rightEye.RootSpacePosition, 0.5f);
                return true;
            }

            if (hasLeft)
            {
                eyeLocalPosition = leftEye.RootSpacePosition;
                return true;
            }

            if (hasRight)
            {
                eyeLocalPosition = rightEye.RootSpacePosition;
                return true;
            }

            eyeLocalPosition = Vector3.zero;
            return false;
        }

        private static bool TryResolveBasisEyePosition(AvatarCalibrationContext context, out Vector3 eyeLocalPosition)
        {
            eyeLocalPosition = Vector3.zero;
            BasisAvatar basisAvatar = context.Root.GetComponentInChildren<BasisAvatar>(true);
            if (basisAvatar == null || basisAvatar.AvatarEyePosition == Vector2.zero)
            {
                return false;
            }

            Vector3 animatorLocalEyePosition = BasisHelpers.AvatarPositionConversion(basisAvatar.AvatarEyePosition);
            Transform animatorTransform = context.Animator != null ? context.Animator.transform : context.Root;
            Vector3 worldEyePosition = animatorTransform.TransformPoint(animatorLocalEyePosition);
            eyeLocalPosition = context.Root.InverseTransformPoint(worldEyePosition);
            return true;
        }

        private static bool TryResolveHeadFallback(AvatarCalibrationContext context, out Vector3 eyeLocalPosition)
        {
            if (context.State.ReferencePose.TryGetPose(HumanBodyBones.Head, out AvatarBoneReferencePose headPose))
            {
                eyeLocalPosition = headPose.RootSpacePosition;
                return true;
            }

            eyeLocalPosition = Vector3.zero;
            return false;
        }
    }
}
