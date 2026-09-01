using System.Text;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A testing menu: force a level up, wind the run's clock on, heal, stop
    /// taking damage, clear the arena.
    ///
    /// It creates itself and finds what it needs, the same way PerformanceOverlay
    /// does, because a tool that has to be wired into a scene is a tool that is
    /// missing from the scene you actually wanted it in. Toggled with F8.
    ///
    /// Unlike that overlay this does NOT ship. The overlay exists so a tester can
    /// report how the game runs, which is worth having in a release build; this
    /// hands out levels and invulnerability, which is not. It compiles in the
    /// editor and wherever UNITY_INCLUDE_INSTRUMENTATION is defined - that is, at
    /// Managed Code Variant Instrumented or above, which is Unity 6.6's
    /// replacement for the old DEVELOPMENT_BUILD symbol. A plain Release build
    /// does not get it. SURVIVAL_CHAOS_DEBUG_MENU forces it in regardless, for
    /// the case where you want to hand a cheat-enabled build to a playtester on
    /// purpose.
    ///
    /// This replaces the bare F7 level-up that used to live in SkillSelect.Update
    /// with no guard at all, so it was reachable by any player in a shipped build
    /// who pressed F7.
    /// </summary>
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
    public sealed class DebugMenu : MonoBehaviour
    {
        /// <summary>
        /// How often the scene is re-searched for the objects this drives.
        ///
        /// They are held between searches rather than looked up per click, and
        /// re-searched rather than held forever, because a scene reload replaces
        /// every one of them. FindAnyObjectByType is far too slow to run per
        /// frame and quite cheap once a second, and nothing here needs to notice
        /// a new Player within one frame of it existing.
        /// </summary>
        private const float RefreshInterval = 1f;

        private const float BaseWidth = 232f;
        private const float BaseRow = 24f;
        private const float BaseGap = 6f;

        private bool visible;
        private float nextRefresh;

        private Player player;
        private Timer timer;
        private WaveDirector director;
        private SkillSelect skills;

        private readonly StringBuilder status = new StringBuilder(256);
        private readonly GUIContent statusContent = new GUIContent();

        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle titleStyle;
        private Texture2D background;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameObject host = new GameObject("Debug Menu");
            host.AddComponent<DebugMenu>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            // Same reasoning as PerformanceOverlay: IMGUI builds a layout context
            // per event unless this is off, and this object lives for the whole
            // session. Everything below is drawn into explicit Rects, so there is
            // no GUILayout call to give up.
            useGUILayout = false;
        }

        private void Update()
        {
            if (GameInput.DebugMenuTogglePressed)
            {
                visible = !visible;
                if (visible)
                {
                    Refresh();
                }
            }

            // F7 still works without opening the menu, which is where it used to
            // live and is genuinely the one worth reaching for mid-run. The
            // difference is that it is behind this file's compile gate now
            // instead of running in every shipped build.
            if (GameInput.DebugLevelUpPressed)
            {
                Refresh();
                LevelUp();
            }

            if (!visible)
            {
                return;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                Refresh();
            }

            ReadShortcuts();
        }

        /// <summary>
        /// Number-key equivalents for the buttons.
        ///
        /// The buttons alone would mean taking a hand off the controls and
        /// finding a 24-pixel target while dodging, which is not a thing you can
        /// do in the situations worth testing. These read the same actions off
        /// the top-row numbers so the menu can be driven mid-run.
        ///
        /// Through GameInput rather than UnityEngine.Input, like everything else
        /// that reads a key here. This project is on the Input System package,
        /// and the legacy class throws once per call under it - which as a debug
        /// tool polling eight keys every frame meant a four-figure pile of
        /// exceptions in the console rather than one honest failure.
        /// </summary>
        private void ReadShortcuts()
        {
            switch (GameInput.DebugShortcutPressed)
            {
                case 1: LevelUp(); break;
                case 2: Advance(10f); break;
                case 3: Advance(30f); break;
                case 4: SkipToBoss(); break;
                case 5: FullHeal(); break;
                case 6: ToggleInvulnerable(); break;
                case 7: ClearArena(); break;
                case 8: StepTimeScale(); break;
            }
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshInterval;

            // Unity's fake-null makes a destroyed object compare equal to null, so
            // a stale reference from the previous scene is replaced rather than
            // silently driven.
            if (player == null) { player = FindAnyObjectByType<Player>(); }
            if (timer == null) { timer = FindAnyObjectByType<Timer>(); }
            if (director == null) { director = FindAnyObjectByType<WaveDirector>(); }
            if (skills == null) { skills = FindAnyObjectByType<SkillSelect>(); }
        }

        // ---- actions -------------------------------------------------------

        private void LevelUp()
        {
            if (skills != null)
            {
                skills.PickSkill();
            }
        }

        /// <summary>
        /// Winds both clocks on together.
        ///
        /// Two objects track the run's progress - the countdown that reveals the
        /// boss, and the director whose spawn rate ramps off its own elapsed
        /// time. Advancing one without the other is how you get a boss arriving
        /// over an opening-minute trickle of enemies, or a full-rate arena with
        /// ten minutes still on the bar. They move as a pair or not at all.
        /// </summary>
        private void Advance(float seconds)
        {
            if (timer != null) { timer.AdvanceBy(seconds); }
            if (director != null) { director.AdvanceBy(seconds); }
        }

        /// <summary>
        /// Ends the survival phase and puts the boss in the arena.
        ///
        /// Both halves, deliberately. The clock advance is what brings the health
        /// bar up and stops the countdown; the spawn is what puts something under
        /// it. Doing only the first is the state this button exists to avoid.
        /// </summary>
        private void SkipToBoss()
        {
            if (timer != null && !timer.HandedOver)
            {
                Advance(Mathf.Max(0f, timer.RunLength - timer.Elapsed));
            }

            if (director != null)
            {
                director.SpawnBossNow();
            }
        }

        private void FullHeal()
        {
            if (player != null)
            {
                player.Heal(player.MaxHealth - player.CurrentHealth);
            }
        }

        private void ToggleInvulnerable()
        {
            if (player != null)
            {
                player.Invulnerable = !player.Invulnerable;
            }
        }

        private void ClearArena()
        {
            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
            foreach (Enemy enemy in enemies)
            {
                enemy.Kill();
            }
        }

        /// <summary>
        /// Cycles slow motion, for watching a bullet pattern that is otherwise
        /// over before it can be read.
        ///
        /// It never sets zero. The death, pause and victory screens all pause by
        /// writing timeScale, and a debug tool that could also produce a stopped
        /// game would be indistinguishable from those - you would be left looking
        /// at a frozen arena with no way to tell which of the two put it there.
        /// </summary>
        private void StepTimeScale()
        {
            float current = Time.timeScale;

            if (current > 0.9f) { Time.timeScale = 0.5f; }
            else if (current > 0.4f) { Time.timeScale = 0.25f; }
            else { Time.timeScale = 1f; }
        }

        // ---- drawing -------------------------------------------------------

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            float pad = 12f * scale;
            float width = BaseWidth * scale;
            float row = BaseRow * scale;
            float gap = BaseGap * scale;

            BuildStatus();
            statusContent.text = status.ToString();
            float statusHeight = labelStyle.CalcHeight(statusContent, width);

            // Eight buttons, three separators, the title and the status block.
            float height = pad * 2f
                           + row + gap
                           + statusHeight + gap
                           + row * 8f + gap * 7f
                           + gap * 3f;

            Rect panel = new Rect(Screen.width - width - pad * 2f - pad, pad, width + pad * 2f, height);
            GUI.DrawTexture(panel, background);

            float x = panel.x + pad;
            float y = panel.y + pad;

            GUI.Label(new Rect(x, y, width, row), "DEBUG  (F8)", titleStyle);
            y += row + gap;

            GUI.Label(new Rect(x, y, width, statusHeight), statusContent, labelStyle);
            y += statusHeight + gap;

            if (Draw(x, ref y, width, row, gap, "1  Level up", skills != null)) { LevelUp(); }
            if (Draw(x, ref y, width, row, gap, "2  Advance 10s", timer != null || director != null)) { Advance(10f); }
            if (Draw(x, ref y, width, row, gap, "3  Advance 30s", timer != null || director != null)) { Advance(30f); }
            // Stays live after the handover, because the two halves can be in
            // different states - a run that reached the boss phase on its own but
            // whose boss has been killed still has a use for this.
            if (Draw(x, ref y, width, row, gap, "4  Skip to boss", timer != null || director != null)) { SkipToBoss(); }

            y += gap;

            if (Draw(x, ref y, width, row, gap, "5  Full heal", player != null)) { FullHeal(); }

            string godLabel = player != null && player.Invulnerable
                ? "6  God mode: ON"
                : "6  God mode: off";
            if (Draw(x, ref y, width, row, gap, godLabel, player != null)) { ToggleInvulnerable(); }

            y += gap;

            if (Draw(x, ref y, width, row, gap, "7  Clear arena", true)) { ClearArena(); }
            if (Draw(x, ref y, width, row, gap, "8  Time x" + Time.timeScale.ToString("0.##"), true)) { StepTimeScale(); }
        }

        /// <summary>
        /// One row. Returns true on the frame it is clicked.
        ///
        /// A row whose target is missing is drawn disabled rather than hidden.
        /// The menu is a map of what the game can be told to do, and a button
        /// that vanishes when its system is absent reads as a tool with fewer
        /// features rather than as a scene with something missing from it.
        /// </summary>
        private bool Draw(float x, ref float y, float width, float row, float gap, string label, bool enabled)
        {
            bool was = GUI.enabled;
            GUI.enabled = enabled;

            bool clicked = GUI.Button(new Rect(x, y, width, row), label, buttonStyle);

            GUI.enabled = was;
            y += row + gap;
            return clicked;
        }

        private void BuildStatus()
        {
            status.Clear();

            if (player != null)
            {
                status.Append("HP    ").Append(player.CurrentHealth)
                      .Append(" / ").Append(player.MaxHealth).Append('\n');
            }
            else
            {
                status.Append("HP    -\n");
            }

            if (timer != null)
            {
                status.Append("Run   ").Append(Mathf.FloorToInt(timer.Elapsed))
                      .Append("s / ").Append(Mathf.FloorToInt(timer.RunLength)).Append('s');
                if (timer.HandedOver)
                {
                    status.Append("  BOSS");
                }
                status.Append('\n');
            }
            else
            {
                status.Append("Run   -\n");
            }

            status.Append("Spawn ");
            if (director != null)
            {
                status.Append(Mathf.FloorToInt(director.Elapsed)).Append('s');
            }
            else
            {
                status.Append('-');
            }
        }

        private void EnsureStyles()
        {
            if (buttonStyle != null && background != null)
            {
                return;
            }

            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.78f));
            background.Apply();
            background.hideFlags = HideFlags.HideAndDontSave;

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            Font mono = Font.CreateDynamicFontFromOSFont("Consolas", 14);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = mono,
                fontSize = Mathf.RoundToInt(14f * scale),
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = Color.white;

            titleStyle = new GUIStyle(labelStyle);
            titleStyle.normal.textColor = new Color(0.45f, 0.85f, 1f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = mono,
                fontSize = Mathf.RoundToInt(14f * scale),
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };
            buttonStyle.padding = new RectOffset(Mathf.RoundToInt(8f * scale), 4, 0, 0);
        }
    }
#endif
}
