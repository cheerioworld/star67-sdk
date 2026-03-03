using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67
{
    public interface IAvatarLoader
    {
        bool CanLoad(IAvatarDescriptor d);
        Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor d, Transform parent, CancellationToken ct);
    }
}