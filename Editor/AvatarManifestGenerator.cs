using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Star67.Sdk.Editor
{
    internal static class AvatarManifestGenerator
    {
        private const string AssetBundlesDirectoryName = "avatars/sandbox";
        private const string ManifestFileName = "avatar_manifest.json";

        [MenuItem("Star67/Avatar Manifest/Generate Manifest")]
        private static void GenerateManifest()
        {
            string projectRoot = GetProjectRoot();
            string assetBundlesRoot = Path.Combine(projectRoot, AssetBundlesDirectoryName);

            if (!Directory.Exists(assetBundlesRoot))
            {
                EditorUtility.DisplayDialog(
                    "Avatar Manifest",
                    "Could not find the AssetBundles directory at:\n" + assetBundlesRoot,
                    "OK");
                return;
            }

            AvatarManifest manifest = BuildManifest(projectRoot, assetBundlesRoot);
            string manifestPath = Path.Combine(assetBundlesRoot, ManifestFileName);
            string json = JsonUtility.ToJson(manifest, true);

            File.WriteAllText(manifestPath, json + Environment.NewLine);
            AssetDatabase.Refresh();

            Debug.LogFormat(
                "Generated avatar manifest with {0} avatar(s): {1}",
                manifest.avatars.Count,
                NormalizeSeparators(manifestPath));

            EditorUtility.DisplayDialog(
                "Avatar Manifest",
                string.Format("Generated {0} avatar manifest entr{1} at:\n{2}",
                    manifest.avatars.Count,
                    manifest.avatars.Count == 1 ? "y" : "ies",
                    NormalizeSeparators(manifestPath)),
                "OK");
        }

        private static AvatarManifest BuildManifest(string projectRoot, string assetBundlesRoot)
        {
            AvatarManifest manifest = new AvatarManifest();
            string[] files = Directory.GetFiles(assetBundlesRoot, "*", SearchOption.AllDirectories);
            string normalizedAssetBundlesRoot = NormalizeSeparators(Path.GetFullPath(assetBundlesRoot)).TrimEnd('/');
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (!string.Equals(Path.GetExtension(file), ".bee", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DirectoryInfo parentDirectory = Directory.GetParent(file);
                if (parentDirectory == null)
                {
                    continue;
                }

                string parentPath = NormalizeSeparators(parentDirectory.FullName).TrimEnd('/');
                if (string.Equals(parentPath, normalizedAssetBundlesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                manifest.avatars.Add(new AvatarManifestEntry
                {
                    path = GetProjectRelativePath(projectRoot, file),
                    name = parentDirectory.Name
                });
            }

            return manifest;
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo assetsDirectory = Directory.GetParent(Application.dataPath);
            if (assetsDirectory == null)
            {
                throw new DirectoryNotFoundException("Could not resolve the Unity project root.");
            }

            return assetsDirectory.FullName;
        }

        private static string GetProjectRelativePath(string projectRoot, string fullPath)
        {
            string normalizedRoot = NormalizeSeparators(Path.GetFullPath(projectRoot)).TrimEnd('/');
            string normalizedPath = NormalizeSeparators(Path.GetFullPath(fullPath));
            string rootPrefix = normalizedRoot + "/";

            if (!normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath;
            }

            return normalizedPath.Substring(rootPrefix.Length);
        }

        private static string NormalizeSeparators(string path)
        {
            return path.Replace('\\', '/');
        }

        [Serializable]
        private sealed class AvatarManifest
        {
            public List<AvatarManifestEntry> avatars = new List<AvatarManifestEntry>();
        }

        [Serializable]
        private sealed class AvatarManifestEntry
        {
            public string path;
            public string name;
        }
    }
}
