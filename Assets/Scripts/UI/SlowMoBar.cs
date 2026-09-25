using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Shows Slow Mo's gauge, and nothing at all until the ability has been
    /// taken - the way <see cref="DeflectorBar"/> shows the deflector.
    ///
    /// The bar drains while the game is slowed and fills over the wait after,
    /// so full means "press it" and falling means "this is how long is left".
    /// See <see cref="SlowMoCycle.Gauge"/>.
    ///
    /// Sits on a parent that stays active, because hiding the bar's own object
    /// would stop this Update as well and the bar could never come back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlowMoBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The ability this reads. Wired by the HUD builder; found in the scene when empty.")]
        private PlayerSlowMo slowMo;

        [SerializeField]
        [Tooltip("The bar. Its Image fill is written the way DashBar writes the dash's, for " +
                 "HoloBar to draw.")]
        private Image fill;

        [SerializeField]
        [Tooltip("Shown and hidden with the bar.")]
        private GameObject label;

        /// <summary>
        /// What is on screen now. Starts true so the first Update, which always
        /// finds nothing held, actually hides what the builder left visible.
        /// </summary>
        private bool shown = true;

        private void Awake()
        {
            if (slowMo == null)
            {
                slowMo = FindAnyObjectByType<PlayerSlowMo>(FindObjectsInactive.Include);
            }

            if (slowMo == null || fill == null)
            {
                Debug.LogWarning(
                    "SlowMoBar is missing its ability or its bar, so Slow Mo will not show. " +
                    "Rebuild the HUD, or assign them by hand.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// In Update rather than LateUpdate so HoloBar, which reads the fill in
        /// its own LateUpdate, sees this frame's value.
        /// </summary>
        private void Update()
        {
            bool held = slowMo.Held;

            if (held != shown)
            {
                Show(held);
            }

            if (held)
            {
                fill.fillAmount = slowMo.Gauge;
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
