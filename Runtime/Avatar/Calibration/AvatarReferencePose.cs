using System;
using UnityEngine;

namespace Star67.Avatar.Calibration
{
    [Serializable]
    public sealed class AvatarReferencePose
    {
        [SerializeField] private AvatarBoneReferencePose[] bonePoses = new AvatarBoneReferencePose[(int)HumanBodyBones.LastBone];

        public AvatarBoneReferencePose[] BonePoses => EnsureCapacity();

        public bool HasAnyPose
        {
            get
            {
                AvatarBoneReferencePose[] poses = EnsureCapacity();
                for (int i = 0; i < poses.Length; i++)
                {
                    if (poses[i].IsValid)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Clear()
        {
            AvatarBoneReferencePose[] poses = EnsureCapacity();
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i] = default;
            }
        }

        public void SetPose(in AvatarBoneReferencePose pose)
        {
            int index = (int)pose.Bone;
            if (index < 0 || index >= (int)HumanBodyBones.LastBone)
            {
                return;
            }

            AvatarBoneReferencePose[] poses = EnsureCapacity();
            poses[index] = pose;
        }

        public bool TryGetPose(HumanBodyBones bone, out AvatarBoneReferencePose pose)
        {
            int index = (int)bone;
            if (index < 0 || index >= (int)HumanBodyBones.LastBone)
            {
                pose = default;
                return false;
            }

            AvatarBoneReferencePose[] poses = EnsureCapacity();
            pose = poses[index];
            return pose.IsValid;
        }

        private AvatarBoneReferencePose[] EnsureCapacity()
        {
            if (bonePoses == null || bonePoses.Length != (int)HumanBodyBones.LastBone)
            {
                bonePoses = new AvatarBoneReferencePose[(int)HumanBodyBones.LastBone];
            }

            return bonePoses;
        }
    }
}
