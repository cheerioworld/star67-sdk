using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Star67;
using UnityEngine;

namespace Star67.Sdk.Samples
{
  public class SampleAvatarLoader : MonoBehaviour
  {
    [SerializeField] private string filePath;

    private string resolvedPath;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
      resolvedPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, filePath);
      var descriptor = new AvatarDescriptor
      {
        Type = AvatarType.Basis,
        Uri = $"file://{resolvedPath}",
        Metadata = new Dictionary<string, string>()
        {
          { "password", "test" }
        }
      };
      var avatar = await LoadAvatarAsync(descriptor, transform, new CancellationToken());
      // var (basisBundleConnector, basisBundleGenerated, sectionBytes) = await LoadFromLocalBeeAsync(resolvedPath, "test",
      //   new BasisProgressReport(), new CancellationToken());
    }

    public async Task<IAvatar> LoadAvatarAsync(IAvatarDescriptor descriptor, Transform parent,
      CancellationToken cancellationToken)
    {
      if (descriptor == null)
      {
        throw new ArgumentNullException(nameof(descriptor));
      }

      cancellationToken.ThrowIfCancellationRequested();

      string beeSource = ResolveBeeSource(descriptor);
      string password = ResolveUnlockPassword(descriptor);
      Debug.Log($"Loading basis avatar from {beeSource}...");
      Debug.Log("Password: " + password);

      if (string.IsNullOrWhiteSpace(password))
      {
        throw new InvalidOperationException(
          "Basis avatar loading requires an unlock password. " +
          "Set descriptor.Metadata[\"unlockPassword\"] (or \"password\"/\"vp\").");
      }

      var progress = new BasisProgressReport();
      BasisBundleConnector connector;
      BasisBundleGenerated platformBundle;
      byte[] bundleSectionBytes;

      if (IsHttpUrl(beeSource))
      {
        (connector, platformBundle, bundleSectionBytes) = await LoadFromRemoteBeeAsync(
          beeSource,
          password,
          progress,
          cancellationToken);
      }
      else
      {
        string localBeePath = ResolveLocalBeePath(beeSource);
        (connector, platformBundle, bundleSectionBytes) = await LoadFromLocalBeeAsync(
          localBeePath,
          password,
          progress,
          cancellationToken);
      }

      cancellationToken.ThrowIfCancellationRequested();

      var bundleRequest = await BasisEncryptionToData.GenerateBundleFromFile(
        password,
        bundleSectionBytes,
        platformBundle.AssetBundleCRC,
        progress);

      if (bundleRequest?.assetBundle == null)
      {
        throw new InvalidOperationException("Basis bundle decryption/load failed (asset bundle is null).");
      }

      AssetBundle assetBundle = bundleRequest.assetBundle;
      GameObject spawnedAvatar = null;

      try
      {
        GameObject avatarPrefab =
          await LoadAvatarPrefabAsync(assetBundle, platformBundle.AssetToLoadName, cancellationToken);
        if (avatarPrefab == null)
        {
          throw new InvalidOperationException(
            $"Failed to locate avatar prefab in bundle. AssetToLoadName='{platformBundle.AssetToLoadName}'.");
        }

        spawnedAvatar = SpawnAvatarWithContentPolice(avatarPrefab, parent, descriptor);
        spawnedAvatar.name = BuildAvatarName(descriptor, connector, avatarPrefab.name);

        if (ShouldDisableAnimator(descriptor))
        {
          Animator[] animators = spawnedAvatar.GetComponentsInChildren<Animator>(true);
          for (int i = 0; i < animators.Length; i++)
          {
            animators[i].enabled = false;
          }
        }

        var avatarout = spawnedAvatar.GetComponentInChildren<Star67Avatar>();
        // var rig = new AvatarRig(
        //   spawnedAvatar.transform,
        //   spawnedAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        //
        // await UniTask.NextFrame(cancellationToken);
        // return new S67BasisAvatar(descriptor, rig);
        return avatarout;
      }
      catch
      {
        if (spawnedAvatar != null)
        {
          UnityEngine.Object.Destroy(spawnedAvatar);
        }

        throw;
      }
      finally
      {
        assetBundle.Unload(false);
      }
    }

    private static GameObject SpawnAvatarWithContentPolice(GameObject avatarPrefab, Transform parent,
      IAvatarDescriptor descriptor)
    {
      if (!ShouldUseContentPolice(descriptor))
      {
        return UnityEngine.Object.Instantiate(avatarPrefab, parent, false);
      }

      ContentPoliceSelector selector = EnsureAvatarContentPoliceSelector(descriptor);
      BundledContentHolder holder = EnsureBundledContentHolder();
      holder.AvatarScriptSelector = selector;

      var checksRequired = new ChecksRequired
      {
        UseContentRemoval = true,
        DisableAnimatorEvents = true,
        RemoveColliders = ShouldRemoveColliders(descriptor)
      };

      GameObject spawned = ContentPoliceControl.ContentControl(
        avatarPrefab,
        checksRequired,
        Vector3.zero,
        Quaternion.identity,
        false,
        Vector3.one,
        BundledContentHolder.Selector.Avatar,
        parent
      );

      if (spawned == null)
      {
        throw new InvalidOperationException(
          "Content police rejected or failed to spawn the Basis avatar prefab. " +
          "Verify AvatarScriptSelector whitelist includes the avatar's required components.");
      }

      return spawned;
    }

    private static BundledContentHolder EnsureBundledContentHolder()
    {
      if (BundledContentHolder.Instance != null)
      {
        return BundledContentHolder.Instance;
      }

      var holderObject = new GameObject("BasisBundledContentHolder_Runtime");
      var holder = holderObject.AddComponent<BundledContentHolder>();
      UnityEngine.Object.DontDestroyOnLoad(holderObject);
      return holder;
    }

    private static ContentPoliceSelector EnsureAvatarContentPoliceSelector(IAvatarDescriptor descriptor)
    {
      ContentPoliceSelector existing = BundledContentHolder.Instance?.AvatarScriptSelector;
      if (HasWhitelist(existing))
      {
        return existing;
      }

      ContentPoliceSelector fromMetadata = ResolveContentPoliceSelectorFromMetadata(descriptor);
      if (HasWhitelist(fromMetadata))
      {
        return fromMetadata;
      }

      throw new InvalidOperationException(
        "Content police is enabled but no valid Avatar ContentPoliceSelector is configured. " +
        "Set BundledContentHolder.Instance.AvatarScriptSelector, or pass metadata " +
        "\"contentPoliceSelectorResource\" (Resources path) or \"contentPoliceSelectedTypes\" (comma-separated type names).");
    }

    private static ContentPoliceSelector ResolveContentPoliceSelectorFromMetadata(IAvatarDescriptor descriptor)
    {
      string resourcePath = TryGetMetadataValue(
        descriptor,
        "contentPoliceSelectorResource",
        "policeSelectorResource",
        "avatarContentPoliceSelectorResource");

      if (!string.IsNullOrWhiteSpace(resourcePath))
      {
        ContentPoliceSelector resourceSelector = Resources.Load<ContentPoliceSelector>(resourcePath.Trim());
        if (resourceSelector != null)
        {
          return resourceSelector;
        }

        Debug.LogWarning(
          $"BasisAvatarLoader: Could not load ContentPoliceSelector from Resources path '{resourcePath}'.");
      }

      string inlineTypes = TryGetMetadataValue(
        descriptor,
        "contentPoliceSelectedTypes",
        "contentPoliceWhitelist",
        "contentPoliceTypes");

      if (string.IsNullOrWhiteSpace(inlineTypes))
      {
        return null;
      }

      string[] tokens = inlineTypes.Split(new[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries);
      var selector = ScriptableObject.CreateInstance<ContentPoliceSelector>();
      selector.name = "RuntimeContentPoliceSelector";
      for (int i = 0; i < tokens.Length; i++)
      {
        string typeName = tokens[i].Trim();
        if (typeName.Length == 0 || selector.selectedTypes.Contains(typeName))
        {
          continue;
        }

        selector.selectedTypes.Add(typeName);
      }

      return selector;
    }

    private static bool HasWhitelist(ContentPoliceSelector selector)
    {
      return selector != null
             && selector.selectedTypes != null
             && selector.selectedTypes.Count > 0;
    }

    private static string ResolveBeeSource(IAvatarDescriptor descriptor)
    {
      string source =
        descriptor.Uri
        ?? TryGetMetadataValue(descriptor, "beeUri", "beeUrl", "beePath", "uri", "url", "path", "source")
        ?? descriptor.AvatarId;

      if (string.IsNullOrWhiteSpace(source))
      {
        throw new InvalidOperationException(
          "Basis avatar descriptor is missing source URI/path. " +
          "Set descriptor.Uri or descriptor.Metadata[\"beePath\"/\"beeUrl\"].");
      }

      return source.Trim();
    }

    private static string ResolveUnlockPassword(IAvatarDescriptor descriptor)
    {
      string password = TryGetMetadataValue(
        descriptor,
        "unlockPassword",
        "password",
        "vp",
        "basisPassword",
        "beePassword");

      return string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }

    private static string TryGetMetadataValue(IAvatarDescriptor descriptor, params string[] keys)
    {
      IReadOnlyDictionary<string, string> metadata = descriptor.Metadata;
      if (metadata == null || keys == null || keys.Length == 0)
      {
        return null;
      }

      for (int i = 0; i < keys.Length; i++)
      {
        if (metadata.TryGetValue(keys[i], out string direct) && !string.IsNullOrWhiteSpace(direct))
        {
          return direct;
        }
      }

      foreach (KeyValuePair<string, string> kvp in metadata)
      {
        if (string.IsNullOrWhiteSpace(kvp.Value))
        {
          continue;
        }

        for (int i = 0; i < keys.Length; i++)
        {
          if (string.Equals(kvp.Key, keys[i], StringComparison.OrdinalIgnoreCase))
          {
            return kvp.Value;
          }
        }
      }

      return null;
    }

    private static bool IsHttpUrl(string value)
    {
      return Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
             && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveLocalBeePath(string source)
    {
      if (Uri.TryCreate(source, UriKind.Absolute, out Uri absoluteUri))
      {
        if (absoluteUri.IsFile)
        {
          string filePath = absoluteUri.LocalPath;
          if (File.Exists(filePath))
          {
            return filePath;
          }
        }
        else if (!string.IsNullOrWhiteSpace(absoluteUri.Scheme))
        {
          throw new InvalidOperationException(
            $"Unsupported avatar URI scheme '{absoluteUri.Scheme}'. " +
            "Use a local file path or an http/https URL.");
        }
      }

      var candidates = new List<string>();
      if (Path.IsPathRooted(source))
      {
        candidates.Add(source);
      }
      else
      {
        candidates.Add(Path.Combine(Application.streamingAssetsPath, source));
        candidates.Add(Path.Combine(Application.persistentDataPath, source));
        candidates.Add(Path.Combine(Application.dataPath, source));
        try
        {
          candidates.Add(Path.GetFullPath(source));
        }
        catch (Exception)
        {
          // Ignore invalid paths here; we'll throw a clear error below if none resolve.
        }
      }

      bool hasBeeExt = source.EndsWith(".bee", StringComparison.OrdinalIgnoreCase);
      if (!hasBeeExt)
      {
        int baseCount = candidates.Count;
        for (int i = 0; i < baseCount; i++)
        {
          candidates.Add(candidates[i] + ".bee");
        }
      }

      for (int i = 0; i < candidates.Count; i++)
      {
        if (File.Exists(candidates[i]))
        {
          return candidates[i];
        }
      }

      throw new FileNotFoundException(
        $"Could not resolve local Basis .bee file from '{source}'. " +
        "Checked absolute path, StreamingAssets, persistentDataPath, project Assets, and current directory.");
    }

    private static async Task<(BasisBundleConnector Connector, BasisBundleGenerated Generated, byte[] SectionBytes)>
      LoadFromRemoteBeeAsync(string url, string password, BasisProgressReport progress,
        CancellationToken cancellationToken)
    {
      BeeResult<BasisIOManagement.BeeDownloadResult> result = await BasisIOManagement.DownloadBEEEx(
        url,
        password,
        progress,
        cancellationToken);

      if (!result.IsSuccess || result.Value == null)
      {
        throw new InvalidOperationException(
          $"Failed to download Basis .bee from '{url}'. {result.Error ?? "Unknown error."}");
      }

      BasisBundleConnector connector = result.Value.Connector;
      if (connector == null)
      {
        throw new InvalidOperationException("Downloaded Basis .bee is missing connector metadata.");
      }

      if (!connector.GetPlatform(out BasisBundleGenerated generated) || generated == null)
      {
        throw new InvalidOperationException(
          $"Basis .bee does not contain a compatible platform bundle for '{Application.platform}'.");
      }

      byte[] section = result.Value.SectionData;
      if (section == null || section.Length == 0)
      {
        throw new InvalidOperationException("Downloaded Basis .bee did not return platform bundle bytes.");
      }

      return (connector, generated, section);
    }

    private static async Task<(BasisBundleConnector Connector, BasisBundleGenerated Generated, byte[] SectionBytes)>
      LoadFromLocalBeeAsync(string localBeePath, string password, BasisProgressReport progress,
        CancellationToken cancellationToken)
    {
      if (!File.Exists(localBeePath))
      {
        throw new FileNotFoundException($"Basis .bee file not found at '{localBeePath}'.");
      }

      cancellationToken.ThrowIfCancellationRequested();

      // Supports both Basis BEE formats:
      // - packaged/distribution format: [Int64 connectorLength][connector][sections...]
      // - cached disk format:          [Int32 connectorLength][connector][platformSection]
      using var stream = new FileStream(
        localBeePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 1024 * 64,
        useAsync: true);

      if (stream.Length < 8)
      {
        throw new InvalidDataException($"Basis .bee file is too small: '{localBeePath}'.");
      }

      byte[] first8 = await ReadExactAsync(stream, 8, cancellationToken);
      if (TryReadInt64LittleEndian(first8, out long connectorSize64)
          && connectorSize64 > 0
          && connectorSize64 <= int.MaxValue
          && connectorSize64 <= stream.Length - 8)
      {
        stream.Position = 8;
        byte[] connectorBytes = await ReadExactAsync(stream, (int)connectorSize64, cancellationToken);
        BasisBundleConnector connector =
          await BasisEncryptionToData.GenerateMetaFromBytes(password, connectorBytes, progress);

        string sectionError = connector == null ? "Connector decryption failed." : string.Empty;
        BasisBundleGenerated generated = null;
        long sectionStart = 0;
        int sectionLength = 0;

        if (connector != null && TryResolveRemoteFormatSectionRange(
              connector,
              connectorSize64,
              stream.Length,
              out generated,
              out sectionStart,
              out sectionLength,
              out sectionError))
        {
          stream.Position = sectionStart;
          byte[] sectionBytes = await ReadExactAsync(stream, sectionLength, cancellationToken);
          return (connector, generated, sectionBytes);
        }

        Debug.LogWarning(
          $"BasisAvatarLoader: Failed to parse '{localBeePath}' as packaged BEE format; trying cached format. " +
          $"Details: {sectionError}");
      }

      stream.Position = 0;
      byte[] first4 = await ReadExactAsync(stream, 4, cancellationToken);
      if (!TryReadInt32LittleEndian(first4, out int connectorSize32)
          || connectorSize32 <= 0
          || connectorSize32 > stream.Length - 4)
      {
        throw new InvalidDataException(
          $"Invalid Basis .bee header in '{localBeePath}'. " +
          "Expected either packaged (Int64) or cached (Int32) connector-size format.");
      }

      byte[] diskConnectorBytes = await ReadExactAsync(stream, connectorSize32, cancellationToken);
      BasisBundleConnector diskConnector =
        await BasisEncryptionToData.GenerateMetaFromBytes(password, diskConnectorBytes, progress);
      if (diskConnector == null)
      {
        throw new InvalidOperationException(
          $"Failed to decrypt connector metadata from local Basis .bee '{localBeePath}'.");
      }

      if (!diskConnector.GetPlatform(out BasisBundleGenerated diskGenerated) || diskGenerated == null)
      {
        throw new InvalidOperationException(
          $"Basis .bee does not contain a compatible platform bundle for '{Application.platform}'.");
      }

      long remaining = stream.Length - stream.Position;
      if (remaining <= 0 || remaining > int.MaxValue)
      {
        throw new InvalidDataException(
          $"Basis .bee local section size is invalid ({remaining} bytes) in '{localBeePath}'.");
      }

      byte[] diskSection = await ReadExactAsync(stream, (int)remaining, cancellationToken);
      return (diskConnector, diskGenerated, diskSection);
    }

    private static bool TryResolveRemoteFormatSectionRange(
      BasisBundleConnector connector,
      long connectorSize,
      long fileLength,
      out BasisBundleGenerated generated,
      out long sectionStart,
      out int sectionLength,
      out string error)
    {
      generated = null;
      sectionStart = 0;
      sectionLength = 0;
      error = string.Empty;

      if (connector == null || connector.BasisBundleGenerated == null || connector.BasisBundleGenerated.Length == 0)
      {
        error = "Connector has no generated bundle entries.";
        return false;
      }

      long cursor = 8 + connectorSize;
      for (int i = 0; i < connector.BasisBundleGenerated.Length; i++)
      {
        BasisBundleGenerated entry = connector.BasisBundleGenerated[i];
        if (entry == null)
        {
          error = $"Connector entry {i} is null.";
          return false;
        }

        long length = entry.EndByte;
        if (length <= 0)
        {
          error = $"Connector entry {i} has invalid section length '{length}'.";
          return false;
        }

        if (cursor + length > fileLength)
        {
          error = $"Connector entry {i} points past end-of-file.";
          return false;
        }

        if (BasisBundleConnector.IsPlatform(entry))
        {
          if (length > int.MaxValue)
          {
            error = $"Platform section is too large to load into memory ({length} bytes).";
            return false;
          }

          generated = entry;
          sectionStart = cursor;
          sectionLength = (int)length;
          return true;
        }

        cursor += length;
      }

      error = $"No compatible platform entry for '{Application.platform}'.";
      return false;
    }

    private static async Task<GameObject> LoadAvatarPrefabAsync(
      AssetBundle assetBundle,
      string assetToLoadName,
      CancellationToken cancellationToken)
    {
      string preferredPrefabName = ConvertBundleAssetToPrefabName(assetToLoadName);

      GameObject avatarPrefab = await LoadPrefabByNameAsync(assetBundle, preferredPrefabName, cancellationToken);
      if (avatarPrefab != null)
      {
        return avatarPrefab;
      }

      if (!string.Equals(preferredPrefabName, assetToLoadName, StringComparison.Ordinal))
      {
        avatarPrefab = await LoadPrefabByNameAsync(assetBundle, assetToLoadName, cancellationToken);
        if (avatarPrefab != null)
        {
          return avatarPrefab;
        }
      }

      string[] allAssets = assetBundle.GetAllAssetNames();
      for (int i = 0; i < allAssets.Length; i++)
      {
        if (!allAssets[i].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        avatarPrefab = await LoadPrefabByNameAsync(assetBundle, allAssets[i], cancellationToken);
        if (avatarPrefab != null)
        {
          return avatarPrefab;
        }
      }

      return null;
    }

    private static string ConvertBundleAssetToPrefabName(string assetToLoadName)
    {
      if (string.IsNullOrWhiteSpace(assetToLoadName))
      {
        return assetToLoadName;
      }

      if (assetToLoadName.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
      {
        return assetToLoadName.Substring(0, assetToLoadName.Length - ".bundle".Length) + ".prefab";
      }

      return assetToLoadName;
    }

    private static async Task<GameObject> LoadPrefabByNameAsync(
      AssetBundle assetBundle,
      string prefabName,
      CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(prefabName))
      {
        return null;
      }

      AssetBundleRequest request = assetBundle.LoadAssetAsync<GameObject>(prefabName);
      while (!request.isDone)
      {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        // await Task.Yield(PlayerLoopTiming.Update, cancellationToken);
      }

      return request.asset as GameObject;
    }

    private static async Task<byte[]> ReadExactAsync(FileStream stream, int length,
      CancellationToken cancellationToken)
    {
      if (length < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(length), "Read length must be non-negative.");
      }

      byte[] buffer = new byte[length];
      int offset = 0;
      while (offset < length)
      {
        cancellationToken.ThrowIfCancellationRequested();
        int read = await stream.ReadAsync(buffer, offset, length - offset);
        if (read <= 0)
        {
          throw new EndOfStreamException($"Unexpected end of stream. Wanted {length} bytes, got {offset}.");
        }

        offset += read;
      }

      return buffer;
    }

    private static bool TryReadInt32LittleEndian(byte[] bytes, out int value)
    {
      value = 0;
      if (bytes == null || bytes.Length < 4)
      {
        return false;
      }

      if (!BitConverter.IsLittleEndian)
      {
        bytes = (byte[])bytes.Clone();
        Array.Reverse(bytes);
      }

      value = BitConverter.ToInt32(bytes, 0);
      return true;
    }

    private static bool TryReadInt64LittleEndian(byte[] bytes, out long value)
    {
      value = 0;
      if (bytes == null || bytes.Length < 8)
      {
        return false;
      }

      if (!BitConverter.IsLittleEndian)
      {
        bytes = (byte[])bytes.Clone();
        Array.Reverse(bytes);
      }

      value = BitConverter.ToInt64(bytes, 0);
      return true;
    }

    private static bool ShouldUseContentPolice(IAvatarDescriptor descriptor)
    {
      return ReadMetadataBool(
        descriptor,
        defaultValue: true,
        "useContentPolice",
        "enableContentPolice",
        "contentPolice");
    }

    private static bool ShouldRemoveColliders(IAvatarDescriptor descriptor)
    {
      return ReadMetadataBool(
        descriptor,
        defaultValue: false,
        "contentPoliceRemoveColliders",
        "removeColliders");
    }

    private static bool ShouldDisableAnimator(IAvatarDescriptor descriptor)
    {
      return ReadMetadataBool(descriptor, defaultValue: true, "disableAnimator");
    }

    private static bool ReadMetadataBool(IAvatarDescriptor descriptor, bool defaultValue, params string[] keys)
    {
      string raw = TryGetMetadataValue(descriptor, keys);
      if (string.IsNullOrWhiteSpace(raw))
      {
        return defaultValue;
      }

      if (string.Equals(raw, "1", StringComparison.Ordinal))
      {
        return true;
      }

      if (string.Equals(raw, "0", StringComparison.Ordinal))
      {
        return false;
      }

      return bool.TryParse(raw, out bool parsed) ? parsed : defaultValue;
    }

    private static string BuildAvatarName(IAvatarDescriptor descriptor, BasisBundleConnector connector,
      string fallbackName)
    {
      string id =
        !string.IsNullOrWhiteSpace(descriptor.AvatarId) ? descriptor.AvatarId
        : connector != null && !string.IsNullOrWhiteSpace(connector.UniqueVersion) ? connector.UniqueVersion
        : fallbackName;

      return $"BasisAvatar_{id}";
    }

  }
}