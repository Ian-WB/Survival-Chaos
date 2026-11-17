using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A small line of text on a menu that fills itself in when the menu
    /// opens: which build this is, or the player's best runs.
    ///
    /// Filled on enable rather than once, so the title screen shows a record
    /// set by the run the player has just come back from.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Menu Footnote")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class MenuFootnote : MonoBehaviour
    {
        public enum Content
        {
            /// <summary>The build folder and commit, from <see cref="SurvivalChaos.BuildStamp"/>.</summary>
            BuildStamp,

            /// <summary>The best runs, from <see cref="RunRecords"/>. Hidden when there are none.</summary>
            Bests
        }

        [SerializeField]
        private Content content = Content.BuildStamp;

        [SerializeField]
        [Tooltip("Optional. A frame behind the line, hidden with it when there is nothing to say.")]
        private GameObject plate;

        /// <summary>
        /// The bests line with its leading BEST as a gold tag and a rule after
        /// it (17 November 2026 redesign). Anything else is left as it is.
        /// </summary>
        public static string TagBest(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("BEST"))
            {
                return line ?? string.Empty;
            }

            return "<color=#FFD140>BEST</color>  <alpha=#55>|<alpha=#FF> " + line.Substring(4);
        }

        private void OnEnable()
        {
            TMP_Text text = GetComponent<TMP_Text>();
            string line = content == Content.BuildStamp ? SurvivalChaos.BuildStamp.Text : RunRecords.Describe();

            if (content == Content.Bests)
            {
                line = TagBest(line);
            }

            text.text = line;
            text.enabled = line.Length > 0;

            if (plate != null)
            {
                plate.SetActive(line.Length > 0);
            }
        }
    }
}
