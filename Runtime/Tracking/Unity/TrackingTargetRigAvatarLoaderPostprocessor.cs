using System.Threading;
using System.Threading.Tasks;

namespace Star67.Tracking.Unity
{
    [System.Obsolete("Use UserTrackingAvatarBindingPostprocessor instead.")]
    public sealed class TrackingTargetRigAvatarLoaderPostprocessor : IAvatarLoaderPostprocessor
    {
        private readonly UserTrackingAvatarBindingPostprocessor _inner;

        public TrackingTargetRigAvatarLoaderPostprocessor()
        {
            _inner = new UserTrackingAvatarBindingPostprocessor();
        }

        public TrackingTargetRigAvatarLoaderPostprocessor(UserTrackingService userTrackingService)
        {
            _inner = new UserTrackingAvatarBindingPostprocessor(userTrackingService);
        }

        public int Order => _inner.Order;

        public bool CanProcess(IAvatar avatar)
        {
            return _inner.CanProcess(avatar);
        }

        public Task ProcessAsync(IAvatar avatar, CancellationToken ct)
        {
            return _inner.ProcessAsync(avatar, ct);
        }
    }
}
