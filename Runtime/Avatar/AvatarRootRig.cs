using UnityEngine;

namespace Star67
{
    public sealed class AvatarRootRig : IAvatarRig
    {
        private readonly Transform[] humanoidBones = new Transform[(int)HumanBodyBones.LastBone];
        private readonly Transform[] leftThumb;
        private readonly Transform[] leftIndex;
        private readonly Transform[] leftMiddle;
        private readonly Transform[] leftRing;
        private readonly Transform[] leftLittle;
        private readonly Transform[] rightThumb;
        private readonly Transform[] rightIndex;
        private readonly Transform[] rightMiddle;
        private readonly Transform[] rightRing;
        private readonly Transform[] rightLittle;

        public AvatarRootRig(Transform root)
            : this(root, root != null ? root.GetComponentInChildren<Animator>(true) : null)
        {
        }

        public AvatarRootRig(Transform root, Animator animator)
        {
            Root = root;
            Animator = animator;

            if (Animator != null && Animator.isHuman)
            {
                CacheHumanoidBones(Animator);
            }

            leftThumb = CreateFingerBones(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal);
            leftIndex = CreateFingerBones(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
            leftMiddle = CreateFingerBones(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal);
            leftRing = CreateFingerBones(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal);
            leftLittle = CreateFingerBones(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal);
            rightThumb = CreateFingerBones(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal);
            rightIndex = CreateFingerBones(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
            rightMiddle = CreateFingerBones(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal);
            rightRing = CreateFingerBones(HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal);
            rightLittle = CreateFingerBones(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal);
        }

        public Transform Root { get; }
        public Animator Animator { get; }
        public Transform Hips => GetBone(HumanBodyBones.Hips);
        public Transform Spine => GetBone(HumanBodyBones.Spine);
        public Transform Chest => GetBone(HumanBodyBones.Chest);
        public Transform UpperChest => GetBone(HumanBodyBones.UpperChest);
        public Transform Neck => GetBone(HumanBodyBones.Neck);
        public Transform Head => GetBone(HumanBodyBones.Head);
        public Transform LeftEye => GetBone(HumanBodyBones.LeftEye);
        public Transform RightEye => GetBone(HumanBodyBones.RightEye);
        public Transform LeftShoulder => GetBone(HumanBodyBones.LeftShoulder);
        public Transform LeftUpperArm => GetBone(HumanBodyBones.LeftUpperArm);
        public Transform LeftLowerArm => GetBone(HumanBodyBones.LeftLowerArm);
        public Transform LeftHand => GetBone(HumanBodyBones.LeftHand);
        public Transform RightShoulder => GetBone(HumanBodyBones.RightShoulder);
        public Transform RightUpperArm => GetBone(HumanBodyBones.RightUpperArm);
        public Transform RightLowerArm => GetBone(HumanBodyBones.RightLowerArm);
        public Transform RightHand => GetBone(HumanBodyBones.RightHand);
        public Transform LeftUpperLeg => GetBone(HumanBodyBones.LeftUpperLeg);
        public Transform LeftLowerLeg => GetBone(HumanBodyBones.LeftLowerLeg);
        public Transform LeftFoot => GetBone(HumanBodyBones.LeftFoot);
        public Transform LeftToes => GetBone(HumanBodyBones.LeftToes);
        public Transform RightUpperLeg => GetBone(HumanBodyBones.RightUpperLeg);
        public Transform RightLowerLeg => GetBone(HumanBodyBones.RightLowerLeg);
        public Transform RightFoot => GetBone(HumanBodyBones.RightFoot);
        public Transform RightToes => GetBone(HumanBodyBones.RightToes);
        public Transform[] LeftThumb => leftThumb;
        public Transform[] LeftIndex => leftIndex;
        public Transform[] LeftMiddle => leftMiddle;
        public Transform[] LeftRing => leftRing;
        public Transform[] LeftLittle => leftLittle;
        public Transform[] RightThumb => rightThumb;
        public Transform[] RightIndex => rightIndex;
        public Transform[] RightMiddle => rightMiddle;
        public Transform[] RightRing => rightRing;
        public Transform[] RightLittle => rightLittle;

        public bool TryGetBone(HumanBodyBones bone, out Transform transform)
        {
            int index = (int)bone;
            if (index < 0 || index >= humanoidBones.Length)
            {
                transform = null;
                return false;
            }

            transform = humanoidBones[index];
            return transform != null;
        }

        private void CacheHumanoidBones(Animator animator)
        {
            for (int i = 0; i < humanoidBones.Length; i++)
            {
                humanoidBones[i] = animator.GetBoneTransform((HumanBodyBones)i);
            }
        }

        private Transform[] CreateFingerBones(HumanBodyBones proximal, HumanBodyBones intermediate, HumanBodyBones distal)
        {
            return new[]
            {
                GetBone(proximal),
                GetBone(intermediate),
                GetBone(distal)
            };
        }

        private Transform GetBone(HumanBodyBones bone)
        {
            TryGetBone(bone, out Transform transform);
            return transform;
        }
    }
}
