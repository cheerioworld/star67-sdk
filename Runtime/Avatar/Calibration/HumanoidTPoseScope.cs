using System;
using System.Collections.Generic;
using UnityEngine;

namespace Star67.Avatar.Calibration
{
    public sealed class HumanoidTPoseScope : IDisposable
    {
        private readonly Animator animator;
        private readonly bool animatorWasEnabled;
        private readonly TransformState[] transformStates;
        private bool disposed;

        private HumanoidTPoseScope(Animator animator, bool animatorWasEnabled, TransformState[] transformStates)
        {
            this.animator = animator;
            this.animatorWasEnabled = animatorWasEnabled;
            this.transformStates = transformStates;
        }

        public static bool TryCreate(IAvatarRig rig, out HumanoidTPoseScope scope)
        {
            scope = null;
            Animator animator = rig?.Animator;
            if (animator == null || animator.avatar == null || !animator.isHuman)
            {
                return false;
            }

            SkeletonBone[] skeleton = animator.avatar.humanDescription.skeleton;
            if (skeleton == null || skeleton.Length == 0)
            {
                return false;
            }

            var transformsByName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            Transform[] transforms = animator.transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || string.IsNullOrEmpty(transform.name) || transformsByName.ContainsKey(transform.name))
                {
                    continue;
                }

                transformsByName.Add(transform.name, transform);
            }

            var states = new List<TransformState>(skeleton.Length);
            bool animatorEnabled = animator.enabled;
            animator.enabled = false;

            for (int i = 0; i < skeleton.Length; i++)
            {
                SkeletonBone bone = skeleton[i];
                if (!transformsByName.TryGetValue(bone.name, out Transform transform) || transform == null)
                {
                    continue;
                }

                states.Add(new TransformState(transform));
                transform.localPosition = bone.position;
                transform.localRotation = bone.rotation;
                transform.localScale = bone.scale;
            }

            scope = new HumanoidTPoseScope(animator, animatorEnabled, states.ToArray());
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (transformStates != null)
            {
                for (int i = transformStates.Length - 1; i >= 0; i--)
                {
                    transformStates[i].Restore();
                }
            }

            if (animator != null)
            {
                animator.enabled = animatorWasEnabled;
            }
        }

        private readonly struct TransformState
        {
            private readonly Transform transform;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;

            public TransformState(Transform transform)
            {
                this.transform = transform;
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
            }

            public void Restore()
            {
                if (transform == null)
                {
                    return;
                }

                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = localScale;
            }
        }
    }
}
