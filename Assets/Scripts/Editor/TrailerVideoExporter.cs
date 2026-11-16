#if UNITY_EDITOR

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Records the showcase videos, with the game's own sound:
    /// 1. Arena Intro Cutscene (Game.unity) -> Builds/Videos/Intro_Arena_Entry.mp4
    /// 2. Title Boot Splash & Attract Mode (Menu.unity) -> Builds/Videos/Title_Boot_Splash.mp4
    /// 3. Full 60-Second Cinematic Action Trailer (Game.unity) -> Builds/Videos/Survival_Chaos_Trailer_60s.mp4
    ///
    /// Unity Recorder films the Game view and records the game's audio in step
    /// with it, one frame of film per frame of game. Until 16 November 2026 this
    /// saved a PNG a frame and had ffmpeg lay one music file under them, so the
    /// videos had none of the game's sounds - no boss horn, no explosions - and
    /// the music was not what the game plays there: the boss track under the
    /// splash, the menu track under the intro, and music over the opening card,
    /// which the game holds silent.
    ///
    /// The Game view is switched to the recording size while it records, as the
    /// Recorder does, and put back on the size it was on afterwards. Its Mute
    /// Audio is lifted while it records, since the Recorder hears only what the
    /// game plays out - muted, the first recording had a silent track - and set
    /// back as it was. The game can be heard while it records.
    /// </summary>
    public static class TrailerVideoExporter
    {
        public static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "Videos"));

        public const int Width = 2560;
        public const int Height = 1440;
        public const int FrameRate = 30;

        /// <summary>Frames the scene runs before the recording starts, so its first frames are not filmed.</summary>
        private const int WarmUpFrames = 10;

        /// <summary>The title splash's key press, in seconds of film: after the logo has lit and the prompt has come up.</summary>
        public const float TitleRevealAt = 5.0f;

        private static string activeFileName;
        private static string finalMp4Path;
        private static int totalFrames;
        private static int startFrame;
        private static bool isFullTrailer;
        private static bool revealPressed;
        private static bool waitingToStart;
        private static RecorderController controller;
        private static int heldGameViewSize = -1;
        private static bool? heldMute;
        private static EditorApplication.CallbackFunction updateHook;

        [MenuItem("Survival Chaos/Videos/Record 60s Cinematic Trailer (MP4)", priority = 220)]
        public static void RecordFullTrailer()
        {
            LaunchRecorder("Assets/Scenes/Game.unity", "Survival_Chaos_Trailer_60s", 60.0f, true);
        }

        [MenuItem("Survival Chaos/Videos/Record Arena Intro Cutscene (MP4)", priority = 221)]
        public static void RecordArenaIntro()
        {
            LaunchRecorder("Assets/Scenes/Game.unity", "Intro_Arena_Entry", 4.2f, false);
        }

        [MenuItem("Survival Chaos/Videos/Record Title Menu Splash (MP4)", priority = 222)]
        public static void RecordTitleSplash()
        {
            LaunchRecorder("Assets/Scenes/Menu.unity", "Title_Boot_Splash", 7.0f, false);
        }

        /// <summary>Frames of film for <paramref name="seconds"/>, never fewer than half a second's.</summary>
        public static int FramesFor(float seconds)
        {
            return Mathf.Max(FrameRate / 2, Mathf.CeilToInt(seconds * FrameRate));
        }

        public static void LaunchRecorder(string scenePath, string fileName, float duration, bool isTrailer)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Please stop Play Mode before starting an automated recording session.");
                return;
            }

            if (controller != null || waitingToStart)
            {
                Debug.LogWarning("[TrailerVideoExporter] A recording is already under way.");
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
            isFullTrailer = isTrailer;
            revealPressed = false;
            totalFrames = FramesFor(duration);

            Directory.CreateDirectory(OutputDirectory);
            finalMp4Path = Path.Combine(OutputDirectory, fileName + ".mp4");

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
            // The warm-up runs on film time too, so the splash's opening card has
            // moved on by its frames and no more when the filming starts.
            Time.captureDeltaTime = 1f / FrameRate;
            waitingToStart = true;

            Debug.Log($"[TrailerVideoExporter] Recording '{activeFileName}': {totalFrames} frames @ {FrameRate} fps -> {finalMp4Path}");

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
            if (state == PlayModeStateChange.ExitingPlayMode && (controller != null || waitingToStart))
            {
                Debug.LogWarning("[TrailerVideoExporter] Recording cancelled: play mode was left before it finished.");
                StopEverything();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (waitingToStart)
            {
                // Warm-up: wait for the scene to render its first frames.
                if (Time.frameCount < WarmUpFrames || Time.timeSinceLevelLoad < 0.2f)
                {
                    return;
                }

                waitingToStart = false;

                if (isFullTrailer)
                {
                    CinematicTrailerDirector.StartTrailer();
                }

                if (!BeginRecording())
                {
                    StopEverything();
                    EditorApplication.isPlaying = false;
                }
                return;
            }

            if (controller == null)
            {
                return;
            }

            int filmed = Time.frameCount - startFrame;

            // The title film's key press, which takes the attract prompt to the menu.
            if (!revealPressed && activeFileName.Contains("Title") && filmed >= Mathf.RoundToInt(TitleRevealAt * FrameRate))
            {
                revealPressed = true;
                TitleSplashDirector splash = UnityEngine.Object.FindAnyObjectByType<TitleSplashDirector>();
                if (splash != null)
                {
                    splash.TriggerMenuReveal();
                }
            }

            if (filmed >= totalFrames)
            {
                StopEverything();

                if (File.Exists(finalMp4Path))
                {
                    Debug.Log($"[TrailerVideoExporter] SUCCESS: Video exported to {finalMp4Path}");
                }
                else
                {
                    Debug.LogError($"[TrailerVideoExporter] The Recorder finished but wrote no {finalMp4Path}.");
                }

                EditorApplication.isPlaying = false;
            }
        }

        private static bool BeginRecording()
        {
            RecorderControllerSettings settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = activeFileName;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movie.CaptureAudio = true;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = Width,
                OutputHeight = Height
            };

            // The Recorder adds the extension.
            movie.OutputFile = Path.Combine(OutputDirectory, activeFileName).Replace('\\', '/');

            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = FrameRate;
            settings.CapFrameRate = true;
            settings.ExitPlayMode = false;

            heldGameViewSize = GameViewSizeIndex();
            heldMute = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = false;

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            if (!controller.StartRecording())
            {
                Debug.LogError("[TrailerVideoExporter] The Recorder did not start.");
                controller = null;
                return false;
            }

            startFrame = Time.frameCount;
            return true;
        }

        private static void StopEverything()
        {
            waitingToStart = false;

            if (controller != null)
            {
                if (controller.IsRecording())
                {
                    controller.StopRecording();
                }
                controller = null;
            }

            Time.captureDeltaTime = 0f;
            EditorApplication.playModeStateChanged -= OnPlayStateChangedDuringCapture;

            if (updateHook != null)
            {
                EditorApplication.update -= updateHook;
                updateHook = null;
            }

            if (heldGameViewSize >= 0)
            {
                SetGameViewSizeIndex(heldGameViewSize);
                heldGameViewSize = -1;
            }

            if (heldMute.HasValue)
            {
                EditorUtility.audioMasterMute = heldMute.Value;
                heldMute = null;
            }
        }

        // The Game view's size choice is not public; the Recorder reaches it the same way.
        private static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");

        private static EditorWindow MainGameView()
        {
            if (GameViewType == null)
            {
                return null;
            }

            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(GameViewType);
            return views.Length > 0 ? views[0] as EditorWindow : null;
        }

        private static int GameViewSizeIndex()
        {
            EditorWindow view = MainGameView();
            PropertyInfo index = GameViewType?.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return view != null && index != null ? (int)index.GetValue(view) : -1;
        }

        private static void SetGameViewSizeIndex(int value)
        {
            EditorWindow view = MainGameView();
            PropertyInfo index = GameViewType?.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (view != null && index != null)
            {
                index.SetValue(view, value);
                view.Repaint();
            }
        }
    }
}

#endif
