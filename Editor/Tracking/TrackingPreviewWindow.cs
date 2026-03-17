using System;
using System.IO;
using System.Linq;
using System.Net;
using Star67.Tracking.Unity;
using UnityEditor;
using UnityEngine;

namespace Star67.Tracking.Editor
{
    public sealed class TrackingPreviewWindow : EditorWindow
    {
        private const double ActiveRepaintIntervalSeconds = 0.2d;
        private const string RecordingPathKey = "Star67.Tracking.Editor.RecordingPath";

        [SerializeField] private EditorPreviewCompositionRoot compositionRoot;
        [SerializeField] private TrackingPreviewController previewController;
        [SerializeField] private string recordingPath;
        [SerializeField] private bool loopPlayback;

        private string playModeStatus;
        private bool editorTickRegistered;
        private double nextRepaintAt;
        private double nextNetworkSnapshotAt;
        private UdpTrackingSession liveSession;
        private TrackingDiscoveryService discoveryService;
        private TrackingRecordingWriter recordingWriter;
        private TrackingRecordingPlayer playbackPlayer;
        private string[] cachedLocalIPv4Addresses = Array.Empty<string>();
        private DiscoveryRegistryEntry[] cachedDiscoveryAnnouncements = Array.Empty<DiscoveryRegistryEntry>();

        [MenuItem("Window/Star67/Tracking Preview")]
        public static void OpenWindow()
        {
            GetWindow<TrackingPreviewWindow>("Tracking Preview").Show();
        }

        public static void OpenWindow(TrackingPreviewController controller)
        {
            TrackingPreviewWindow window = GetWindow<TrackingPreviewWindow>("Tracking Preview");
            window.compositionRoot = controller != null ? controller.GetComponent<EditorPreviewCompositionRoot>() : null;
            window.previewController = controller;
            window.Show();
            window.Focus();
        }

        public static void OpenWindow(EditorPreviewCompositionRoot root)
        {
            TrackingPreviewWindow window = GetWindow<TrackingPreviewWindow>("Tracking Preview");
            window.compositionRoot = root;
            window.previewController = root != null ? root.PreviewController : null;
            window.playModeStatus = root != null ? root.StatusMessage : null;
            window.Show();
            window.Focus();
        }

        public static void OpenWindowForRoot(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            TrackingPreviewWindow window = GetWindow<TrackingPreviewWindow>("Tracking Preview");
            window.Show();
            window.Focus();

            if (EditorApplication.isPlaying)
            {
                window.QueueCompositionRootResolve();
            }
        }

        private void OnEnable()
        {
            cachedLocalIPv4Addresses = GetLocalIPv4Addresses();
            cachedDiscoveryAnnouncements = Array.Empty<DiscoveryRegistryEntry>();
            recordingPath = EditorPrefs.GetString(RecordingPathKey, GetDefaultRecordingPath());
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ResetPlayModeResolutionState();
            RefreshEditorTickRegistration();

            if (EditorApplication.isPlaying)
            {
                QueueCompositionRootResolve();
            }
        }

        private void OnDisable()
        {
            SetEditorTickRegistered(false);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            StopRecording();
            StopPlayback();
            StopLive();
            ResetPlayModeResolutionState();
        }

        private void OnGUI()
        {
            DrawControllerSelection();
            DrawLiveSection();
            EditorGUILayout.Space();
            DrawRecordingSection();
            EditorGUILayout.Space();
            DrawPlaybackSection();
        }

