using System.Collections.Generic;
using System.Linq;
using Basis.Scripts.BasisSdk;
using UnityEngine;

namespace Star67.Sdk
{
    public class Star67Avatar : BasisAvatar, IAvatar
    {
        public void Dispose()
        {
            Destroy(gameObject);
        }

        public IAvatarDescriptor Descriptor { get; }
        public IAvatarRig Rig { get; }
        public IList<SkinnedMeshRenderer> FaceTrackingRenderers
        {
            get
            {
                if (_faceTrackingRenderers != null)
                {
                    return _faceTrackingRenderers;
                }
                var dedupe = new HashSet<SkinnedMeshRenderer>();
                dedupe.Add(this.FaceVisemeMesh);
                dedupe.Add(this.FaceBlinkMesh);
                _faceTrackingRenderers = dedupe.ToArray();
                return _faceTrackingRenderers;
            }
        }
        private IList<SkinnedMeshRenderer> _faceTrackingRenderers;
    }
}