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
        private const string RecordingPathKey = "Star67.Tracking.Editor.RecordingPath";

        [SerializeField] private TrackingPreviewController previewController;
        [SerializeField] private string recordingPath;
        [SerializeField] private bool loopPlayback;

        private UdpTrackingSession liveSession;
        private TrackingDiscoveryService discoveryService;
        private TrackingRecordingWriter recordingWriter;
        private TrackingRecordingPlayer playbackPlayer;

        [MenuItem("Window/Star67/Tracking Preview")]
        public static void OpenWindow()
        {
            GetWindow<TrackingPreviewWindow>("Tracking Preview").Show();
        }

        public static void OpenWindow(TrackingPreviewController controller)
        {
            TrackingPreviewWindow window = GetWindow<TrackingPreviewWindow>("Tracking Preview");
            window.previewController = controller;
            window.Show();
            window.Focus();
        }

        public static void OpenWindowForRoot(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            TrackingPreviewEditorUtilities.EnsureStar67PreviewSetup(root);
            OpenWindow(root.GetComponent<TrackingPreviewController>());
        }

        private void OnEnable()
        {
            recordingPath = EditorPrefs.GetString(RecordingPathKey, GetDefaultRecordingPath());
            EditorApplication.update += EditorTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
            StopRecording();
            StopPlayback();
            StopLive();
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
            previewController = (TrackingPreviewController)EditorGUILayout.ObjectField("Controller", previewController, typeof(TrackingPreviewController), true);

            if (previewController == null)
            {
                EditorGUILayout.HelpBox("Assign a TrackingPreviewController in the scene. Use the Star67 avatar inspector button to add a default preview setup.", MessageType.Info);
            }
            else if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to apply live or recorded tracking to the preview controller.", MessageType.Info);
            }
        }

        private void DrawLiveSection()
        {
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("IPv4", string.Join(", ", GetLocalIPv4Addresses()), GUILayout.MaxWidth(position.width - 120f));
            }

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
                DiscoveryRegistryEntry[] announcements = discoveryService.Registry.Snapshot(TimeSpan.FromSeconds(5));
                EditorGUILayout.LabelField("Discovered Apps", announcements.Length.ToString());
                for (int i = 0; i < announcements.Length; i++)
                {
                    DiscoveryRegistryEntry entry = announcements[i];
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
            if (previewController != null && EditorApplication.isPlaying)
            {
                if (playbackPlayer != null && !ReferenceEquals(previewController.Source, playbackPlayer))
                {
                    previewController.SetSource(playbackPlayer);
                }
                else if (playbackPlayer == null && liveSession != null && !ReferenceEquals(previewController.Source, liveSession))
                {
                    previewController.SetSource(liveSession);
                }
            }

            Repaint();
        }

        private void StartLive()
        {
            StopPlayback();
            StopLive();

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

            if (previewController != null)
            {
                previewController.SetSource(liveSession);
            }
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
            liveSession?.Dispose();
            liveSession = null;
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
        }

        private void StopRecording()
        {
            if (liveSession != null)
            {
                liveSession.PacketSink = null;
            }

            recordingWriter?.Dispose();
            recordingWriter = null;
        }

        private void OpenPlayback(string path)
        {
            StopLive();
            StopPlayback();

            playbackPlayer = new TrackingRecordingPlayer(path)
            {
                Loop = loopPlayback
            };
            if (previewController != null)
            {
                previewController.SetSource(playbackPlayer);
            }
        }

        private void StopPlayback()
        {
            if (previewController != null && playbackPlayer != null && ReferenceEquals(previewController.Source, playbackPlayer))
            {
                previewController.SetSource(null);
            }

            playbackPlayer?.Dispose();
            playbackPlayer = null;
        }

        private void TogglePlayback()
        {
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