        private void DrawControllerSelection()
        {
            EditorGUILayout.LabelField("Preview Target", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorPreviewCompositionRoot newRoot = (EditorPreviewCompositionRoot)EditorGUILayout.ObjectField(
                "Composition Root",
                compositionRoot,
                typeof(EditorPreviewCompositionRoot),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                compositionRoot = newRoot;
                previewController = compositionRoot != null ? compositionRoot.PreviewController : null;
                playModeStatus = compositionRoot != null ? compositionRoot.StatusMessage : playModeStatus;
                ApplyActiveSourceToPreviewController();
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Controller", previewController, typeof(TrackingPreviewController), true);
            }

            if (EditorApplication.isPlaying)
            {
                if (!string.IsNullOrEmpty(playModeStatus))
                {
                    EditorGUILayout.HelpBox(playModeStatus, compositionRoot == null ? MessageType.Warning : MessageType.Info);
                }

                if (compositionRoot == null)
                {
                    EditorGUILayout.HelpBox(
                        "A play-mode EditorPreviewCompositionRoot will be created automatically and bind to the first Star67Avatar found in loaded scenes.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "The resolved composition root owns the shared preview rig, preview controller, and reusable face blendshape driver.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to apply live or recorded tracking through the play-mode preview composition root.",
                    MessageType.Info);
            }
        }

        private void DrawLiveSection()
        {
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("IPv4", string.Join(", ", cachedLocalIPv4Addresses), GUILayout.MaxWidth(position.width - 120f));
            }

            using (new EditorGUI.DisabledScope(IsPreviewUnavailableInPlayMode()))
            {
                if (liveSession == null)
                {
                    if (GUILayout.Button("Start Listening"))
                    {
                        if (EnsurePlayMode("Start live preview"))
                        {
                            StartLive();
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Listening"))
                    {
                        StopLive();
                    }
                }
            }

            EditorGUILayout.LabelField("State", liveSession?.State.ToString() ?? TrackingConnectionState.Stopped.ToString());
            EditorGUILayout.LabelField("Data Port", TrackingProtocol.DataPort.ToString());
            EditorGUILayout.LabelField("Discovery Port", TrackingProtocol.DiscoveryPort.ToString());
            if (liveSession != null)
            {
                EditorGUILayout.LabelField("Session Token", liveSession.SessionToken.ToString());
                EditorGUILayout.LabelField("Device", string.IsNullOrEmpty(liveSession.SessionInfo.DeviceName) ? "(waiting)" : liveSession.SessionInfo.DeviceName);
                EditorGUILayout.LabelField("App Version", string.IsNullOrEmpty(liveSession.SessionInfo.AppVersion) ? "(waiting)" : liveSession.SessionInfo.AppVersion);
                EditorGUILayout.LabelField("Features", liveSession.SessionInfo.AvailableFeatures.ToString());
            }

            if (discoveryService != null)
            {
                EditorGUILayout.LabelField("Discovered Apps", cachedDiscoveryAnnouncements.Length.ToString());
                for (int i = 0; i < cachedDiscoveryAnnouncements.Length; i++)
                {
                    DiscoveryRegistryEntry entry = cachedDiscoveryAnnouncements[i];
                    EditorGUILayout.LabelField($"- {entry.Announcement.DeviceName}", $"{entry.RemoteEndPoint.Address}:{entry.Announcement.DataPort}");
                }
            }
        }

        private void DrawRecordingSection()
        {
            EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                recordingPath = EditorGUILayout.TextField("File", recordingPath);
                if (GUILayout.Button("...", GUILayout.Width(32f)))
                {
                    string chosenPath = EditorUtility.SaveFilePanel("Save tracking recording", Path.GetDirectoryName(recordingPath), Path.GetFileName(recordingPath), "s67trk");
                    if (!string.IsNullOrEmpty(chosenPath))
                    {
                        recordingPath = chosenPath;
                        EditorPrefs.SetString(RecordingPathKey, recordingPath);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(liveSession == null))
            {
                if (recordingWriter == null)
                {
                    if (GUILayout.Button("Start Recording"))
                    {
                        StartRecording();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Recording"))
                    {
                        StopRecording();
                    }
                }
            }

            EditorGUILayout.LabelField("Dropped Recording Frames", recordingWriter?.DroppedFrameCount.ToString() ?? "0");
        }

        private void DrawPlaybackSection()
        {
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(IsPreviewUnavailableInPlayMode()))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Recording"))
                    {
                        string chosenPath = EditorUtility.OpenFilePanel("Open tracking recording", Path.GetDirectoryName(recordingPath), "s67trk");
                        if (!string.IsNullOrEmpty(chosenPath))
                        {
                            recordingPath = chosenPath;
                            EditorPrefs.SetString(RecordingPathKey, recordingPath);
                            if (EnsurePlayMode("Open recording playback"))
                            {
                                OpenPlayback(chosenPath);
                            }
                        }
                    }

                    using (new EditorGUI.DisabledScope(playbackPlayer == null))
                    {
                        if (GUILayout.Button(playbackPlayer != null && playbackPlayer.IsPlaying ? "Pause" : "Play"))
                        {
                            TogglePlayback();
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(playbackPlayer == null))
            {
                bool newLoop = EditorGUILayout.Toggle("Loop", loopPlayback);
                if (newLoop != loopPlayback)
                {
                    loopPlayback = newLoop;
                    if (playbackPlayer != null)
                    {
                        playbackPlayer.Loop = loopPlayback;
                    }
                }

                float duration = playbackPlayer?.DurationSeconds ?? 0f;
                float currentTime = playbackPlayer?.CurrentTimeSeconds ?? 0f;
                float normalized = duration > 0f ? currentTime / duration : 0f;
                EditorGUI.BeginChangeCheck();
                float newNormalized = EditorGUILayout.Slider("Seek", normalized, 0f, 1f);
                if (EditorGUI.EndChangeCheck() && playbackPlayer != null)
                {
                    playbackPlayer.Seek(newNormalized);
                }

                EditorGUILayout.LabelField("Current Time", currentTime.ToString("0.000"));
                EditorGUILayout.LabelField("Duration", duration.ToString("0.000"));
            }
        }

        private void EditorTick()
        {
            if (!NeedsPeriodicRefresh())
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now >= nextNetworkSnapshotAt)
            {
                nextNetworkSnapshotAt = now + 1d;
                cachedDiscoveryAnnouncements = discoveryService != null
                    ? discoveryService.Registry.Snapshot(TimeSpan.FromSeconds(5))
                    : Array.Empty<DiscoveryRegistryEntry>();
            }

            if (now < nextRepaintAt)
            {
                return;
            }

            nextRepaintAt = now + ActiveRepaintIntervalSeconds;
            Repaint();
        }

        private void StartLive()
        {
            StopPlayback();
            StopLive();
            if (!EnsurePreviewControllerForPlayMode())
            {
                return;
            }

            liveSession = new UdpTrackingSession();
            liveSession.Start();

            discoveryService = new TrackingDiscoveryService(new DiscoveryAnnouncement
            {
                Role = DiscoveryRole.Editor,
                SessionToken = liveSession.SessionToken,
                DeviceName = Environment.MachineName,
                DataPort = TrackingProtocol.DataPort,
                PackageVersion = "com.cheerioworld.star67.sdk",
                AvailableFeatures = TrackingFeatureFlags.Face | TrackingFeatureFlags.HeadPose | TrackingFeatureFlags.CameraWorldPose | TrackingFeatureFlags.LeftHand | TrackingFeatureFlags.RightHand
            });
            discoveryService.Start();
            cachedDiscoveryAnnouncements = Array.Empty<DiscoveryRegistryEntry>();
            ApplyActiveSourceToPreviewController();
            RefreshEditorTickRegistration();
            Repaint();
        }

        private void StopLive()
        {
            if (previewController != null && liveSession != null && ReferenceEquals(previewController.Source, liveSession))
            {
                previewController.SetSource(null);
            }

            if (recordingWriter != null)
            {
                StopRecording();
            }

            discoveryService?.Dispose();
            discoveryService = null;
            cachedDiscoveryAnnouncements = Array.Empty<DiscoveryRegistryEntry>();
            liveSession?.Dispose();
            liveSession = null;
            RefreshEditorTickRegistration();
            Repaint();
        }

        private void StartRecording()
        {
            if (liveSession == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(recordingPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            recordingWriter = new TrackingRecordingWriter();
            recordingWriter.Start(recordingPath, new TrackingRecordingHeader
            {
                SessionToken = liveSession.SessionToken,
                SessionInfo = liveSession.SessionInfo.Clone()
            });
            liveSession.PacketSink = recordingWriter;
            Repaint();
        }

        private void StopRecording()
        {
            if (liveSession != null)
            {
                liveSession.PacketSink = null;
            }

            recordingWriter?.Dispose();
            recordingWriter = null;
            Repaint();
        }

        private void OpenPlayback(string path)
        {
            StopLive();
            StopPlayback();
            if (!EnsurePreviewControllerForPlayMode())
            {
                return;
            }

            playbackPlayer = new TrackingRecordingPlayer(path)
            {
                Loop = loopPlayback
            };

            if (previewController != null)
            {
                previewController.SetSource(playbackPlayer);
            }

            RefreshEditorTickRegistration();
            Repaint();
        }

        private void StopPlayback()
        {
            if (previewController != null && playbackPlayer != null && ReferenceEquals(previewController.Source, playbackPlayer))
            {
                previewController.SetSource(null);
            }

            playbackPlayer?.Dispose();
            playbackPlayer = null;
            RefreshEditorTickRegistration();
            Repaint();
        }

        private void TogglePlayback()
        {
            if (!EnsurePreviewControllerForPlayMode())
            {
                return;
            }

            if (playbackPlayer == null)
            {
                if (File.Exists(recordingPath))
                {
                    OpenPlayback(recordingPath);
                    playbackPlayer?.Play();
                }

                return;
            }

            if (playbackPlayer.IsPlaying)
            {
                playbackPlayer.Pause();
            }
            else
            {
                playbackPlayer.Play();
            }
        }

        private static string[] GetLocalIPv4Addresses()
        {
            try
            {
                return Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(address => address.ToString())
                    .Distinct()
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string GetDefaultRecordingPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "star67-preview.s67trk");
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                StopRecording();
                StopPlayback();
                StopLive();
                ResetPlayModeResolutionState();
                Repaint();
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ResetPlayModeResolutionState();
                QueueCompositionRootResolve();
                Repaint();
            }
        }

        private bool EnsurePreviewControllerForPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            if (!EnsureCompositionRootForPlayMode())
            {
                return false;
            }

            previewController = compositionRoot != null ? compositionRoot.PreviewController : null;
            ApplyActiveSourceToPreviewController();
            return previewController != null;
        }

        private bool EnsureCompositionRootForPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            if (compositionRoot != null)
            {
                previewController = compositionRoot.PreviewController;
                if (!string.IsNullOrEmpty(compositionRoot.StatusMessage))
                {
                    playModeStatus = compositionRoot.StatusMessage;
                }

                return true;
            }

            return ResolveCompositionRoot(createIfNeeded: true);
        }

        private bool ResolveCompositionRoot(bool createIfNeeded)
        {
            if (!EditorApplication.isPlaying)
            {
                compositionRoot = null;
                previewController = null;
                playModeStatus = null;
                return false;
            }

            EditorPreviewCompositionRoot resolvedRoot = EditorPreviewCompositionRoot.FindActive();
            string resolvedStatusMessage = resolvedRoot != null ? resolvedRoot.StatusMessage : null;
            if (resolvedRoot == null && createIfNeeded)
            {
                EditorPreviewCompositionRoot.TryResolveOrCreateForPlayMode(out resolvedRoot, out resolvedStatusMessage);
            }

            compositionRoot = resolvedRoot;
            previewController = compositionRoot != null ? compositionRoot.PreviewController : null;
            playModeStatus = !string.IsNullOrEmpty(resolvedStatusMessage)
                ? resolvedStatusMessage
                : compositionRoot != null
                    ? compositionRoot.StatusMessage
                    : "No Star67Avatar found in loaded scenes.";
            ApplyActiveSourceToPreviewController();
            return compositionRoot != null;
        }

        private void ResetPlayModeResolutionState()
        {
            compositionRoot = null;
            previewController = null;
            playModeStatus = EditorApplication.isPlaying
                ? "Resolving play-mode preview composition root..."
                : null;
        }

        private bool IsPreviewUnavailableInPlayMode()
        {
            return EditorApplication.isPlaying && compositionRoot == null;
        }

        private void QueueCompositionRootResolve()
        {
            EditorApplication.delayCall -= DelayedResolveCompositionRoot;
            EditorApplication.delayCall += DelayedResolveCompositionRoot;
        }

        private void DelayedResolveCompositionRoot()
        {
            EditorApplication.delayCall -= DelayedResolveCompositionRoot;

            if (this == null || !EditorApplication.isPlaying)
            {
                return;
            }

            ResolveCompositionRoot(createIfNeeded: true);
            Repaint();
        }

        private void ApplyActiveSourceToPreviewController()
        {
            previewController = compositionRoot != null ? compositionRoot.PreviewController : previewController;
            if (previewController == null || !EditorApplication.isPlaying)
            {
                return;
            }

            if (playbackPlayer != null)
            {
                if (!ReferenceEquals(previewController.Source, playbackPlayer))
                {
                    previewController.SetSource(playbackPlayer);
                }

                return;
            }

            if (liveSession != null)
            {
                if (!ReferenceEquals(previewController.Source, liveSession))
                {
                    previewController.SetSource(liveSession);
                }

                return;
            }

            if (previewController.Source != null)
            {
                previewController.SetSource(null);
            }
        }

        private bool NeedsPeriodicRefresh()
        {
            return discoveryService != null || playbackPlayer != null || liveSession != null;
        }

        private void RefreshEditorTickRegistration()
        {
            SetEditorTickRegistered(NeedsPeriodicRefresh());
        }

        private void SetEditorTickRegistered(bool shouldRegister)
        {
            if (editorTickRegistered == shouldRegister)
            {
                return;
            }

            if (shouldRegister)
            {
                nextRepaintAt = 0d;
                nextNetworkSnapshotAt = 0d;
                EditorApplication.update += EditorTick;
            }
            else
            {
                EditorApplication.update -= EditorTick;
            }

            editorTickRegistered = shouldRegister;
        }

        private static bool EnsurePlayMode(string actionLabel)
        {
            if (EditorApplication.isPlaying)
            {
                return true;
            }

            EditorUtility.DisplayDialog("Play Mode Required", $"{actionLabel} requires Unity Play Mode.", "OK");
            return false;
        }
    }
}
