using System.Collections.Generic;
using System.Linq;
using Basis.Scripts.BasisSdk;
using UnityEngine;

namespace Star67.Sdk
{
    public class Star67Avatar : BasisAvatar
    {
        // private AvatarDescriptor descriptor;
        // private IAvatarRig rig;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        // public IAvatarDescriptor Descriptor => descriptor ??= new AvatarDescriptor
        // {
        //     Type = AvatarType.Basis,
        //     AvatarId = name,
        //     Uri = gameObject.scene.path,
        //     Metadata = new Dictionary<string, string>()
        // };
        //
        // public IAvatarRig Rig => rig ??= new global::Star67.AvatarRootRig(transform, Animator);
        public IList<SkinnedMeshRenderer> FaceTrackingRenderers
        {
            get
            {
                if (_faceTrackingRenderers != null)
                {
                    return _faceTrackingRenderers;
                }
                var dedupe = new HashSet<SkinnedMeshRenderer>();
                if (FaceVisemeMesh != null)
                {
                    dedupe.Add(FaceVisemeMesh);
                }

                if (FaceBlinkMesh != null)
                {
                    dedupe.Add(FaceBlinkMesh);
                }

                _faceTrackingRenderers = dedupe.ToArray();
                return _faceTrackingRenderers;
            }
        }
        private IList<SkinnedMeshRenderer> _faceTrackingRenderers;
    }
}
