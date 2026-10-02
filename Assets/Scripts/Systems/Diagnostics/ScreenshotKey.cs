using System;
using System.IO;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// F12 saves the frame as it is on screen, HUD included, to
    /// Pictures\Survival Chaos.
    ///
    /// The file's name carries the build stamp and the time, so a picture says
    /// which build it came from - the thing a screenshot taken with Windows'
    /// own tools cannot. It creates itself, like the performance overlay, so
    /// it works in every scene and in a normal build.
    ///
    /// A "Saved" note shows for a second afterwards. It is drawn only once the
    /// capture has been taken, so it is never in the picture.
    /// </summary>
    public sealed class ScreenshotKey : MonoBehaviour
    {
        public const string FolderName = "Survival Chaos";

        private const float NoteSeconds = 1.5f;

        // ScreenCapture takes the picture at the end of the frame it is asked
        // in. The note waits two frames, so it cannot be in it.
        private const int NoteDelayFrames = 2;

        private readonly GUIContent note = new GUIContent();
        private GUIStyle style;
        private Texture2D background;
        private int noteFromFrame = -1;
        private float noteUntil;

#if !SURVIVAL_CHAOS_NO_SCREENSHOTS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameObject host = new GameObject("Screenshot Key");
            host.AddComponent<ScreenshotKey>();
            DontDestroyOnLoad(host);
        }
#endif

        private void Awake()
        {
            // No GUILayout is used, and leaving this on allocates a layout
            // context for every OnGUI event whether or not a note is showing.
            useGUILayout = false;
        }

        private void OnDestroy()
        {
            if (background != null)
            {
                Destroy(background);
            }
        }

        private void Update()
        {
            if (GameInput.ScreenshotPressed)
            {
                Save();
            }
        }

        private void Save()
        {
            string file = ScreenshotName.For(BuildStamp.Text, DateTime.Now);

            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), FolderName);
                Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, file));
                note.text = "Saved to Pictures\\" + FolderName + "\\" + file;
            }
            catch (Exception problem)
            {
                // A read-only or missing Pictures folder is the player's
                // machine, not a bug; say so rather than fail silently.
                note.text = "Screenshot not saved: " + problem.Message;
            }

            noteFromFrame = Time.frameCount + NoteDelayFrames;
            noteUntil = Time.unscaledTime + NoteSeconds;
        }

        private void OnGUI()
        {
            if (noteFromFrame < 0 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (Time.frameCount < noteFromFrame)
            {
                return;
            }

            if (Time.unscaledTime > noteUntil)
            {
                noteFromFrame = -1;
                return;
            }

            if (style == null)
            {
                background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
                background.Apply();

                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, Screen.height / 60),
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(14, 14, 8, 8)
                };
                style.normal.textColor = Color.white;
                style.normal.background = background;
            }

            Vector2 size = style.CalcSize(note);
            Rect box = new Rect((Screen.width - size.x) * 0.5f, Screen.height - size.y - 24f, size.x, size.y);
            GUI.Label(box, note, style);
        }
    }

    /// <summary>
    /// The file name of a screenshot: the build stamp, then the time, as in
    /// <c>26_0930-0825_polish 8ee1a3a+ 2026-09-30 21.14.07.png</c>. Kept apart
    /// from the component so it can be tested without a screen.
    /// </summary>
    public static class ScreenshotName
    {
        public static string For(string stamp, DateTime when)
        {
            string name = Clean(stamp);
            string time = when.ToString("yyyy-MM-dd HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture);
            return (name.Length > 0 ? name + " " : string.Empty) + time + ".png";
        }

        /// <summary>
        /// The stamp with everything a file name cannot hold taken out. The
        /// stamp's own separator is a middle dot between the build and the
        /// commit; it reads as a space here.
        /// </summary>
        public static string Clean(string stamp)
        {
            if (string.IsNullOrWhiteSpace(stamp))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            System.Text.StringBuilder builder = new System.Text.StringBuilder(stamp.Length);
            bool lastWasSpace = true;

            foreach (char c in stamp)
            {
                bool drop = c == '·' || char.IsWhiteSpace(c) || char.IsControl(c) ||
                            Array.IndexOf(invalid, c) >= 0;

                if (!drop)
                {
                    builder.Append(c);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }
    }
}
