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

            into.Append("SURVIVED ").AppendLine(Clock(RunStats.Seconds));
            into.Append("LEVEL ").AppendLine(RunStats.LevelReached.ToString());
            into.Append("DESTROYED ").AppendLine(RunStats.EnemiesDestroyed.ToString());
            into.Append("EXPERIENCE ").AppendLine(RunStats.ExperienceEarned.ToString());

            // Counted rather than listed. Twenty picks is twenty lines of mostly
            // the same four words, and what the player wants back is the shape of
            // the build - which is the counts.
            if (RunStats.SkillOrder.Count > 0)
            {
                into.AppendLine();

                for (int i = 0; i < RunStats.SkillOrder.Count; i++)
                {
                    string skill = RunStats.SkillOrder[i];

                    if (i > 0)
                    {
                        into.Append("   ");
                    }

                    into.Append(skill).Append(" x").Append(RunStats.PicksOf(skill));
                }
            }

            return into.ToString();
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

            if (font != null)
            {
                text.font = font;
            }

            text.alignment = TextAlignmentOptions.Top;
            text.fontSize = 22f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(540f, 130f);
            rect.anchoredPosition = new Vector2(0f, -288f);

            return text;
        }
    }
}
