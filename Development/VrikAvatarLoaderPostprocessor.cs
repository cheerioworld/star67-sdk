using System.Threading;
using System.Threading.Tasks;
using Star67.Sdk.Avatar;
using UnityEngine.Scripting;

namespace Star67.Avatar
{
  public sealed class VrikAvatarLoaderPostprocessor : IAvatarLoaderPostprocessor
  {
    [Preserve]
    public VrikAvatarLoaderPostprocessor() { }

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

      // Transform root = avatar.Rig.Root;
      // AvatarIKTargets trackingTargets = avatar.IKTargets;
      // if (trackingTargets == null)
      // {
      //   Debug.LogWarning($"VrikAvatarLoaderPostprocessor: A AvatarIKTargets was not found while loading avatar '{root.name}'. Skipping VRIK setup.");
      //   return Task.CompletedTask;
      // }

      AvatarVRIKTargetsDriver avatarComponent;
      if (!avatar.Components.TryGet(out avatarComponent))
      {
        avatar.Components.Add<AvatarVRIKTargetsDriver>();
      }
      return Task.CompletedTask;
    }
  }
}
