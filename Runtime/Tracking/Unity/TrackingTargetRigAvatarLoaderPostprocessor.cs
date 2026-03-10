using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    public sealed class TrackingTargetRigAvatarLoaderPostprocessor : IAvatarLoaderPostprocessor
    {
        public int Order => 100;

        public bool CanProcess(IAvatar avatar)
        {
            if (avatar?.Rig?.Root == null || avatar.Descriptor == null)
            {
                return false;
            }

            AvatarType type = avatar.Descriptor.Type;
            return type == AvatarType.Basis || type == AvatarType.Genies;
        }

        public Task ProcessAsync(IAvatar avatar, CancellationToken ct)
        {
            if (!CanProcess(avatar))
            {
                return Task.CompletedTask;
            }

            ct.ThrowIfCancellationRequested();

            Transform root = avatar.Rig.Root;
            TrackingTargetRig rig = root.GetComponent<TrackingTargetRig>();
            if (rig == null)
            {
                rig = root.gameObject.AddComponent<TrackingTargetRig>();
            }
            TrackingPreviewSetupUtilities.ConfigureTrackingTargetRig(root, rig, EnsureChild);

            return Task.CompletedTask;
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
