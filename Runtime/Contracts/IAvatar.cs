using System;
using System.Collections.Generic;
using UnityEngine;

namespace Star67
{
    public enum AvatarType
    {
        Genies = 0,
        Basis = 1,
        VRM = 3
    }

    public class AvatarDescriptor: IAvatarDescriptor {
        public AvatarType Type { get; init; }
        public string AvatarId { get; init; }
        public string Uri { get; init;  }
        public IReadOnlyDictionary<string, string> Metadata { get; init; }
    };

    public interface IAvatar : IDisposable
    {
        IAvatarDescriptor Descriptor { get; }
        IAvatarRig Rig { get; }
        IList<SkinnedMeshRenderer> FaceTrackingRenderers { get; }
    }

    public interface IAvatarRig
    {
        Transform Root  { get; }
        Animator Animator { get; }
        Transform Hips { get; }
        Transform Spine { get; }
        Transform Chest { get; }
        Transform UpperChest { get; }
        Transform Neck { get; }
        Transform Head { get; }
        Transform LeftEye { get; }
        Transform RightEye { get; }
        Transform LeftShoulder { get; }
        Transform LeftUpperArm { get; }
        Transform LeftLowerArm { get; }
        Transform LeftHand { get; }
        Transform RightShoulder { get; }
        Transform RightUpperArm { get; }
        Transform RightLowerArm { get; }
        Transform RightHand { get; }
        Transform LeftUpperLeg { get; }
        Transform LeftLowerLeg { get; }
        Transform LeftFoot { get; }
        Transform LeftToes { get; }
        Transform RightUpperLeg { get; }
        Transform RightLowerLeg { get; }
        Transform RightFoot { get; }
        Transform RightToes { get; }
        Transform[] LeftThumb { get; }
        Transform[] LeftIndex { get; }
        Transform[] LeftMiddle { get; }
        Transform[] LeftRing { get; }
        Transform[] LeftLittle { get; }
        Transform[] RightThumb { get; }
        Transform[] RightIndex { get; }
        Transform[] RightMiddle { get; }
        Transform[] RightRing { get; }
        Transform[] RightLittle { get; }
        bool TryGetBone(HumanBodyBones bone, out Transform transform);
    }
}
