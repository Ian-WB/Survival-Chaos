using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Shows how much of the dash's cooldown has come back.
    ///
    /// The dash was built with <see cref="PlayerDash.ReadyFraction"/> exposed for
    /// exactly this and nothing reading it, which was survivable while the dash
    /// was a convenience. It stopped being survivable when the boss got a ram:
    /// that attack is faster round the ring than the player and taller than the
    /// band they fly in, so the dash is not *a* counter to it, it is the only
    /// one. Asking someone to commit to a 0.22 second window with no way of
    /// knowing whether the window exists is asking them to guess.
    ///
    /// Writes an Image's fill rather than a Slider, the way the experience bar
    /// does, so <see cref="HoloBar"/> picks it up through the same fallback and
    /// the shader draws it like every other bar on the screen.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class DashBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The dash this reads. Wired by the HUD builder; found in the scene when empty.")]
        private PlayerDash dash;

        private Image fill;

        private void Awake()
        {
            fill = GetComponent<Image>();

            if (dash == null)
            {
                // The player is in the scene from the start, so this is a
                // misconfiguration rather than a race - but the HUD is generated,
                // and a generated reference is exactly the kind that goes missing
                // quietly after a rename.
                dash = FindAnyObjectByType<PlayerDash>(FindObjectsInactive.Include);
            }

            if (dash == null)
            {
                Debug.LogWarning(
                    "DashBar found no PlayerDash, so the dash meter would sit still. " +
                    "Rebuild the HUD, or assign it by hand.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// Written every frame rather than on change, because the value is a
        /// continuous ramp - there is no event to hang it off, and the whole
        /// point is watching it climb.
        ///
        /// In Update rather than LateUpdate so HoloBar, which reads this in its
        /// own LateUpdate, sees the current frame's value instead of the previous
        /// frame's.
        /// </summary>
        private void Update()
        {
            fill.fillAmount = dash.ReadyFraction;
        }
    }
}
