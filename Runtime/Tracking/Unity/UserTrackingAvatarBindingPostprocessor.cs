using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    public sealed class UserTrackingAvatarBindingPostprocessor : IAvatarLoaderPostprocessor
    {
        private readonly UserTrackingService _userTrackingService;

        public UserTrackingAvatarBindingPostprocessor()
        {
        }

        public UserTrackingAvatarBindingPostprocessor(UserTrackingService userTrackingService)
        {
            _userTrackingService = userTrackingService;
        }

        public int Order => 100;

        public bool CanProcess(IAvatar avatar)
        {
            if (avatar?.Rig?.Root == null || avatar.Descriptor == null)
            {
                return false;
            }

            AvatarType type = avatar.Descriptor.Type;
            return type == AvatarType.Basis || type == AvatarType.Genies;
        }

        public Task ProcessAsync(IAvatar avatar, CancellationToken ct)
        {
            if (!CanProcess(avatar))
            {
                return Task.CompletedTask;
            }

            ct.ThrowIfCancellationRequested();

            UserTrackingService userTrackingService = ResolveUserTrackingService();
            if (userTrackingService == null)
            {
                Debug.LogWarning(
                    $"UserTrackingAvatarBindingPostprocessor: No UserTrackingService was found while loading avatar '{avatar.Rig.Root.name}'. Skipping tracking binding.");
                return Task.CompletedTask;
            }

            TrackingPreviewSetupUtilities.EnsureUserTrackingService(userTrackingService.gameObject);
            TrackingPreviewSetupUtilities.BindAvatar(userTrackingService, avatar);
            return Task.CompletedTask;
        }

        private UserTrackingService ResolveUserTrackingService()
        {
            if (_userTrackingService != null)
            {
                return _userTrackingService;
            }

            return Object.FindAnyObjectByType<UserTrackingService>();
        }
    }
}
