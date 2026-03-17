using System;
using Star67.Tracking.Unity;
using UnityEditor;
using UnityEngine;

namespace Star67.Tracking.Editor
{
    public static class TrackingPreviewEditorUtilities
    {
        public static EditorPreviewCompositionRoot EnsureStar67PreviewSetup(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            TrackingPreviewWindow.OpenWindowForRoot(root);
            if (!EditorApplication.isPlaying)
            {
                return null;
            }

            EditorPreviewCompositionRoot.TryResolveOrCreateForPlayMode(out EditorPreviewCompositionRoot compositionRoot, out _);
            return compositionRoot;
        }
    }
}
