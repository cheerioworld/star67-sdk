using System;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [Serializable]
    public sealed class TrackingTargetRigState
    {
        public bool CameraTracked;
        public bool HeadTracked;
        public bool LeftHandTracked;
        public bool RightHandTracked;
    }

    [DisallowMultipleComponent]
    public sealed class TrackingTargetRig : MonoBehaviour
    {
        [SerializeField] private Transform cameraWorldTarget;
        [SerializeField] private Transform headWorldTarget;
        [SerializeField] private Transform leftWristTarget;
        [SerializeField] private Transform rightWristTarget;
        [SerializeField] private Transform[] leftHandJointTargets = new Transform[TrackingProtocol.HandJointCount];
        [SerializeField] private Transform[] rightHandJointTargets = new Transform[TrackingProtocol.HandJointCount];
        [SerializeField] private TrackingTargetRigState state = new TrackingTargetRigState();

        public Transform CameraWorldTarget
        {
            get => cameraWorldTarget;
            set => cameraWorldTarget = value;
        }

        public Transform HeadWorldTarget
        {
            get => headWorldTarget;
            set => headWorldTarget = value;
        }

        public Transform LeftWristTarget
        {
            get => leftWristTarget;
            set => leftWristTarget = value;
        }

        public Transform RightWristTarget
        {
            get => rightWristTarget;
            set => rightWristTarget = value;
        }

        public Transform[] LeftHandJointTargets => EnsureJointTargets(ref leftHandJointTargets);
        public Transform[] RightHandJointTargets => EnsureJointTargets(ref rightHandJointTargets);
        public TrackingTargetRigState State => state;

        private static Transform[] EnsureJointTargets(ref Transform[] jointTargets)
        {
            if (jointTargets == null || jointTargets.Length != TrackingProtocol.HandJointCount)
            {
                jointTargets = new Transform[TrackingProtocol.HandJointCount];
            }

            return jointTargets;
        }
    }
}
