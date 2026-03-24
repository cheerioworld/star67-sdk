using System;
using System.IO;
using System.Net;
using Star67.Tracking.Unity;
using UnityEditor;
using UnityEngine;

namespace Star67.Tracking.Editor
{
    public sealed class EditorPreviewWindow : EditorWindow
    {
        private const double ActiveRepaintIntervalSeconds = 0.2d;
        private const string RecordingPathKey = "Star67.Tracking.Editor.RecordingPath";
        private const string SenderIpAddressKey = "Star67.Tracking.Editor.SenderIpAddress";

        [SerializeField] private EditorPreviewManager manager;
        [SerializeField] private string recordingPath;
        [SerializeField] private string senderIpAddress;
        [SerializeField] private bool loopPlayback;

        private string playModeStatus;
        private bool editorTickRegistered;
        private double nextRepaintAt;
        private UdpTrackingReceiverClient liveClient;
        private TrackingRecordingWriter recordingWriter;
        private TrackingRecordingPlayer playbackPlayer;
        private string[] cachedLocalIPv4Addresses = Array.Empty<string>();

        [MenuItem("Window/Star67/Tracking Preview")]
        public static void OpenWindow()
        {
            GetWindow<EditorPreviewWindow>("Tracking Preview").Show();
        }

        public static void OpenWindow(EditorPreviewManager root)
        {
            EditorPreviewWindow window = GetWindow<EditorPreviewWindow>("Tracking Preview");
            window.manager = root;
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

            EditorPreviewWindow window = GetWindow<EditorPreviewWindow>("Tracking Preview");
            window.Show();
            window.Focus();

            if (EditorApplication.isPlaying)
            {
                window.QueueCompositionRootResolve();
            }
        }

