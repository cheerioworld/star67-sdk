using UnityEngine;

namespace Star67
{
    public sealed class AvatarRootRig : IAvatarRig
    {
        public AvatarRootRig(Transform root)
        {
            Root = root;
        }

        public Transform Root { get; }
    }
}
