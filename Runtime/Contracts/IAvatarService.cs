using System.Threading;
using System.Threading.Tasks;

namespace Star67.Avatar
{
    public interface IAvatarService
    {
        
        Task<IAvatar> LoadAvatar(IAvatarDescriptor descriptor, CancellationToken cancellationToken = default);
    }
}