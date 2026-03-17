using System.Collections.Generic;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [DisallowMultipleComponent]
    public sealed class TrackingPreviewController : MonoBehaviour
    {
        [SerializeField] private bool autoFindAppliers = true;
        [SerializeField] private MonoBehaviour[] applierBehaviours;
        private readonly TrackingFrameBuffer _frameBuffer = new TrackingFrameBuffer();
        private readonly List<ITrackingFrameApplier> _appliers = new List<ITrackingFrameApplier>();

        private ITrackingFrameSource _source;

        public bool AutoFindAppliers
        {
            get => autoFindAppliers;
            set => autoFindAppliers = value;
        }

        public MonoBehaviour[] ApplierBehaviours
        {
            get => applierBehaviours;
            set => applierBehaviours = value;
        }

        public ITrackingFrameSource Source => _source;

        private void Awake()
        {
            RefreshAppliers();
        }

        private void OnEnable()
        {
            RefreshAppliers();
        }

        private void OnDisable()
        {
            ResetAppliers();
        }

        public void SetSource(ITrackingFrameSource source)
        {
            if (ReferenceEquals(_source, source))
            {
                return;
            }

            ResetAppliers();
            _source = source;
        }

        [ContextMenu("Refresh Appliers")]
        public void RefreshAppliers()
        {
            _appliers.Clear();

            if (autoFindAppliers)
            {
                MonoBehaviour[] components = GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is ITrackingFrameApplier applier)
                    {
                        _appliers.Add(applier);
                    }
                }
            }

            if (applierBehaviours == null)
            {
                return;
            }

            for (int i = 0; i < applierBehaviours.Length; i++)
            {
                if (applierBehaviours[i] is ITrackingFrameApplier applier && !_appliers.Contains(applier))
                {
                    _appliers.Add(applier);
                }
            }
        }

        private void LateUpdate()
        {
            if (_source == null)
            {
                return;
            }

            _source.Update();
            if (!_source.TryCopyLatestFrame(_frameBuffer))
            {
                return;
            }

            for (int i = 0; i < _appliers.Count; i++)
            {
                _appliers[i].ApplyFrame(_frameBuffer);
            }
        }

        private void ResetAppliers()
        {
            for (int i = 0; i < _appliers.Count; i++)
            {
                _appliers[i].ResetState();
            }
        }
    }
}
