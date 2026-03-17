using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67
{
    /// <summary>
    /// Loads an avatar descriptor into a fully constructed and postprocessed runtime avatar instance.
    /// </summary>
    public interface IAvatarLoader
    {
        /// <summary>
        /// Returns true when this loader can load the supplied descriptor.
        /// </summary>
        bool CanLoad(IAvatarDescriptor d);

        /// <summary>
        /// Loads and postprocesses an avatar, returning the ready-to-activate runtime instance.
        /// </summary>
        Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor d, Transform parent, CancellationToken ct);
    }

    public abstract class PostprocessedAvatarLoaderBase : IAvatarLoader
    {
        private readonly IAvatarLoaderPostprocessor[] _postLoadProcessors;

        protected PostprocessedAvatarLoaderBase(IEnumerable<IAvatarLoaderPostprocessor> postLoadProcessors = null)
        {
            _postLoadProcessors = postLoadProcessors == null
                ? Array.Empty<IAvatarLoaderPostprocessor>()
                : postLoadProcessors
                    .Where(processor => processor != null)
                    .OrderBy(processor => processor.Order)
                    .ToArray();
        }

        public abstract bool CanLoad(IAvatarDescriptor d);

        public abstract Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor d, Transform parent, CancellationToken ct);

        protected async Task<IAvatar> PostprocessLoadedAvatarAsync(IAvatar avatar, CancellationToken cancellationToken)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            try
            {
                for (int i = 0; i < _postLoadProcessors.Length; i++)
                {
                    IAvatarLoaderPostprocessor processor = _postLoadProcessors[i];
                    if (!processor.CanProcess(avatar))
                    {
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await processor.ProcessAsync(avatar, cancellationToken);
                }

                return avatar;
            }
            catch
            {
                DisposeAvatar(avatar);
                throw;
            }
        }

        private static void DisposeAvatar(IAvatar avatar)
        {
            try
            {
                avatar.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PostprocessedAvatarLoaderBase: Failed to dispose avatar cleanly after postprocessing failure. {exception.Message}");
            }
        }
    }
}
