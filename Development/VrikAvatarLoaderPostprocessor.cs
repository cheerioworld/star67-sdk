using System.Threading;
using System.Threading.Tasks;
using RootMotion.FinalIK;
using Star67.Tracking.Unity;
using UnityEngine;

namespace Star67.Avatar
{
  public sealed class VrikAvatarLoaderPostprocessor : IAvatarLoaderPostprocessor
  {
    private readonly UserTrackingService _userTrackingService;
    private readonly TrackingTargetRig _trackingRig;

    public VrikAvatarLoaderPostprocessor()
    {
    }

    public VrikAvatarLoaderPostprocessor(UserTrackingService userTrackingService)
    {
      _userTrackingService = userTrackingService;
    }

    public VrikAvatarLoaderPostprocessor(TrackingTargetRig trackingRig)
    {
      _trackingRig = trackingRig;
    }

    public int Order => 200;

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

      Transform root = avatar.Rig.Root;
      TrackingTargetRig trackingRig = ResolveTrackingRig();
      if (trackingRig == null)
      {
        Debug.LogWarning($"VrikAvatarLoaderPostprocessor: A TrackingTargetRig was not found while loading avatar '{root.name}'. Skipping VRIK setup.");
        return Task.CompletedTask;
      }

      if (trackingRig.HeadWorldTarget == null || trackingRig.LeftWristTarget == null || trackingRig.RightWristTarget == null)
      {
        Debug.LogWarning($"VrikAvatarLoaderPostprocessor: Tracking targets are incomplete on TrackingTargetRig '{trackingRig.name}'. Skipping VRIK setup for avatar '{root.name}'.");
        return Task.CompletedTask;
      }

      VRIK vrik = root.GetComponent<VRIK>();
      if (vrik == null)
      {
        vrik = root.gameObject.AddComponent<VRIK>();
      }

      vrik.AutoDetectReferences();
      if (vrik.references == null || !vrik.references.isFilled)
      {
        Debug.LogWarning($"VrikAvatarLoaderPostprocessor: VRIK could not auto-detect a valid humanoid rig for avatar '{root.name}'.");
        return Task.CompletedTask;
      }

      vrik.GuessHandOrientations();
      vrik.solver.spine.headTarget = trackingRig.HeadWorldTarget;
      vrik.solver.leftArm.target = trackingRig.LeftWristTarget;
      vrik.solver.rightArm.target = trackingRig.RightWristTarget;
      return Task.CompletedTask;
    }

    private TrackingTargetRig ResolveTrackingRig()
    {
      if (_trackingRig != null)
      {
        return _trackingRig;
      }

      UserTrackingService userTrackingService = ResolveUserTrackingService();
      if (userTrackingService == null)
      {
        return null;
      }

      TrackingPreviewSetupUtilities.EnsureUserTrackingService(userTrackingService.gameObject);
      return userTrackingService.Rig;
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
