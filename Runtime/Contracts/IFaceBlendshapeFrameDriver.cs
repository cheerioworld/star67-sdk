using UnityEngine;

namespace Star67
{
    public interface IFaceBlendshapeFrameDriver
    {
        void ApplyFrame(FaceBlendshape[] frame);
        void UpdateTarget(SkinnedMeshRenderer[] faceMeshes);
    }
}
