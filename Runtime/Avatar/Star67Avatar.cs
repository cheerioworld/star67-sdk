using Basis.Scripts.BasisSdk;

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
    }
}