using System;
using Star67.Tracking.Unity;
using UnityEditor;
using UnityEngine;

namespace Star67.Tracking.Editor
{
    public static class TrackingPreviewEditorUtilities
    {
        public static EditorPreviewManager EnsureStar67PreviewSetup(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            EditorPreviewWindow.OpenWindowForRoot(root);
            if (!EditorApplication.isPlaying)
            {
                return null;
            }

            EditorPreviewManager.TryResolveOrCreateForPlayMode(out EditorPreviewManager compositionRoot, out _);
            return compositionRoot;
        }
    }
}
