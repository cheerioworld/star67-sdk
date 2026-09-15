using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67.Avatar
{
    public enum AvatarLoadStage { Download, AssetsReady, Apply, Activated }
    public enum AvatarLoadFailureCategory { Unknown, Network, Timeout, Rejected, NotFound, InvalidData, Unsupported, Configuration }

    public readonly struct AvatarLoadProgress
    {
        public AvatarLoadStage Stage { get; }
        public bool IsRemote { get; }
        public long? DownloadedBytes { get; }
        public AvatarLoadProgress(AvatarLoadStage stage, bool isRemote = false, long? downloadedBytes = null)
        { Stage = stage; IsRemote = isRemote; DownloadedBytes = downloadedBytes; }

        public static void Report(IProgress<AvatarLoadProgress> observer, AvatarLoadStage stage, bool isRemote = false, long? downloadedBytes = null)
        {
            try { observer?.Report(new AvatarLoadProgress(stage, isRemote, downloadedBytes)); }
            catch (Exception) { /* Observers cannot fail a load. */ }
        }
    }

    /// <summary>Optional reporting extension; existing IAvatarLoader implementations continue to work.</summary>
    public interface IObservableAvatarLoader : IAvatarLoader
    {
        Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor descriptor, Transform parent, CancellationToken ct,
            IProgress<AvatarLoadProgress> observer);
    }

    public sealed class AvatarLoadException : Exception
    {
        public AvatarLoadFailureCategory Category { get; }
        public AvatarLoadException(AvatarLoadFailureCategory category, string message) : base(message) => Category = category;
    }
}
