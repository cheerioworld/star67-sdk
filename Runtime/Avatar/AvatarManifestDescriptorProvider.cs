using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Star67.Avatar
{
  public class AvatarManifestDescriptorProvider
  {
    public const string DefaultManifestUrl =
      "https://star67-staging-usercontent.s3.us-west-2.amazonaws.com/avatars/sandbox/avatar_manifest.json";

    public const string DefaultContentOrigin =
      "https://star67-staging-usercontent.s3.us-west-2.amazonaws.com";

    public const string PasswordMetadataKey = "password";
    public const string DefaultPassword = "supereasypassword";

    private readonly string _manifestUrl;
    private readonly string _contentOrigin;

    public AvatarManifestDescriptorProvider(
      string manifestUrl = DefaultManifestUrl,
      string contentOrigin = DefaultContentOrigin)
    {
      if (string.IsNullOrWhiteSpace(manifestUrl))
      {
        throw new ArgumentException("Manifest URL cannot be null or empty.", nameof(manifestUrl));
      }

      if (string.IsNullOrWhiteSpace(contentOrigin))
      {
        throw new ArgumentException("Content origin cannot be null or empty.", nameof(contentOrigin));
      }

      _manifestUrl = manifestUrl.Trim();
      _contentOrigin = contentOrigin.Trim().TrimEnd('/');
    }

    public async Task<AvatarDescriptor[]> FetchAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      string json = await FetchManifestJsonAsync(cancellationToken);
      AvatarManifest manifest = ParseManifest(json);
      return BuildDescriptors(manifest);
    }

    private async Task<string> FetchManifestJsonAsync(CancellationToken cancellationToken)
    {
      using UnityWebRequest request = UnityWebRequest.Get(_manifestUrl);
      UnityWebRequestAsyncOperation operation = request.SendWebRequest();

      using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(request.Abort);

      while (!operation.isDone)
      {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
      }

      cancellationToken.ThrowIfCancellationRequested();

      if (request.result != UnityWebRequest.Result.Success)
      {
        throw new InvalidOperationException(
          $"Failed to fetch avatar manifest from '{_manifestUrl}'. " +
          $"ResponseCode={request.responseCode}, Error='{request.error}'.");
      }

      string text = request.downloadHandler?.text;
      if (string.IsNullOrWhiteSpace(text))
      {
        throw new InvalidOperationException($"Avatar manifest fetched from '{_manifestUrl}' was empty.");
      }

      return text;
    }

    private AvatarManifest ParseManifest(string json)
    {
      try
      {
        AvatarManifest manifest = JsonUtility.FromJson<AvatarManifest>(json);
        if (manifest?.avatars == null)
        {
          throw new InvalidOperationException("Avatar manifest is missing the required 'avatars' array.");
        }

        return manifest;
      }
      catch (Exception exception) when (exception is not InvalidOperationException)
      {
        throw new InvalidOperationException(
          $"Failed to parse avatar manifest from '{_manifestUrl}'.",
          exception);
      }
    }

    private AvatarDescriptor[] BuildDescriptors(AvatarManifest manifest)
    {
      AvatarDescriptor[] descriptors = new AvatarDescriptor[manifest.avatars.Length];

      for (int i = 0; i < manifest.avatars.Length; i++)
      {
        AvatarManifestEntry entry = manifest.avatars[i];
        if (entry == null)
        {
          throw new InvalidOperationException($"Avatar manifest entry at index {i} was null.");
        }

        if (string.IsNullOrWhiteSpace(entry.path))
        {
          throw new InvalidOperationException($"Avatar manifest entry at index {i} is missing a path.");
        }

        if (string.IsNullOrWhiteSpace(entry.name))
        {
          throw new InvalidOperationException($"Avatar manifest entry at index {i} is missing a name.");
        }

        descriptors[i] = new AvatarDescriptor
        {
          Type = AvatarType.Basis,
          AvatarId = entry.name.Trim(),
          Uri = BuildAvatarUri(entry.path),
          Metadata = new Dictionary<string, string>
          {
            { PasswordMetadataKey, DefaultPassword }
          }
        };
      }

      return descriptors;
    }

    private string BuildAvatarUri(string manifestPath)
    {
      return _contentOrigin + "/" + manifestPath.Trim().TrimStart('/');
    }

    [Serializable]
    private sealed class AvatarManifest
    {
      public AvatarManifestEntry[] avatars;
    }

    [Serializable]
    private sealed class AvatarManifestEntry
    {
      public string path;
      public string name;
    }
  }
}
