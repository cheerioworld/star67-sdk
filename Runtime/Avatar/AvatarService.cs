using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Star67.Avatar
{
  public class AvatarService : IAvatarService
  {
    public event Action<IAvatar> AvatarLoaded;
    public event Action<IAvatar> AvatarUnloaded;

    public IAvatar CurrentAvatar => _activeAvatar;

    private readonly IEnumerable<IAvatarLoader> _loaders;
    private readonly Transform _avatarParent;
    private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

    private IAvatar _activeAvatar;

    [Preserve]
    public AvatarService(IEnumerable<IAvatarLoader> loaders)
    {
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

          IAvatar previousAvatar = _activeAvatar;
          _activeAvatar = loadedAvatar;
          activated = true;

          if (previousAvatar != null && !ReferenceEquals(previousAvatar, loadedAvatar))
          {
            DisposeAvatar(previousAvatar);
          }

          AvatarLoaded?.Invoke(loadedAvatar);
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

    private void DisposeAvatar(IAvatar avatar)
    {
      AvatarUnloaded?.Invoke(avatar);
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
