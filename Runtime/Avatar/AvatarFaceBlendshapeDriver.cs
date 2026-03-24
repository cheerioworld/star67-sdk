using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Star67.Avatar
{
    /// <summary>
    /// Drives ARKit-compatible face blendshapes across one or more target <see cref="SkinnedMeshRenderer"/> instances.
    /// The driver lazily discovers supported blendshape channels from the current targets, caches the resolved mapping,
    /// and applies incoming <see cref="FaceBlendshape"/> weights to every matching blendshape index.
    /// </summary>
    public class AvatarFaceBlendshapeDriver : AvatarComponent, IAvatarFaceBlendshapeDriver
    {
        private readonly Dictionary<FaceBlendshapeLocation, List<BlendshapeTarget>> _targetsByLocation = new();
        private static readonly IReadOnlyDictionary<string, FaceBlendshapeLocation> CamelCaseLocationLookup =
            FaceBlendshapeNameLookup.CamelCase;
        private static readonly IReadOnlyDictionary<string, FaceBlendshapeLocation> PascalCaseLocationLookup =
            FaceBlendshapeNameLookup.PascalCase;

        private SkinnedMeshRenderer[] _faceMeshes;
        private bool _isMapBuilt;

        /// <summary>
        /// Creates a new driver for the provided face meshes.
        /// Blendshape discovery is deferred until the first <see cref="Apply"/> call or until
        /// <see cref="SetTargetRenderers"/> is used to replace the active targets.
        /// </summary>
        /// <param name="faceMeshes">The initial set of skinned mesh renderers that may expose ARKit-compatible blendshapes.</param>
        public AvatarFaceBlendshapeDriver(SkinnedMeshRenderer[] faceMeshes = null)
        {
            _faceMeshes = faceMeshes ?? Array.Empty<SkinnedMeshRenderer>();
        }

        public AvatarFaceBlendshapeDriver() { }

        /// <summary>
        /// Applies a face tracking frame to all discovered target blendshapes.
        /// Weights are interpreted as normalized values in the range 0..1 and converted to Unity's 0..100 blendshape scale.
        /// Channels omitted from the frame are left unchanged.
        /// </summary>
        /// <param name="blendshapes">The ARKit-style blendshape frame to apply.</param>
        public void Apply(FaceBlendshape[] blendshapes)
        {
            if (!_isMapBuilt)
            {
                RebuildBlendshapeMap();
            }

            if (blendshapes == null || blendshapes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < blendshapes.Length; i++)
            {
                FaceBlendshape blendshape = blendshapes[i];
                if (!_targetsByLocation.TryGetValue(blendshape.location, out List<BlendshapeTarget> targets))
                {
                    continue;
                }

                float unityWeight = Mathf.Clamp01(blendshape.weight) * 100f;
                for (int j = 0; j < targets.Count; j++)
                {
                    BlendshapeTarget target = targets[j];
                    if (target.Renderer == null)
                    {
                        continue;
                    }

                    target.Renderer.SetBlendShapeWeight(target.BlendShapeIndex, unityWeight);
                }
            }
        }

        /// <summary>
        /// Replaces the active target renderers and rebuilds the cached blendshape map immediately.
        /// Previously driven weights on the old targets are not reset.
        /// </summary>
        /// <param name="faceMeshes">The new set of skinned mesh renderers to target.</param>
        public void UpdateTarget(SkinnedMeshRenderer[] faceMeshes)
        {
            _faceMeshes = faceMeshes;
            RebuildBlendshapeMap();
        }

        private void RebuildBlendshapeMap()
        {
            _targetsByLocation.Clear();

            if (_faceMeshes == null)
            {
                _isMapBuilt = true;
                return;
            }

            for (int i = 0; i < _faceMeshes.Length; i++)
            {
                SkinnedMeshRenderer renderer = _faceMeshes[i];
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = renderer.sharedMesh;
                IReadOnlyDictionary<string, FaceBlendshapeLocation> locationLookup = SelectLocationLookup(mesh);
                for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; blendShapeIndex++)
                {
                    string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);
                    if (!locationLookup.TryGetValue(blendShapeName, out FaceBlendshapeLocation location))
                    {
                        continue;
                    }

                    if (!_targetsByLocation.TryGetValue(location, out List<BlendshapeTarget> targets))
                    {
                        targets = new List<BlendshapeTarget>();
                        _targetsByLocation[location] = targets;
                    }

                    targets.Add(new BlendshapeTarget(renderer, blendShapeIndex));
                }
            }

            _isMapBuilt = true;
        }

        private static IReadOnlyDictionary<string, FaceBlendshapeLocation> SelectLocationLookup(Mesh mesh)
        {
            for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; blendShapeIndex++)
            {
                string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);
                if (!string.IsNullOrEmpty(blendShapeName) && CamelCaseLocationLookup.ContainsKey(blendShapeName))
                {
                    return CamelCaseLocationLookup;
                }
            }

            return PascalCaseLocationLookup;
        }

        private readonly struct BlendshapeTarget
        {
            public BlendshapeTarget(SkinnedMeshRenderer renderer, int blendShapeIndex)
            {
                Renderer = renderer;
                BlendShapeIndex = blendShapeIndex;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public int BlendShapeIndex { get; }
        }

        public override string Name => "AvatarFaceBlendshapeDriver";
        protected override bool TryInitialize()
        {
            _faceMeshes = Avatar.FaceTrackingRenderers.ToArray();
            RebuildBlendshapeMap();
            return true;
        }

        protected override void OnRemoved()
        {
            Debug.Log("AvatarFaceBlendshapeDriver.OnRemoved()");
        }
    }
}
