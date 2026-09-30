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

        private void OnEnable()
        {
            TMP_Text text = GetComponent<TMP_Text>();
            string line = content == Content.BuildStamp ? SurvivalChaos.BuildStamp.Text : RunRecords.Describe();

            text.text = line;
            text.enabled = line.Length > 0;
        }
    }
}
