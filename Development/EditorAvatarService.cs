using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Star67.Avatar;
using Star67.Avatar.Calibration;
using Star67.Tracking.Unity;
using UnityEngine;

namespace Star67.Sdk.Avatar
{
  
  public class EditorAvatarService: MonoBehaviour
  {
    private IEnumerable<IAvatarLoader> _loaders;
    private readonly Transform _avatarParent;
    private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

    public IAvatar activeAvatar;

    private void Awake()
    {
      UserTrackingService userTrackingService = FindAnyObjectByType<UserTrackingService>();
      var calibrationService = new AvatarCalibrationService(
        new IAvatarCalibrationStep[]
        {
          new HumanoidReferencePoseCalibrationStep(),
          new EyeHeightCalibrationStep()
        },
        new IAvatarCalibrationPoseGuard[]
        {
          new FinalIkAvatarCalibrationPoseGuard()
        });
      var postLoadProcessors = new IAvatarLoaderPostprocessor[]
      {
        new UserTrackingAvatarBindingPostprocessor(userTrackingService),
        new AvatarCalibrationPostprocessor(calibrationService),
        new VrikAvatarLoaderPostprocessor(userTrackingService)
      };

      var loaders = new IAvatarLoader[] { new Star67AvatarLocalLoader(postLoadProcessors) };
      _loaders = loaders ?? throw new ArgumentNullException(nameof(loaders));
    }

    public EditorAvatarService(IEnumerable<IAvatarLoader> loaders, Transform avatarParent)
    {
      _avatarParent = avatarParent;
      _loaders = loaders ?? throw new ArgumentNullException(nameof(loaders));
    }

    public async Task<IAvatar> LoadAvatar(IAvatarDescriptor descriptor, CancellationToken cancellationToken = default)
    {
      if (descriptor == null)
      {
        throw new ArgumentNullException(nameof(descriptor));
      }

      await _loadLock.WaitAsync(cancellationToken);
      try
      {
        IAvatarLoader loader = ResolveLoader(descriptor);
        IAvatar loadedAvatar = null;
        bool activated = false;

        try
        {
          loadedAvatar = await loader.LoadAvatarAsync(descriptor, _avatarParent, cancellationToken);
          if (loadedAvatar == null)
          {
            throw new InvalidOperationException($"Loader '{loader.GetType().Name}' returned null avatar.");
          }

          IAvatar previousAvatar = activeAvatar;
          activeAvatar = loadedAvatar;
          activated = true;

          if (previousAvatar != null && !ReferenceEquals(previousAvatar, loadedAvatar))
          {
            DisposeAvatar(previousAvatar);
          }

          return loadedAvatar;
        }
        catch
        {
          if (!activated && loadedAvatar != null)
          {
            DisposeAvatar(loadedAvatar);
          }

          throw;
        }
      }
      finally
      {
        _loadLock.Release();
      }
    }

    private IAvatarLoader ResolveLoader(IAvatarDescriptor descriptor)
    {
      foreach (IAvatarLoader loader in _loaders)
      {
        if (loader != null && loader.CanLoad(descriptor))
        {
          return loader;
        }
      }

      throw new NotSupportedException($"No avatar loader found for avatar type '{descriptor.Type}'.");
    }

    private static void DisposeAvatar(IAvatar avatar)
    {
      try
      {
        avatar.Dispose();
      }
      catch (Exception exception)
      {
        Debug.LogWarning($"AvatarService: Failed to dispose avatar cleanly. {exception.Message}");
      }
    }
  }
}
