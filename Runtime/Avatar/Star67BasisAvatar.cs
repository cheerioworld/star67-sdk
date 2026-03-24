using System.Collections.Generic;
using Basis.Scripts.BasisSdk;
using UnityEngine;

namespace Star67.Avatar
{
    public sealed class Star67BasisAvatar : IAvatar
    {
        public IAvatarDescriptor Descriptor { get; }
        public IAvatarRig Rig { get; }
        public IList<SkinnedMeshRenderer> FaceTrackingRenderers { get; }
        public AvatarComponentManager Components { get; }
        public AvatarIKTargets IKTargets { get; }

        public Star67BasisAvatar(IAvatarDescriptor descriptor, IAvatarRig rig)
        {
            Components = new AvatarComponentManager(this);
            Descriptor = descriptor;
            Rig = rig;
            FaceTrackingRenderers = CollectFaceTrackingRenderers(rig?.Root);
            IKTargets = AvatarIKTargets.Create(rig?.Root.parent);
        }

        public void Dispose()
        {
            Debug.Log("Disposing basis avatar");
            UnityEngine.Object.Destroy(Rig.Root.gameObject);
        }

        private static IList<SkinnedMeshRenderer> CollectFaceTrackingRenderers(Transform root)
        {
            if (root == null)
            {
                return new List<SkinnedMeshRenderer>();
            }

            var renderers = new List<SkinnedMeshRenderer>();
            BasisAvatar basisAvatar = root.GetComponentInChildren<BasisAvatar>(true);
            if (basisAvatar != null)
            {
                if (basisAvatar.FaceVisemeMesh != null)
                {
                    renderers.Add(basisAvatar.FaceVisemeMesh);
                }

                if (basisAvatar.FaceBlinkMesh != null && !renderers.Contains(basisAvatar.FaceBlinkMesh))
                {
                    renderers.Add(basisAvatar.FaceBlinkMesh);
                }
            }

            return renderers;
        }
    }
}