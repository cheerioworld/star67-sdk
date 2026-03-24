using System;
using System.Threading;
using System.Threading.Tasks;

namespace Star67.Avatar
{
    public interface IAvatarService
    {
        event Action<IAvatar> AvatarLoaded;
        event Action<IAvatar> AvatarUnloaded;
        
        IAvatar CurrentAvatar { get; }
        Task<IAvatar> LoadAvatar(IAvatarDescriptor descriptor, CancellationToken cancellationToken = default);
    }
}