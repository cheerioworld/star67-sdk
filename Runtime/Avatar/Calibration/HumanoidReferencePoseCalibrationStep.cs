using UnityEngine;
using UnityEngine.Scripting;

namespace Star67.Avatar.Calibration
{
    public sealed class HumanoidReferencePoseCalibrationStep : IAvatarCalibrationStep
    {
        public int Order => 100;

        public bool CanCalibrate(IAvatar avatar)
        {
            return avatar?.Rig?.Root != null;
        }
        
        [Preserve]
        public HumanoidReferencePoseCalibrationStep() {}

        public void Calibrate(AvatarCalibrationContext context)
        {
            if (context?.Rig?.Root == null)
            {
                return;
            }

            AvatarReferencePose referencePose = context.State.ReferencePose;
            referencePose.Clear();

            Transform root = context.Rig.Root;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                HumanBodyBones bone = (HumanBodyBones)i;
                if (!context.Rig.TryGetBone(bone, out Transform boneTransform) || boneTransform == null)
                {
                    continue;
                }

                var pose = new AvatarBoneReferencePose
                {
                    Bone = bone,
                    IsValid = true,
                    LocalPosition = boneTransform.localPosition,
                    LocalRotation = boneTransform.localRotation,
                    RootSpacePosition = root.InverseTransformPoint(boneTransform.position),
                    RootSpaceRotation = Quaternion.Inverse(root.rotation) * boneTransform.rotation
                };

                referencePose.SetPose(pose);
            }
        }
    }
}
