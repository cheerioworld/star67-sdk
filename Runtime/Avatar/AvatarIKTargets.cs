using UnityEngine;

namespace Star67.Avatar
{
    public class AvatarIKTargets
    {
        public Transform TargetSpaceRoot { get; }
        public Transform CameraTarget { get; }
        public Transform HeadTarget { get; }
        public Transform LeftWristTarget { get; }
        public Transform RightWristTarget { get; }
        

        public AvatarIKTargets(Transform targetSpaceRoot, Transform headTarget, Transform leftWristTarget, Transform rightWristTarget, Transform cameraTarget = null)
        {
            TargetSpaceRoot = targetSpaceRoot;
            CameraTarget = cameraTarget;
            HeadTarget = headTarget;
            LeftWristTarget = leftWristTarget;
            RightWristTarget = rightWristTarget;
        }

        public static AvatarIKTargets Create(Transform parent = null)
        {
            var root = new GameObject("AvatarIKTargets").transform;
            root.SetParent(parent);
            var headTarget = new GameObject("HeadTarget").transform;
            headTarget.SetParent(root, false);
            var cameraTarget = new GameObject("CameraTarget").transform;
            cameraTarget.SetParent(root, false);
            var leftWristTarget = new GameObject("LeftWristTarget").transform;
            leftWristTarget.SetParent(root, false);
            var rightWristTarget = new GameObject("RightWristTarget").transform;
            rightWristTarget.SetParent(root, false);
            return new AvatarIKTargets(root, headTarget, leftWristTarget, rightWristTarget, cameraTarget);
        }
    }
}