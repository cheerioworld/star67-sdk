using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67.Sdk.Avatar
{
    public class Star67AvatarLocalLoader: IAvatarLoader
    {
        public bool CanLoad(IAvatarDescriptor d)
        {
            if (d.Type == AvatarType.Basis)
            {
                return true;
            }

            return false;
        }

        public Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor d, Transform parent, CancellationToken ct)
        {
            SceneStar67AvatarAdapter avatar = SceneStar67AvatarAdapter.TryCreateFirstInScene(out int avatarCount);
            if (avatar == null)
            {
                throw new Exception("Could not find any Star67Avatars in the scene to 'load'");
            }

            if (avatarCount > 1)
            {
                Debug.LogWarning($"Multiple Star67Avatars found in the scene. Using '{avatar.AvatarName}'.");
            }

            return Task.FromResult<IAvatar>(avatar);
        }
    }
}
