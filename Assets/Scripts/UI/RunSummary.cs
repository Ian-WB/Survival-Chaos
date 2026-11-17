using System.Text;
using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Writes the run's totals onto the screen that ends it.
    ///
    /// Both endings put up a 620x420 card carrying a title and two buttons, and
    /// leave roughly a third of it empty underneath. That gap is where this
    /// goes - the run ends with the player being asked to press Restart, and
    /// until now nothing on the way to that button said how the run had gone.
    ///
    /// Added by the two menus as they show their panel rather than authored on
    /// them, so the feature does not depend on two scene objects being wired
    /// identically by hand. Its text is built the same way, for the same reason;
    /// assign <see cref="target"/> to take that over with something styled, and
    /// nothing here will build anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSummary : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where the summary is written. Left empty, one is built inside the panel " +
                 "under the buttons.")]
        private TextMeshProUGUI target;

        [SerializeField]
        [Tooltip("Typeface for the summary. Left empty, TextMeshPro's default is used.")]
        private TMP_FontAsset font;

        /// <summary>Fills in the summary on a panel, adding this the first time.</summary>
        public static void Show(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            RunSummary summary = panel.GetComponent<RunSummary>();

            if (summary == null)
            {
                summary = panel.AddComponent<RunSummary>();
            }

            summary.Fill();
        }

        /// <summary>
        /// Also on enable, so a panel that is shown by any other route than the
        /// two menus still gets its numbers.
        /// </summary>
        private void OnEnable()
        {
            Fill();
        }

        private void Fill()
        {
            TextMeshProUGUI text = Resolve();

            if (text != null)
            {
                text.text = Compose();
            }
        }

        private string Compose()
        {
            var into = new StringBuilder();

            // What ended a lost run comes first: it is the question the card is
            // read to answer.
            if (!string.IsNullOrEmpty(RunStats.KilledBy))
            {
                into.Append("<color=#FF6B6E>").Append(LostTo(RunStats.KilledBy)).AppendLine("</color>");
            }

            // Then, where it is seen: the reason to look at the numbers under it.
            string best = Bests(RunRecords.LastBeaten);
            if (best.Length > 0)
            {
                into.Append("<color=#66FFCC>NEW BEST  -  ").Append(best).AppendLine("</color>");
            }
            else if (RunRecords.Assisted)
            {
                into.AppendLine(DebugPill);
            }

            into.AppendLine(StatsLine(Clock(RunStats.Seconds), RunStats.LevelReached,
                RunStats.EnemiesDestroyed, RunStats.ExperienceEarned));

            // Counted rather than listed. A full run's thirty-odd picks would be
            // thirty-odd lines of mostly the same eight names, and what the player
            // wants back is the shape of the build - which is the counts. One
            // chip per skill (17 November 2026), wrapping as the card's width
            // allows rather than a fixed number to a line.
            if (RunStats.SkillOrder.Count > 0)
            {
                into.Append("<line-height=160%>");

                for (int i = 0; i < RunStats.SkillOrder.Count; i++)
                {
                    string skill = RunStats.SkillOrder[i];
                    if (i > 0)
                    {
                        into.Append(" <space=8>");
                    }

                    into.Append(Chip(ShortName(skill), RunStats.PicksOf(skill)));
                }

                into.Append("</line-height>");
            }

            return into.ToString();
        }

        /// <summary>The amber pill for a run the debug menu touched.</summary>
        public const string DebugPill =
            "<size=80%><mark=#FFB8302E padding=\"12,12,4,4\"><color=#FFB830>DEBUG MENU USED - NOT A RECORD</color></mark></size>";

        /// <summary>
        /// The run's four numbers on one line: dim labels, bright values,
        /// dots between.
        /// </summary>
        public static string StatsLine(string survived, int level, int destroyed, int experience)
        {
            return "<size=85%>" + Stat("SURVIVED", survived) + Dot + Stat("LEVEL", level.ToString())
                + Dot + Stat("DESTROYED", destroyed.ToString()) + Dot + Stat("EXPERIENCE", experience.ToString())
                + "</size>";
        }

        private const string Dot = "  <alpha=#66>·<alpha=#FF>  ";

        private static string Stat(string label, string value)
        {
            return "<color=#9CD8E6>" + label + "</color> <color=#FFFFFF>" + value + "</color>";
        }

        /// <summary>
        /// One skill as a chip: its name on a dark plate and its count in
        /// gold, kept on one line.
        /// </summary>
        public static string Chip(string name, int count)
        {
            return "<nobr><size=80%><mark=#5CB8CC24 padding=\"12,12,5,5\">" + name
                + " <color=#FFD140>X" + count + "</color></mark></size></nobr>";
        }

        /// <summary>How many skills the card lists on one line.</summary>
        public const int SkillsPerLine = 3;

        /// <summary>
        /// A skill as the card names it. Skills are recorded under the words
        /// their level-up banner announces - "Attack Speed Increased!",
        /// "Deflector Online!" - and the card listed them like that, each
        /// with its exclamation mark (12 November 2026). Here the announcement
        /// is taken off and the name set in capitals like the lines above it.
        /// </summary>
        public static string ShortName(string announced)
        {
            if (string.IsNullOrEmpty(announced))
            {
                return string.Empty;
            }

            string name = announced.Trim().TrimEnd('!').Trim();

            foreach (string tail in AnnouncementTails)
            {
                if (name.Length > tail.Length && name.EndsWith(tail, System.StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - tail.Length);
                    break;
                }
            }

            return name.ToUpperInvariant();
        }

        private static readonly string[] AnnouncementTails = { " Increased", " Online", " Recovers Faster" };

        /// <summary>The Ship Lost card's first line: "LOST TO A LEVIATHAN TORPEDO".</summary>
        public static string LostTo(string source)
        {
            return "LOST TO " + source.ToUpperInvariant();
        }

        /// <summary>The records a run beat, as the card names them. Empty for none.</summary>
        public static string Bests(RunRecords.Beaten beaten)
        {
            var names = new StringBuilder();

            void Add(RunRecords.Beaten flag, string name)
            {
                if ((beaten & flag) == 0)
                {
                    return;
                }

                if (names.Length > 0)
                {
                    names.Append(", ");
                }

                names.Append(name);
            }

            Add(RunRecords.Beaten.Longest, "LONGEST RUN");
            Add(RunRecords.Beaten.Level, "HIGHEST LEVEL");
            Add(RunRecords.Beaten.Fastest, "FASTEST WIN");
            return names.ToString();
        }

        /// <summary>Minutes and seconds, because runs are ten minutes long.</summary>
        private static string Clock(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60) + ":" + (whole % 60).ToString("00");
        }

        private TextMeshProUGUI Resolve()
        {
            if (target != null)
            {
                return target;
            }

            target = Build();
            return target;
        }

        /// <summary>
        /// Builds the summary text inside the card.
        ///
        /// Parented to the child named Panel rather than to this object, which is
        /// the full-screen dimmer behind it - text anchored to that would be
        /// centred on the screen rather than on the card. Both panels are built
        /// the same way; if one day one is not, the fallback puts the text on the
        /// dimmer, which is visible and wrong rather than absent and silent.
        ///
        /// Placed at -288 from the top of a 420-tall card, which is the first
        /// clear line under the Main Menu button at -238 plus its own height.
        /// </summary>
        private TextMeshProUGUI Build()
        {
            Transform card = transform.Find("Panel");
            Transform host = card != null ? card : transform;

            var holder = new GameObject("Run Summary", typeof(RectTransform));
            holder.transform.SetParent(host, worldPositionStays: false);

            var text = holder.AddComponent<TextMeshProUGUI>();

            // The card's own typeface. Left to TextMeshPro's default, the
            // numbers were in Liberation Sans under a title and buttons in
            // the HUD's font.
            TMP_FontAsset face = font != null ? font : BorrowFont(host);
            if (face != null)
            {
                text.font = face;
            }

            text.alignment = TextAlignmentOptions.Top;
            text.fontSize = 18f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = 18f;
            text.color = new Color(0.72f, 0.95f, 1f, 1f);
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(540f, 130f);
            rect.anchoredPosition = new Vector2(0f, -288f);

            return text;
        }

        /// <summary>The font of the first text already on the card: its title.</summary>
        private static TMP_FontAsset BorrowFont(Transform card)
        {
            foreach (TextMeshProUGUI other in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (other.font != null && other.gameObject.name != "Run Summary")
                {
                    return other.font;
                }
            }

            return null;
        }
    }
}
