using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Shows the Deflector's charge, and nothing at all until the upgrade has
    /// been taken.
    ///
    /// Half of the feedback for a blocked hit; DeflectorShield's ring bursting
    /// on the ship is the other. The hull bar does not move and there is no
    /// sound of its own, so this dropping from full to empty, with the red loss
    /// trail every other bar uses for a hit, says what the burst was. It also
    /// says when the next one is covered, which is the thing the upgrade asks
    /// the player to play for.
    ///
    /// Sits on a parent that stays active, because hiding the bar's own object
    /// would stop this Update as well and the bar could never come back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeflectorBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The player whose deflector this reads. Wired by the HUD builder; found in the " +
                 "scene when empty.")]
        private Player player;

        [SerializeField]
        [Tooltip("The bar. Its Image fill is written the way DashBar writes the dash's, for " +
                 "HoloBar to draw.")]
        private Image fill;

        [SerializeField]
        [Tooltip("Shown and hidden with the bar.")]
        private GameObject label;

        /// <summary>
        /// What is on screen now. Starts true so the first Update, which always
        /// finds no deflector, actually hides what the builder left visible.
        /// </summary>
        private bool shown = true;

        private void Awake()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
            }

            if (player == null || fill == null)
            {
                Debug.LogWarning(
                    "DeflectorBar is missing its player or its bar, so the deflector's charge will " +
                    "not show. Rebuild the HUD, or assign them by hand.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// In Update rather than LateUpdate so HoloBar, which reads the fill in
        /// its own LateUpdate, sees this frame's value.
        /// </summary>
        private void Update()
        {
            bool held = player.HasDeflector;

            if (held != shown)
            {
                Show(held);
            }

            if (held)
            {
                fill.fillAmount = player.DeflectorReadyFraction;
            }
        }

        private void Show(bool on)
        {
            shown = on;
            fill.gameObject.SetActive(on);

            if (label != null)
            {
                label.SetActive(on);
            }
        }
    }
}
