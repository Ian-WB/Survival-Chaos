#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Automates recording high-definition MP4 showcase videos for:
    /// 1. Arena Intro Cutscene (Game.unity) -> Builds/Videos/Intro_Arena_Entry.mp4
    /// 2. Title Boot Splash & Attract Mode (Menu.unity) -> Builds/Videos/Title_Boot_Splash.mp4
    /// 3. Full 60-Second Cinematic Action Trailer (Game.unity) -> Builds/Videos/Survival_Chaos_Trailer_60s.mp4
    /// </summary>
    public static class TrailerVideoExporter
    {
        public static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "Videos"));

        public static string MusicTrackPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "Audio", "Music", "neon_hyperdrive.mp3"));

        public static string GameplayMusicTrackPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "Audio", "Music", "blow_the_day_with_a_darbuka.mp3"));

        private static string activeFileName;
        private static string tempFolder;
        private static string finalMp4Path;
        private static string audioPath;
        private static int totalFrames;
        private static int currentFrame;
        private static int lastCapturedFrameCount = -1;
        private static bool isFullTrailer;
        private static bool trailerStarted;
        private static bool recordingActive;
        private static EditorApplication.CallbackFunction updateHook;

        [MenuItem("Survival Chaos/Videos/Record 60s Cinematic Trailer (MP4)", priority = 220)]
        public static void RecordFullTrailer()
        {
            LaunchRecorder("Assets/Scenes/Game.unity", "Survival_Chaos_Trailer_60s", 60.0f, true, MusicTrackPath);
        }

        [MenuItem("Survival Chaos/Videos/Record Arena Intro Cutscene (MP4)", priority = 221)]
        public static void RecordArenaIntro()
        {
            LaunchRecorder("Assets/Scenes/Game.unity", "Intro_Arena_Entry", 4.2f, false, GameplayMusicTrackPath);
        }

        [MenuItem("Survival Chaos/Videos/Record Title Menu Splash (MP4)", priority = 222)]
        public static void RecordTitleSplash()
        {
            LaunchRecorder("Assets/Scenes/Menu.unity", "Title_Boot_Splash", 5.5f, false, MusicTrackPath);
        }

        public static void LaunchRecorder(string scenePath, string fileName, float duration, bool isTrailer, string audioFile = null)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Please stop Play Mode before starting an automated recording session.");
                return;
            }

            // Opening the scene below would throw away unsaved edits in the one
            // that is open. Ask first, as the editor itself does, and stop on Cancel.
            if (EditorSceneManager.GetActiveScene().path != scenePath
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[TrailerVideoExporter] Recording not started: the open scene was left unsaved.");
                return;
            }

            activeFileName = fileName;
            audioPath = audioFile;
            isFullTrailer = isTrailer;
            trailerStarted = false;
            lastCapturedFrameCount = -1;
            totalFrames = Mathf.Max(15, Mathf.CeilToInt(duration * 30f));
            currentFrame = 0;
            recordingActive = false;

            string outDir = OutputDirectory;
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            finalMp4Path = Path.Combine(outDir, fileName + ".mp4");
            tempFolder = Path.Combine(outDir, "temp_" + fileName);

            try
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
                Directory.CreateDirectory(tempFolder);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TrailerVideoExporter] Temp folder note: {ex.Message}");
            }

            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                EditorSceneManager.OpenScene(scenePath);
            }

            Action<PlayModeStateChange> playHook = null;
            playHook = (state) =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    EditorApplication.playModeStateChanged -= playHook;
                    StartCaptureSession();
                }
            };

            EditorApplication.playModeStateChanged += playHook;
            EditorApplication.isPlaying = true;
        }

        private static void StartCaptureSession()
        {
            Time.captureFramerate = 30;
            recordingActive = true;
            currentFrame = 0;
            trailerStarted = false;
            lastCapturedFrameCount = -1;

            Debug.Log($"[TrailerVideoExporter] Capturing '{activeFileName}': {totalFrames} frames @ 30fps -> {finalMp4Path}");

            EditorApplication.playModeStateChanged -= OnPlayStateChangedDuringCapture;
            EditorApplication.playModeStateChanged += OnPlayStateChangedDuringCapture;

            if (updateHook != null)
            {
                EditorApplication.update -= updateHook;
            }

            updateHook = OnEditorUpdate;
            EditorApplication.update += updateHook;
        }

        private static void OnPlayStateChangedDuringCapture(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && recordingActive)
            {
                CancelCapture();
            }
        }

        private static void CancelCapture()
        {
            recordingActive = false;
            Time.captureFramerate = 0;
            EditorApplication.playModeStateChanged -= OnPlayStateChangedDuringCapture;

            if (updateHook != null)
            {
                EditorApplication.update -= updateHook;
                updateHook = null;
            }

            Debug.LogWarning("[TrailerVideoExporter] Recording cancelled due to exiting play mode.");
        }

        private static void OnEditorUpdate()
        {
            if (!recordingActive || !EditorApplication.isPlaying)
            {
                return;
            }

            // Warm-up: wait for scene to render initial frames and camera to activate
            if (Time.frameCount < 10 || Time.timeSinceLevelLoad < 0.2f)
            {
                return;
            }

            // Start trailer once scene is warm
            if (isFullTrailer && !trailerStarted)
            {
                trailerStarted = true;
                CinematicTrailerDirector.StartTrailer();
            }

            // Ensure we capture strictly once per newly rendered engine frame
            int frame = Time.frameCount;
            if (frame == lastCapturedFrameCount)
            {
                return;
            }
            lastCapturedFrameCount = frame;

            // For Title screen recording, simulate key press at frame 105 (~3.5s) to reveal menu buttons
            if (activeFileName.Contains("Title") && currentFrame == 105)
            {
                var splash = UnityEngine.Object.FindAnyObjectByType<TitleSplashDirector>();
                if (splash != null)
                {
                    splash.TriggerMenuReveal();
                }
            }

            if (currentFrame < totalFrames)
            {
                string framePath = Path.Combine(tempFolder, $"frame_{currentFrame:D5}.png");
                ScreenCapture.CaptureScreenshot(framePath);
                currentFrame++;
            }
            else
            {
                recordingActive = false;
                EditorApplication.update -= updateHook;
                updateHook = null;
                FinalizeCapture();
            }
        }

        private static void FinalizeCapture()
        {
            Time.captureFramerate = 0;
            EditorApplication.playModeStateChanged -= OnPlayStateChangedDuringCapture;

            // Wait a moment for last frame on disk
            string lastFrame = Path.Combine(tempFolder, $"frame_{(totalFrames - 1):D5}.png");
            for (int i = 0; i < 40; i++)
            {
                if (File.Exists(lastFrame)) break;
                System.Threading.Thread.Sleep(50);
            }
            System.Threading.Thread.Sleep(300);

            Debug.Log($"[TrailerVideoExporter] All {totalFrames} frames written. Encoding with FFmpeg...");

            // The frames are kept when the encode fails, so it can be run again by hand.
            if (EncodeWithFFmpeg())
            {
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TrailerVideoExporter] Clean up note: {ex.Message}");
                }

                Debug.Log($"[TrailerVideoExporter] SUCCESS: Video exported to {finalMp4Path}");
            }
            else
            {
                Debug.LogError($"[TrailerVideoExporter] No video written. The frames are still in {tempFolder}.");
            }

            EditorApplication.isPlaying = false;
        }

        /// <summary>How long the encode may take before it is given up on.</summary>
        private const int EncodeTimeoutMs = 120000;

        /// <summary>True only when ffmpeg ran to the end and said it succeeded.</summary>
        private static bool EncodeWithFFmpeg()
        {
            string inputPattern = Path.Combine(tempFolder, "frame_%05d.png");
            string arguments;

            bool hasAudio = !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath);
            if (hasAudio)
            {
                arguments = $"-loglevel error -y -framerate 30 -i \"{inputPattern}\" -i \"{audioPath}\" -c:v libx264 -pix_fmt yuv420p -crf 18 -preset fast -c:a aac -b:a 192k -shortest \"{finalMp4Path}\"";
            }
            else
            {
                arguments = $"-loglevel error -y -framerate 30 -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p -crf 18 -preset fast \"{finalMp4Path}\"";
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = false,
                    RedirectStandardOutput = false
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        Debug.LogError("[TrailerVideoExporter] FFmpeg did not start.");
                        return false;
                    }

                    // ExitCode throws on a process still running, so a timeout is
                    // settled before it is read: the encode is stopped and failed.
                    if (!proc.WaitForExit(EncodeTimeoutMs))
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(5000);
                        }
                        catch (Exception killEx)
                        {
                            Debug.LogWarning($"[TrailerVideoExporter] Could not stop FFmpeg: {killEx.Message}");
                        }

                        Debug.LogError($"[TrailerVideoExporter] FFmpeg timed out after {EncodeTimeoutMs / 1000} s and was stopped.");
                        return false;
                    }

                    if (proc.ExitCode != 0)
                    {
                        Debug.LogError($"[TrailerVideoExporter] FFmpeg error (code {proc.ExitCode})");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TrailerVideoExporter] Failed to run FFmpeg: {ex.Message}");
                return false;
            }
        }
    }
}

#endif
