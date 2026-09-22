using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Star67.Avatar;

namespace Star67.Sdk.Editor.Tests
{
    public sealed class BasisAvatarCacheTests
    {
        private string _root;
        private BasisAvatarCache _cache;
        private const string Key = "api\navatar\n1\niOS";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "Star67CacheTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _cache = new BasisAvatarCache(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test, Timeout(5000)]
        public async Task OnlyCommittedFilesAreReusableAcrossCacheInstances()
        {
            File.WriteAllBytes(_cache.PathFor(Key), new byte[] { 1, 2, 3 });
            Assert.That(_cache.TryGetPath(Key, out _), Is.False);
            await _cache.CommitAsync(Key, CancellationToken.None);
            var reopened = new BasisAvatarCache(_root);
            Assert.That(reopened.TryGetPath(Key, out var path), Is.True);
            Assert.That(path, Is.EqualTo(_cache.PathFor(Key)));
            Assert.That(await reopened.ValidateAsync(Key, CancellationToken.None), Is.True);
            Assert.That(Directory.GetFiles(_root, "*.part"), Is.Empty);
        }

        [Test, Timeout(5000)]
        public async Task FullValidationRejectsSameLengthCorruptionEvenWithPreservedTimestamp()
        {
            string path = _cache.PathFor(Key);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            await _cache.CommitAsync(Key, CancellationToken.None);
            var timestamp = File.GetLastWriteTimeUtc(path);
            File.WriteAllBytes(path, new byte[] { 3, 2, 1 });
            File.SetLastWriteTimeUtc(path, timestamp);
            Assert.That(_cache.TryGetPath(Key, out _), Is.True);
            Assert.That(await _cache.ValidateAsync(Key, CancellationToken.None), Is.False);
            _cache.Invalidate(Key);
            Assert.That(_cache.TryGetPath(Key, out _), Is.False);
            Assert.That(File.Exists(path), Is.False);
        }

        [Test, Timeout(5000)]
        public async Task TruncatedFilesAndMissingReceiptsAreNotCached()
        {
            string path = _cache.PathFor(Key);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            await _cache.CommitAsync(Key, CancellationToken.None);
            File.WriteAllBytes(path, new byte[] { 1 });
            Assert.That(_cache.TryGetPath(Key, out _), Is.False);
            File.Delete(path + ".ready");
            Assert.That(await _cache.ValidateAsync(Key, CancellationToken.None), Is.False);
        }

        [Test, Timeout(5000)]
        public async Task CancelledCommitNeverMarksPartialDataReady()
        {
            File.WriteAllBytes(_cache.PathFor(Key), new byte[] { 1, 2, 3 });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try { await _cache.CommitAsync(Key, cancellation.Token); Assert.Fail("Expected cancellation."); }
            catch (OperationCanceledException) { }
            Assert.That(_cache.TryGetPath(Key, out _), Is.False);
            Assert.That(Directory.GetFiles(_root, "*.part"), Is.Empty);
        }

        [Test]
        public void EnvironmentVersionAndPlatformHaveDistinctSafePaths()
        {
            Assert.That(_cache.PathFor(Key), Is.Not.EqualTo(_cache.PathFor("other-api\navatar\n1\niOS")));
            Assert.That(_cache.PathFor(Key), Is.Not.EqualTo(_cache.PathFor("api\navatar\n2\niOS")));
            Assert.That(_cache.PathFor(Key), Is.Not.EqualTo(_cache.PathFor("api\navatar\n1\nAndroid")));
            Assert.That(Path.GetDirectoryName(_cache.PathFor("../../avatar")), Is.EqualTo(_root));
        }
    }
}
