using System;
using Star67.Tracking;
using Star67.Tracking.Unity;
using UnityEngine;

namespace Star67.Sdk.Tracking.Editor
{
    public class EditorTrackingPipeline: MonoBehaviour
    {
        private ITrackingFrameSource _source;
        private readonly TrackingFrameBuffer _frameBuffer = new TrackingFrameBuffer();

        private FaceTrackingRendererSink _faceSink = new ();

        private EditorTrackingPipelineContext _context;

        public void SetSource(ITrackingFrameSource source)
        {
            _source = source;
        }

        public void SetAvatar(IAvatar avatar)
        {
            
        }

        private void Update()
        {
            if (_source == null) return;
            
            _source.Update();
            if (!_source.TryCopyLatestFrame(_frameBuffer))
            {
                return;
            }
            
            _faceSink.Apply(_frameBuffer.FaceBlendshapes);
            
        }
    }
}