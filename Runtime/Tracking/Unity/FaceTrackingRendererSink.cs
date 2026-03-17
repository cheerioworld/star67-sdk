using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;

namespace Star67.Tracking.Unity
{

    /// <summary>
    /// Drives ARKit-compatible face blendshapes across one or more target <see cref="SkinnedMeshRenderer"/> instances.
    /// The driver lazily discovers supported blendshape channels from the current targets, caches the resolved mapping,
    /// and applies incoming <see cref="FaceBlendshape"/> weights to every matching blendshape index.
    /// </summary>
    public class FaceTrackingRendererSink
    {
        private readonly Dictionary<FaceBlendshapeLocation, List<BlendshapeTarget>> _targetsByLocation = new();
        private static readonly Dictionary<string, FaceBlendshapeLocation> CanonicalLocationLookup = CreateCanonicalLocationLookup();
        private static readonly HashSet<string> NoiseTokens = new(StringComparer.Ordinal)
        {
            "blend",
            "shape",
            "blendshape",
            "morph",
            "morpher",
            "expr",
            "expression",
            "face",
            "facial",
            "arkit",
            "bs",
            "key"
        };

        private SkinnedMeshRenderer[] _faceMeshes;
        private bool _isMapBuilt;

        /// <summary>
        /// Creates a new driver for the provided face meshes.
        /// Blendshape discovery is deferred until the first <see cref="Apply"/> call or until
        /// <see cref="SetTargetRenderers"/> is used to replace the active targets.
        /// </summary>
        /// <param name="faceMeshes">The initial set of skinned mesh renderers that may expose ARKit-compatible blendshapes.</param>
        public FaceTrackingRendererSink(SkinnedMeshRenderer[] faceMeshes = null)
        {
            _faceMeshes = faceMeshes ?? Array.Empty<SkinnedMeshRenderer>();
        }

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
        public void SetTargetRenderers(SkinnedMeshRenderer[] faceMeshes)
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
                for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; blendShapeIndex++)
                {
                    string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);
                    if (!TryResolveLocation(blendShapeName, out FaceBlendshapeLocation location))
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

        private static bool TryResolveLocation(string blendShapeName, out FaceBlendshapeLocation location)
        {
            location = default;
            if (string.IsNullOrWhiteSpace(blendShapeName))
            {
                return false;
            }

            string canonicalName = Canonicalize(blendShapeName);
            if (canonicalName.Length > 0 && CanonicalLocationLookup.TryGetValue(canonicalName, out location))
            {
                return true;
            }

            string normalizedTokenName = NormalizeTokenizedName(blendShapeName);
            if (normalizedTokenName.Length == 0)
            {
                return false;
            }

            return CanonicalLocationLookup.TryGetValue(normalizedTokenName, out location);
        }

        private static Dictionary<string, FaceBlendshapeLocation> CreateCanonicalLocationLookup()
        {
            var lookup = new Dictionary<string, FaceBlendshapeLocation>(StringComparer.Ordinal);
            Array values = Enum.GetValues(typeof(FaceBlendshapeLocation));

            for (int i = 0; i < values.Length; i++)
            {
                FaceBlendshapeLocation location = (FaceBlendshapeLocation)values.GetValue(i);
                lookup[Canonicalize(location.ToString())] = location;
            }

            return lookup;
        }

        private static string NormalizeTokenizedName(string blendShapeName)
        {
            List<string> tokens = Tokenize(blendShapeName);
            if (tokens.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (token.Length == 0)
                {
                    continue;
                }

                token = token.ToLowerInvariant();
                if (NoiseTokens.Contains(token))
                {
                    continue;
                }

                if (token == "l")
                {
                    token = "left";
                }
                else if (token == "r")
                {
                    token = "right";
                }

                builder.Append(Canonicalize(token));
            }

            return builder.ToString();
        }

        private static List<string> Tokenize(string value)
        {
            var tokens = new List<string>();
            var currentToken = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char currentChar = value[i];
                if (!char.IsLetterOrDigit(currentChar))
                {
                    FlushToken(tokens, currentToken);
                    continue;
                }

                if (currentToken.Length > 0)
                {
                    char previousChar = currentToken[currentToken.Length - 1];
                    if (ShouldSplitToken(previousChar, currentChar))
                    {
                        FlushToken(tokens, currentToken);
                    }
                }

                currentToken.Append(currentChar);
            }

            FlushToken(tokens, currentToken);
            return tokens;
        }

        private static bool ShouldSplitToken(char previousChar, char currentChar)
        {
            if (char.IsDigit(previousChar) != char.IsDigit(currentChar))
            {
                return true;
            }

            return char.IsLower(previousChar) && char.IsUpper(currentChar);
        }

        private static void FlushToken(List<string> tokens, StringBuilder currentToken)
        {
            if (currentToken.Length == 0)
            {
                return;
            }

            tokens.Add(currentToken.ToString());
            currentToken.Clear();
        }

        private static string Canonicalize(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char currentChar = value[i];
                if (char.IsLetterOrDigit(currentChar))
                {
                    builder.Append(char.ToLowerInvariant(currentChar));
                }
            }

            return builder.ToString();
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
    }
}