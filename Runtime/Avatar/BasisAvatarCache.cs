using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Star67.Avatar
{
    public sealed class BasisAvatarCache
    {
        private readonly string _root;
        private readonly Dictionary<string, SemaphoreSlim> _locks = new();

        public BasisAvatarCache(string root) => _root = root ?? throw new ArgumentNullException(nameof(root));

        public bool TryGetPath(string key, out string path)
        {
            path = PathFor(key);
            try
            {
                var info = new FileInfo(path);
                string[] receipt = File.ReadAllLines(path + ".ready");
                return info.Exists && receipt.Length == 3 && receipt[2].Length == 44 &&
                    long.TryParse(receipt[0], out long size) && size > 0 && info.Length == size &&
                    long.TryParse(receipt[1], out long ticks) && info.LastWriteTimeUtc.Ticks == ticks;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public async Task<bool> ValidateAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!TryGetPath(key, out string path)) return false;
                string[] receipt = File.ReadAllLines(path + ".ready");
                if (receipt.Length != 3) return false;
                string actual = await Task.Run(() => HashFile(path, ct), ct);
                ct.ThrowIfCancellationRequested();
                return TryGetPath(key, out _) && string.Equals(receipt[2], actual, StringComparison.Ordinal);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public void Invalidate(string key)
        {
            string path = PathFor(key);
            File.Delete(path + ".ready");
            File.Delete(path);
        }

        public async Task<string> DownloadAsync(string key, string url, string password, CancellationToken ct,
            IProgress<AvatarLoadProgress> observer = null)
        {
            SemaphoreSlim gate;
            lock (_locks)
            {
                if (!_locks.TryGetValue(key, out gate)) _locks[key] = gate = new SemaphoreSlim(1, 1);
            }
            await gate.WaitAsync(ct);
            string path = PathFor(key);
            try
            {
                if (await ValidateAsync(key, ct))
                {
                    AvatarLoadProgress.Report(observer, AvatarLoadStage.AssetsReady);
                    return path;
                }
                Invalidate(key);
                Directory.CreateDirectory(_root);
                foreach (string partial in Directory.GetFiles(_root, Path.GetFileName(path) + ".*.part"))
                    File.Delete(partial);
                AvatarLoadProgress.Report(observer, AvatarLoadStage.Download, true, 0);
                var result = await BasisIOManagement.DownloadBEEEx(url, password, new BasisProgressReport(), ct,
                    downloadProgress: (bytes, total) => AvatarLoadProgress.Report(observer, AvatarLoadStage.Download, true, bytes, total),
                    destinationPath: path);
                ct.ThrowIfCancellationRequested();
                if (!result.IsSuccess || result.Value == null)
                    throw new AvatarLoadException(BasisAvatarLoader.ClassifyDownloadFailure(result.ResponseCode, result.FailureKind),
                        "Avatar asset acquisition failed.");
                await CommitAsync(key, ct);
                AvatarLoadProgress.Report(observer, AvatarLoadStage.AssetsReady, true,
                    result.Value.DownloadedBytes, result.Value.DownloadedBytes);
                return path;
            }
            catch
            {
                if (!TryGetPath(key, out _)) Invalidate(key);
                throw;
            }
            finally { gate.Release(); }
        }

        internal async Task CommitAsync(string key, CancellationToken ct)
        {
            string path = PathFor(key);
            string hash = await Task.Run(() => HashFile(path, ct), ct);
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            if (info.Length == 0) throw new InvalidDataException("An empty avatar cannot be cached.");
            string temporary = path + ".ready.part";
            try
            {
                File.WriteAllLines(temporary, new[] {
                    info.Length.ToString(CultureInfo.InvariantCulture),
                    info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture), hash });
                ct.ThrowIfCancellationRequested();
                if (File.Exists(path + ".ready")) File.Replace(temporary, path + ".ready", null);
                else File.Move(temporary, path + ".ready");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal string PathFor(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Cache key is required.", nameof(key));
            using var sha = SHA256.Create();
            string digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
            return Path.Combine(_root, digest + ".bee");
        }

        private static string HashFile(string path, CancellationToken ct)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var buffer = new byte[65536];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                sha.TransformBlock(buffer, 0, count, buffer, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToBase64String(sha.Hash);
        }
    }
}
