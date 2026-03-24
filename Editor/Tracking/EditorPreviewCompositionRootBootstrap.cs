using Star67.Tracking.Unity;
using UnityEditor;

namespace Star67.Tracking.Editor
{
    [InitializeOnLoad]
    public static class EditorPreviewCompositionRootBootstrap
    {
        static EditorPreviewCompositionRootBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall -= EnsureCompositionRootAfterPlayModeEntry;
                EditorApplication.delayCall += EnsureCompositionRootAfterPlayModeEntry;
                return;
            }

            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall -= EnsureCompositionRootAfterPlayModeEntry;
            }
        }

        private static void EnsureCompositionRootAfterPlayModeEntry()
        {
            EditorApplication.delayCall -= EnsureCompositionRootAfterPlayModeEntry;

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            EditorPreviewManager.TryResolveOrCreateForPlayMode(out _, out _);
        }
    }
}
