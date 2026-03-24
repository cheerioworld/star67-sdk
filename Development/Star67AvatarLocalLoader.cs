using System;
using System.Threading;
using System.Threading.Tasks;
using Basis.Scripts.BasisSdk;
using Star67.Avatar;
using UnityEngine;

namespace Star67.Sdk.Avatar
{
    public class Star67AvatarLocalLoader: PostprocessedAvatarLoaderBase
    {
        public Star67AvatarLocalLoader(System.Collections.Generic.IEnumerable<IAvatarLoaderPostprocessor> postLoadProcessors = null)
            : base(postLoadProcessors)
        {
        }

        public override bool CanLoad(IAvatarDescriptor d)
        {
            if (d.Type == AvatarType.Basis)
            {
                return true;
            }

            return false;
        }

        public override async Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor d, Transform parent, CancellationToken ct)
        {
            var basisAvatars = UnityEngine.Object.FindObjectsByType<BasisAvatar>(FindObjectsSortMode.None);
            
            var basisAvatar = basisAvatars.Length > 0 ? basisAvatars[0] : null;
            if (basisAvatar == null)
            {
                throw new Exception("Could not find any Star67Avatars in the scene to 'load'");
            }
            
            if (basisAvatars.Length > 1)
            {
                Debug.LogWarning($"Multiple Star67Avatars found in the scene. Using '{basisAvatar.name}'.");
            }
            
            var rig = new AvatarRootRig(basisAvatar.transform, basisAvatar.Animator);
            var avatar = new Star67BasisAvatar(d, rig);
            avatar.Components.Add<AvatarFaceBlendshapeDriver>();
            avatar.Components.Add<AvatarVRIKTargetsDriver>();
            return await PostprocessLoadedAvatarAsync(avatar, ct);
        }
    }
}
