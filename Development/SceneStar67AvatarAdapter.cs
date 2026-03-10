using System;
using System.Collections.Generic;
using UnityEngine;

namespace Star67.Sdk.Avatar
{
    public sealed class SceneStar67AvatarAdapter : IAvatar
    {
        private readonly global::Star67.Sdk.Star67Avatar _avatar;
        private readonly AvatarDescriptor _descriptor;
        private IList<SkinnedMeshRenderer> _faceTrackingRenderers;

        public SceneStar67AvatarAdapter(global::Star67.Sdk.Star67Avatar avatar)
        {
            _avatar = avatar != null ? avatar : throw new ArgumentNullException(nameof(avatar));
            _descriptor = new AvatarDescriptor
            {
                Type = AvatarType.Basis,
                AvatarId = avatar.name,
                Uri = avatar.gameObject.scene.path,
                Metadata = new Dictionary<string, string>()
            };

            Rig = new global::Star67.AvatarRootRig(avatar.transform);
        }

        public global::Star67.Sdk.Star67Avatar AvatarComponent => _avatar;
        public bool IsValid => _avatar != null;
        public string AvatarName => _avatar != null ? _avatar.name : string.Empty;
        public IAvatarDescriptor Descriptor => _descriptor;
        public IAvatarRig Rig { get; }

        public IList<SkinnedMeshRenderer> FaceTrackingRenderers
        {
            get
            {
                if (_faceTrackingRenderers != null)
                {
                    return _faceTrackingRenderers;
                }

                var renderers = new List<SkinnedMeshRenderer>();
                AddRenderer(renderers, _avatar.FaceVisemeMesh);
                AddRenderer(renderers, _avatar.FaceBlinkMesh);

                if (renderers.Count == 0)
                {
                    SkinnedMeshRenderer[] childRenderers = _avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    for (int i = 0; i < childRenderers.Length; i++)
                    {
                        AddRenderer(renderers, childRenderers[i]);
                    }
                }

                _faceTrackingRenderers = renderers;
                return _faceTrackingRenderers;
            }
        }

        public void Dispose()
        {
        }

        public static SceneStar67AvatarAdapter TryCreateFirstInScene(out int avatarCount)
        {
            global::Star67.Sdk.Star67Avatar[] avatars = GameObject.FindObjectsByType<global::Star67.Sdk.Star67Avatar>(FindObjectsSortMode.None);
            avatarCount = avatars == null ? 0 : avatars.Length;
            if (avatarCount == 0)
            {
                return null;
            }

            return new SceneStar67AvatarAdapter(avatars[0]);
        }

        private static void AddRenderer(List<SkinnedMeshRenderer> renderers, SkinnedMeshRenderer renderer)
        {
            if (renderer != null && !renderers.Contains(renderer))
            {
                renderers.Add(renderer);
            }
        }
    }
}
