using System;
using System.Collections.Generic;
using Star67.Sdk;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class Star67AvatarFaceBlendshapeDriver : MonoBehaviour, ITrackingFrameApplier
    {
        [SerializeField] private Transform root;
        [SerializeField] private SkinnedMeshRenderer[] faceRenderers;
        [SerializeField] private bool includeChildMeshesWhenEmpty = true;

        private BasisFaceBlendshapeFrameDriver _driver;
        private FaceBlendshape[] _resetFrame;

        public Transform Root
        {
            get => root;
            set => root = value;
        }

        public SkinnedMeshRenderer[] FaceRenderers
        {
            get => faceRenderers;
            set => faceRenderers = value;
        }

        public TrackingFeatureFlags RequiredFeatures => TrackingFeatureFlags.Face;

        private void Awake()
        {
            Bind();
        }

        private void OnEnable()
        {
            Bind();
        }

        public void ApplyFrame(TrackingFrameBuffer frame)
        {
            if (_driver == null || frame == null || (frame.Features & TrackingFeatureFlags.Face) == 0)
            {
                return;
            }

            _driver.ApplyFrame(frame.FaceBlendshapes);
        }

        public void ResetState()
        {
            if (_driver == null)
            {
                return;
            }

            EnsureResetFrame();
            _driver.ApplyFrame(_resetFrame);
        }

        private void Bind()
        {
            if (root == null)
            {
                root = transform;
            }

            SkinnedMeshRenderer[] renderers = CollectFaceMeshes(root, faceRenderers, includeChildMeshesWhenEmpty);
            if (renderers.Length == 0)
            {
                _driver = null;
                return;
            }

            _driver = new BasisFaceBlendshapeFrameDriver(renderers);
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

        private static SkinnedMeshRenderer[] CollectFaceMeshes(Transform searchRoot, SkinnedMeshRenderer[] explicitRenderers, bool includeChildrenWhenEmpty)
        {
            var renderers = new List<SkinnedMeshRenderer>();

            if (explicitRenderers != null)
            {
                for (int i = 0; i < explicitRenderers.Length; i++)
                {
                    SkinnedMeshRenderer renderer = explicitRenderers[i];
                    if (renderer != null && !renderers.Contains(renderer))
                    {
                        renderers.Add(renderer);
                    }
                }
            }

            if (renderers.Count == 0 && includeChildrenWhenEmpty && searchRoot != null)
            {
                renderers.AddRange(searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }

            return renderers.ToArray();
        }
    }
}
