using System;
using UnityEngine;

namespace Star67.Avatar.Calibration
{
    [Serializable]
    public struct AvatarBoneReferencePose
    {
        public HumanBodyBones Bone;
        public bool IsValid;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 RootSpacePosition;
        public Quaternion RootSpaceRotation;
    }
}
