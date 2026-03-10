using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Star67;
using Star67.Avatar;
using UnityEngine;

namespace Star67.Sdk.Samples
{
  public class SampleAvatarLoader : MonoBehaviour
  {
    [SerializeField] private string filePath;

    private string resolvedPath;

    private IAvatar _avatar;

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
      _avatar = await new Star67AvatarLoader().LoadAvatarAsync(descriptor, transform, new CancellationToken());
      Debug.Log($"Loaded avatar: {_avatar.Descriptor.Uri}");
      // var (basisBundleConnector, basisBundleGenerated, sectionBytes) = await LoadFromLocalBeeAsync(resolvedPath, "test",
      //   new BasisProgressReport(), new CancellationToken());
    }
  }
}