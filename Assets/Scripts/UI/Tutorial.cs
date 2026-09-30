using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>The moments a control is taught.</summary>
    public enum ControlHint
    {
        Reverse,
        Dash,
        SlowMo,
        Deflector,
        Offer
    }

    /// <summary>
    /// Teaches each control at the moment it first matters, one short line at
    /// a time, in the prompt at the foot of the screen.
    ///
    /// There used to be one line, "Shift - Reverse", for the first five
    /// seconds. It said Shift to a player on a pad, and nothing ever
    /// introduced the dash, Slow Mo or the Deflector, or said that taking one
    /// upgrade gives up the others. Now Reverse and Dash come in the opening
    /// seconds, Slow Mo and the Deflector the first time they are taken, and
    /// the offer the first time one goes out. Each line names the key or the
    /// pad button, whichever the player used last, and changes on the spot if
    /// they switch.
    ///
    /// A line leaves once the player has done what it says, or after a few
    /// seconds. Only in the first few runs: after that the player knows, and
    /// the prompt would be clutter. The Controls tab in Options can bring them
    /// back.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        /// <summary>How many runs the player has started with hints showing.</summary>
        public const string RunsSeenKey = "SurvivalChaos.Hints.RunsSeen";

        /// <summary>The runs that show hints. Also what the Controls tab counts against.</summary>
        public const int HintedRuns = 3;

        [SerializeField]
        [Tooltip("The prompt panel. Its text is found inside it.")]
        private GameObject shiftTutorial;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Game seconds before the first line, so the run is under way when it appears.")]
        private float openingDelay = 0.75f;

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("The longest a line stays, in game seconds, if the player never does what it says.")]
        private float lineSeconds = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("The shortest a line stays, so doing it at once does not flash it past unread.")]
        private float minimumSeconds = 1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Game seconds between one line and the next.")]
        private float gapSeconds = 0.4f;

        private static Tutorial active;

        private readonly Queue<ControlHint> queue = new Queue<ControlHint>();
        private readonly HashSet<ControlHint> taught = new HashSet<ControlHint>();

        private TMP_Text text;
        private bool hinting;
        private bool showing;
        private ControlHint current;
        private bool done;
        private float shownFor;
        private float waitUntil;

        /// <summary>Game seconds since the run began: Time.time counts from the application's start.</summary>
        private float clock;
        private int shownDevice = -1;

        /// <summary>
        /// Whether hints will show in the next run. The Controls tab's row
        /// reads and sets it.
        /// </summary>
        public static bool HintsOn
        {
            get => HintRunsLeft > 0;
            set
            {
                PlayerPrefs.SetInt(RunsSeenKey, value ? 0 : HintedRuns);
                SettingsStore.MarkDirty();
            }
        }

        /// <summary>How many more runs will start with hints.</summary>
        public static int HintRunsLeft => Mathf.Max(0, HintedRuns - PlayerPrefs.GetInt(RunsSeenKey, 0));

        /// <summary>
        /// Asks for a line to be taught, if this run is showing hints and it
        /// has not been taught already this run. Safe to call from anywhere,
        /// with or without a prompt in the scene.
        /// </summary>
        public static void Teach(ControlHint hint)
        {
            if (active != null)
            {
                active.Enqueue(hint);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            active = null;
        }

        private void OnEnable()
        {
            active = this;
        }

        private void OnDisable()
        {
            if (active == this)
            {
                active = null;
            }
        }

        void Start()
        {
            if (shiftTutorial == null)
            {
                Debug.LogWarning("Tutorial has no prompt assigned, so nothing will be shown.", this);
                return;
            }

            text = shiftTutorial.GetComponentInChildren<TMP_Text>(true);

            // Counted as the run starts, so the third run is the last to show them.
            int seen = PlayerPrefs.GetInt(RunsSeenKey, 0);
            hinting = seen < HintedRuns;
            if (hinting)
            {
                PlayerPrefs.SetInt(RunsSeenKey, seen + 1);
                SettingsStore.MarkDirty();
            }

            waitUntil = openingDelay;
            Enqueue(ControlHint.Reverse);
            Enqueue(ControlHint.Dash);
            Refresh();
        }

        private void Enqueue(ControlHint hint)
        {
            if (!hinting || taught.Contains(hint))
            {
                return;
            }

            taught.Add(hint);
            queue.Enqueue(hint);
        }

        /// <summary>
        /// Game time throughout, so a line waits out a pause rather than
        /// expiring behind the menu, and lasts as long under Slow Mo as it
        /// reads.
        /// </summary>
        void Update()
        {
            if (!hinting || text == null)
            {
                Refresh();
                return;
            }

            clock += Time.deltaTime;
            float now = clock;

            if (!showing && queue.Count > 0 && now >= waitUntil)
            {
                current = queue.Dequeue();
                showing = true;
                done = false;
                shownFor = 0f;
                shownDevice = -1;
            }

            if (showing && !PauseMenu.GameIsPaused && !RunOutcome.Decided)
            {
                shownFor += Time.deltaTime;
                done |= DidIt(current);

                if (shownFor >= lineSeconds || (done && shownFor >= minimumSeconds))
                {
                    showing = false;
                    waitUntil = now + gapSeconds;
                }
            }

            Refresh();
        }

        /// <summary>
        /// Whether the player has just done what the line says. The two that
        /// name no button run their time out.
        /// </summary>
        private static bool DidIt(ControlHint hint)
        {
            switch (hint)
            {
                case ControlHint.Reverse: return GameInput.ToggleDirectionReleased;
                case ControlHint.Dash: return GameInput.DashPressed;
                case ControlHint.SlowMo: return GameInput.SlowMoPressed;
                default: return false;
            }
        }

        /// <summary>
        /// One line per hint, in the keys or the pad's buttons. Short, because
        /// it is read in the middle of a fight.
        /// </summary>
        public static string Line(ControlHint hint, bool pad)
        {
            switch (hint)
            {
                case ControlHint.Reverse: return (pad ? "LB" : "Shift") + "  -  Reverse";
                case ControlHint.Dash: return (pad ? "A or RB" : "Space") + "  -  Dash";
                case ControlHint.SlowMo: return (pad ? "Y or LT" : "E") + "  -  Slow Mo";
                case ControlHint.Deflector: return "Deflector  -  blocks a hit, then recharges";
                case ControlHint.Offer: return "Fly into one  -  the others vanish";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Out of the way while a menu is up, or once the run is decided. On
        /// 30 September 2026 the prompt covered the options tabs' Back button
        /// when a run was paused in its first five seconds.
        /// </summary>
        private void Refresh()
        {
            if (shiftTutorial == null)
            {
                return;
            }

            bool visible = showing && !PauseMenu.GameIsPaused && !RunOutcome.Decided;
            if (shiftTutorial.activeSelf != visible)
            {
                shiftTutorial.SetActive(visible);
            }

            int device = GameInput.PadLastUsed ? 1 : 0;
            if (visible && text != null && device != shownDevice)
            {
                shownDevice = device;
                text.text = Line(current, device == 1);
            }
        }
    }
}
