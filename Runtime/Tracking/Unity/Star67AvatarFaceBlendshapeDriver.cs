using System;
using System.Collections.Generic;
using Star67.Sdk;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Applies face blendshape weights from tracking frames onto explicitly bound skinned meshes.
    /// </summary>
    public sealed class Star67AvatarFaceBlendshapeDriver : MonoBehaviour, ITrackingFrameApplier
    {
        [SerializeField] private SkinnedMeshRenderer[] faceRenderers = Array.Empty<SkinnedMeshRenderer>();

        private FaceTrackingRendererSink _driver;
        private FaceBlendshape[] _resetFrame;

        /// <summary>
        /// Gets or sets explicit skinned mesh renderers that should receive face blendshape updates.
        /// </summary>
        public SkinnedMeshRenderer[] FaceRenderers
        {
            get => faceRenderers;
            set => SetFaceRenderers(value);
        }

        /// <inheritdoc />
        public TrackingFeatureFlags RequiredFeatures => TrackingFeatureFlags.Face;

        private void Awake()
        {
            RebindDriver();
        }

        private void OnEnable()
        {
            RebindDriver();
        }

        /// <summary>
        /// Rebinds the driver to the face renderers exposed by the given avatar.
        /// </summary>
        public void BindAvatar(IAvatar avatar)
        {
            ResetState();
            SetFaceRenderers(avatar?.FaceTrackingRenderers);
        }

        /// <summary>
        /// Clears the current renderer binding and resets any driven blendshapes back to zero.
        /// </summary>
        public void ClearBinding()
        {
            ResetState();
            SetFaceRenderers(Array.Empty<SkinnedMeshRenderer>());
        }

        /// <summary>
        /// Replaces the active renderer binding.
        /// </summary>
        public void SetFaceRenderers(IList<SkinnedMeshRenderer> renderers)
        {
            faceRenderers = SanitizeRenderers(renderers);
            RebindDriver();
        }

        /// <inheritdoc />
        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            if (_driver == null)
            {
                RebindDriver();
            }

            if (_driver == null || frame == null || (frame.Features & TrackingFeatureFlags.Face) == 0)
            {
                return;
            }

            _driver.Apply(frame.FaceBlendshapes);
        }

        /// <inheritdoc />
        public void ResetState()
        {
            if (_driver == null)
            {
                RebindDriver();
            }

            if (_driver == null)
            {
                return;
            }

            EnsureResetFrame();
            _driver.Apply(_resetFrame);
        }

        private void RebindDriver()
        {
            if (faceRenderers == null)
            {
                faceRenderers = Array.Empty<SkinnedMeshRenderer>();
            }

            if (_driver == null)
            {
                if (faceRenderers.Length == 0)
                {
                    return;
                }

                _driver = new FaceTrackingRendererSink(faceRenderers);
                return;
            }

            _driver.SetTargetRenderers(faceRenderers);
        }

        private void EnsureResetFrame()
        {
            if (_resetFrame != null)
            {
                return;
            }

            Array values = Enum.GetValues(typeof(FaceBlendshapeLocation));
            _resetFrame = new FaceBlendshape[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                _resetFrame[i] = new FaceBlendshape
                {
                    location = (FaceBlendshapeLocation)values.GetValue(i),
                    weight = 0f
                };
            }
        }

        private static SkinnedMeshRenderer[] SanitizeRenderers(IList<SkinnedMeshRenderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                return Array.Empty<SkinnedMeshRenderer>();
            }

            var sanitized = new List<SkinnedMeshRenderer>(renderers.Count);
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer != null && !sanitized.Contains(renderer))
                {
                    sanitized.Add(renderer);
                }
            }

            return sanitized.Count == 0 ? Array.Empty<SkinnedMeshRenderer>() : sanitized.ToArray();
        }
    }
}
