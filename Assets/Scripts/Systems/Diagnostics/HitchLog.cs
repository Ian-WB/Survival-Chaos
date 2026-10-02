using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// Writes down every frame that takes longer than 50 ms, with what the
    /// game had just done.
    ///
    /// DirectX 12 builds a draw state the first time something is drawn, so
    /// the first frame of a thing can be a slow one: the arena's first two
    /// took 0.48 and 0.28 s in a build, which is why the loading screen stays
    /// up through them. Nothing covers the Leviathan's first frames, or its
    /// attacks'. A stutter there cannot be judged in the editor, where the
    /// editor's own work drowns it, so the build keeps a log and a played run
    /// finds out.
    ///
    /// The game tells it what is happening through <see cref="Note"/>: a load,
    /// the Leviathan arriving, an act, an attack's first volley. A slow frame
    /// is written with the notes of the half second before it.
    ///
    /// The log is <c>hitches.log</c> beside the player's settings
    /// (<c>hitches-editor.log</c> from the editor, so the two never mix), and
    /// each line goes to the player log as well.
    /// </summary>
    public sealed class HitchLog : MonoBehaviour
    {
        /// <summary>A frame longer than this is written down.</summary>
        public const float ThresholdSeconds = 0.05f;

        /// <summary>How far back a note still counts as what happened on a slow frame.</summary>
        public const float NoteWindowSeconds = 0.5f;

        private const int NoteCapacity = 16;

        private const float RepeatWindowSeconds = 0.1f;

        // Enough for a run with a hitch every few seconds; past it something is
        // wrong that a longer file would not explain better.
        private const int MaximumLines = 500;

        private static readonly string[] noteText = new string[NoteCapacity];
        private static readonly float[] noteTime = new float[NoteCapacity];
        private static int noteNext;

        private readonly StringBuilder builder = new StringBuilder(256);
        private string path;
        private bool headerWritten;
        private int lines;

#if !SURVIVAL_CHAOS_NO_HITCH_LOG
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameObject host = new GameObject("Hitch Log");
            host.AddComponent<HitchLog>();
            DontDestroyOnLoad(host);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Array.Clear(noteText, 0, noteText.Length);
            Array.Clear(noteTime, 0, noteTime.Length);
            noteNext = 0;
        }

        /// <summary>
        /// Says what the game is doing right now, for a slow frame to be
        /// blamed on. Cheap, and safe to call from anywhere.
        /// </summary>
        public static void Note(string what)
        {
            // The same thing said again at once is one thing: a kit that
            // grants eight levels in a frame is "level up", not eight of them.
            int previous = (noteNext + NoteCapacity - 1) % NoteCapacity;
            if (noteText[previous] == what &&
                Time.realtimeSinceStartup - noteTime[previous] < RepeatWindowSeconds)
            {
                return;
            }

            noteText[noteNext] = what;
            noteTime[noteNext] = Time.realtimeSinceStartup;
            noteNext = (noteNext + 1) % NoteCapacity;
        }

        private void Awake()
        {
            string file = Application.isEditor ? "hitches-editor.log" : "hitches.log";
            path = Path.Combine(Application.persistentDataPath, file);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // This object is made after the first scene has loaded, so that
            // load never reaches OnSceneLoaded.
            Note("game starts in " + SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Note("scene " + scene.name + " loaded");
        }

        private void OnApplicationFocus(bool focused)
        {
            Note(focused ? "window focused" : "window lost focus");
        }

        private void Update()
        {
            float frame = Time.unscaledDeltaTime;
            if (!IsHitch(frame) || lines >= MaximumLines)
            {
                return;
            }

            string line = Describe(frame, Time.realtimeSinceStartup, Time.timeSinceLevelLoad,
                SceneManager.GetActiveScene().name, Time.timeScale, RecentNotes(Time.realtimeSinceStartup));
            Write(line);
        }

        /// <summary>Whether a frame of this length is written down.</summary>
        public static bool IsHitch(float frameSeconds)
        {
            return frameSeconds > ThresholdSeconds;
        }

        /// <summary>One line of the log. Pure, so its shape can be tested.</summary>
        public static string Describe(float frameSeconds, float sinceStart, float sinceSceneLoad,
            string scene, float timeScale, string notes)
        {
            CultureInfo plain = CultureInfo.InvariantCulture;
            return string.Format(plain, "{0,7:F0} ms  at {1,8:F2} s  {2} +{3:F2} s  x{4:0.##}  {5}",
                frameSeconds * 1000f, sinceStart, scene, sinceSceneLoad, timeScale,
                string.IsNullOrEmpty(notes) ? "nothing noted" : notes);
        }

        private string RecentNotes(float now)
        {
            builder.Length = 0;

            // Oldest first, so the line reads in the order things happened.
            for (int i = 0; i < NoteCapacity; i++)
            {
                int slot = (noteNext + i) % NoteCapacity;
                if (noteText[slot] == null || now - noteTime[slot] > NoteWindowSeconds + Time.unscaledDeltaTime)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(noteText[slot]);
            }

            return builder.ToString();
        }

        private void Write(string line)
        {
            lines++;

            // Not to the editor's console: the editor hitches for its own
            // reasons, and a line a hitch there would bury the real log.
            if (!Application.isEditor)
            {
                Debug.Log("HITCH " + line);
            }

            try
            {
                if (!headerWritten)
                {
                    headerWritten = true;
                    File.AppendAllText(path, Environment.NewLine + "== " + BuildStamp.Text + "  " +
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " +
                        SystemInfo.graphicsDeviceName + "  " + Screen.width + "x" + Screen.height +
                        Environment.NewLine);
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception)
            {
                // A log that cannot be written is not worth a second hitch.
                lines = MaximumLines;
            }
        }
    }
}