        private void OnEnable()
        {
            cachedLocalIPv4Addresses = TrackingNetworkUtilities.GetLocalIPv4Addresses();
            recordingPath = EditorPrefs.GetString(RecordingPathKey, GetDefaultRecordingPath());
            senderIpAddress = EditorPrefs.GetString(SenderIpAddressKey, string.Empty);
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
            EditorPreviewManager newRoot = (EditorPreviewManager)EditorGUILayout.ObjectField(
                "Composition Root",
                manager,
                typeof(EditorPreviewManager),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                manager = newRoot;
                playModeStatus = manager != null ? manager.StatusMessage : playModeStatus;
                ApplyActiveSourceToManager();
            }

            if (EditorApplication.isPlaying)
            {
                if (!string.IsNullOrEmpty(playModeStatus))
                {
                    EditorGUILayout.HelpBox(playModeStatus, manager == null ? MessageType.Warning : MessageType.Info);
                }

                if (manager == null)
                {
                    EditorGUILayout.HelpBox(
                        "A play-mode EditorPreviewManager will be created automatically and bind to the first Star67Avatar found in loaded scenes.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "The resolved preview manager owns the shared preview rig and tracking pipeline for the active authored avatar.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to apply live or recorded tracking through the play-mode preview manager.",
                    MessageType.Info);
            }
        }

        private void DrawLiveSection()
        {
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Receiver IPv4", string.Join(", ", cachedLocalIPv4Addresses), GUILayout.MaxWidth(position.width - 120f));
            }

            using (new EditorGUI.DisabledScope(liveClient != null))
            {
                EditorGUI.BeginChangeCheck();
                string newSenderIpAddress = EditorGUILayout.TextField("Sender IP", senderIpAddress ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                {
                    senderIpAddress = (newSenderIpAddress ?? string.Empty).Trim();
                    EditorPrefs.SetString(SenderIpAddressKey, senderIpAddress);
                }
            }

            bool hasValidSenderIp = TryGetConfiguredSenderAddress(out _, out string senderIpValidationMessage);
            if (!hasValidSenderIp)
            {
                EditorGUILayout.HelpBox(senderIpValidationMessage, MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(IsPreviewUnavailableInPlayMode() || !hasValidSenderIp))
            {
                if (liveClient == null)
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

            EditorGUILayout.LabelField("State", liveClient?.State.ToString() ?? TrackingConnectionState.Stopped.ToString());
            EditorGUILayout.LabelField("Data Port", TrackingProtocol.DataPort.ToString());
            EditorGUILayout.LabelField("Control Port", TrackingProtocol.ControlPort.ToString());
            if (liveClient != null)
            {
                EditorGUILayout.LabelField("Session Token", liveClient.SessionToken.ToString());
                EditorGUILayout.LabelField("Sender Endpoint", liveClient.RemoteEndPoint != null ? liveClient.RemoteEndPoint.ToString() : "(waiting)");
                EditorGUILayout.LabelField("Device", string.IsNullOrEmpty(liveClient.SessionInfo.DeviceName) ? "(waiting)" : liveClient.SessionInfo.DeviceName);
                EditorGUILayout.LabelField("App Version", string.IsNullOrEmpty(liveClient.SessionInfo.AppVersion) ? "(waiting)" : liveClient.SessionInfo.AppVersion);
                EditorGUILayout.LabelField("Features", liveClient.SessionInfo.AvailableFeatures.ToString());
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

            using (new EditorGUI.DisabledScope(liveClient == null))
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
            if (now < nextRepaintAt)
            {
                return;
            }

            nextRepaintAt = now + ActiveRepaintIntervalSeconds;
            Repaint();
        }

        private void StartLive()
        {
            if (!TryGetConfiguredSenderAddress(out IPAddress configuredSenderAddress, out _))
            {
                return;
            }

            StopPlayback();
            StopLive();
            if (!EnsurePreviewManagerForPlayMode())
            {
                return;
            }

            try
            {
                liveClient = new UdpTrackingReceiverClient(configuredSenderAddress);
                liveClient.Start();
            }
            catch (Exception exception)
            {
                liveClient?.Dispose();
                liveClient = null;
                EditorUtility.DisplayDialog("Failed to Start Live Preview", exception.Message, "OK");
                return;
            }

            ApplyActiveSourceToManager();
            RefreshEditorTickRegistration();
            Repaint();
        }

        private void StopLive()
        {
            if (manager != null && liveClient != null)
            {
                manager.SetSource(null);
            }

            if (recordingWriter != null)
            {
                StopRecording();
            }

            liveClient?.Dispose();
            liveClient = null;
            RefreshEditorTickRegistration();
            Repaint();
        }

        private void StartRecording()
        {
            if (liveClient == null)
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
                SessionToken = liveClient.SessionToken,
                SessionInfo = liveClient.SessionInfo.Clone()
            });
            liveClient.PacketSink = recordingWriter;
            Repaint();
        }

        private void StopRecording()
        {
            if (liveClient != null)
            {
                liveClient.PacketSink = null;
            }

            recordingWriter?.Dispose();
            recordingWriter = null;
            Repaint();
        }

        private void OpenPlayback(string path)
        {
            StopLive();
            StopPlayback();
            if (!EnsurePreviewManagerForPlayMode())
            {
                return;
            }

            playbackPlayer = new TrackingRecordingPlayer(path)
            {
                Loop = loopPlayback
            };

            if (manager != null)
            {
                manager.SetSource(playbackPlayer);
            }

            RefreshEditorTickRegistration();
            Repaint();
        }

        private void StopPlayback()
        {
            if (manager != null && playbackPlayer != null)
            {
                manager.SetSource(null);
            }

            playbackPlayer?.Dispose();
            playbackPlayer = null;
            RefreshEditorTickRegistration();
            Repaint();
        }

        private void TogglePlayback()
        {
            if (!EnsurePreviewManagerForPlayMode())
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

        private static string GetDefaultRecordingPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "star67-preview.s67trk");
        }

        private bool TryGetConfiguredSenderAddress(out IPAddress configuredSenderAddress, out string validationMessage)
        {
            configuredSenderAddress = null;
            if (string.IsNullOrWhiteSpace(senderIpAddress))
            {
                validationMessage = "Enter the sender's IPv4 address to start live preview.";
                return false;
            }

            if (!IPAddress.TryParse(senderIpAddress, out configuredSenderAddress) || configuredSenderAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                configuredSenderAddress = null;
                validationMessage = "Sender IP must be a valid IPv4 address.";
                return false;
            }

            validationMessage = null;
            return true;
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

        private bool EnsurePreviewManagerForPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            if (!EnsureCompositionRootForPlayMode())
            {
                return false;
            }

            ApplyActiveSourceToManager();
            return manager != null;
        }

        private bool EnsureCompositionRootForPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            if (manager != null)
            {
                if (!string.IsNullOrEmpty(manager.StatusMessage))
                {
                    playModeStatus = manager.StatusMessage;
                }

                return true;
            }

            return ResolveCompositionRoot(createIfNeeded: true);
        }

        private bool ResolveCompositionRoot(bool createIfNeeded)
        {
            if (!EditorApplication.isPlaying)
            {
                manager = null;
                playModeStatus = null;
                return false;
            }

            EditorPreviewManager resolvedRoot = EditorPreviewManager.FindActive();
            string resolvedStatusMessage = resolvedRoot != null ? resolvedRoot.StatusMessage : null;
            if (resolvedRoot == null && createIfNeeded)
            {
                EditorPreviewManager.TryResolveOrCreateForPlayMode(out resolvedRoot, out resolvedStatusMessage);
            }

            manager = resolvedRoot;
            playModeStatus = !string.IsNullOrEmpty(resolvedStatusMessage)
                ? resolvedStatusMessage
                : manager != null
                    ? manager.StatusMessage
                    : "No Star67Avatar found in loaded scenes.";
            ApplyActiveSourceToManager();
            return manager != null;
        }

        private void ResetPlayModeResolutionState()
        {
            manager = null;
            playModeStatus = EditorApplication.isPlaying
                ? "Resolving play-mode preview composition root..."
                : null;
        }

        private bool IsPreviewUnavailableInPlayMode()
        {
            return EditorApplication.isPlaying && manager == null;
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

        private void ApplyActiveSourceToManager()
        {
            if (manager == null || !EditorApplication.isPlaying)
            {
                return;
            }

            if (playbackPlayer != null)
            {
                manager.SetSource(playbackPlayer);
                return;
            }

            if (liveClient != null)
            {
                manager.SetSource(liveClient);
                return;
            }

            manager.SetSource(null);
        }

        private bool NeedsPeriodicRefresh()
        {
            return playbackPlayer != null || liveClient != null;
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
