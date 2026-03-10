using System.Threading;
using System.Threading.Tasks;

namespace Star67
{
    public interface IAvatarLoaderPostprocessor
    {
        int Order { get; }
        bool CanProcess(IAvatar avatar);
        Task ProcessAsync(IAvatar avatar, CancellationToken ct);
    }
}
