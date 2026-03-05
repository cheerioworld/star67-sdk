using System;
using System.Collections.Generic;
using UnityEngine;

namespace Star67
{
    public enum AvatarType
    {
        Genies = 0,
        Basis = 1,
        VRM = 2
    }

    public class AvatarDescriptor: IAvatarDescriptor {
        public AvatarType Type { get; init; }
        public string AvatarId { get; init; }
        public string Uri { get; init;  }
        public IReadOnlyDictionary<string, string> Metadata { get; init; }
    };

    public interface IAvatar : IDisposable
    {
        IAvatarDescriptor Descriptor { get; }
        IAvatarRig Rig { get; }
        IList<SkinnedMeshRenderer> FaceTrackingRenderers { get; }
    }

    public interface IAvatarRig
    {
        Transform Root  { get; }
        // SkinnedMeshRenderer[] SkinnedMeshRenderers { get; }
        // IBlendShapeController BlendShapes { get; }
    }
}