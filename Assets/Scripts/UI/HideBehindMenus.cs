using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Takes the run HUD off the screen while a menu is open: pause, options,
    /// the end cards (17 November 2026 redesign, block 8). Behind the pause
    /// card the timer, hull and level bars only competed with the card, and a
    /// level-up banner caught mid-fade sat over the options panel's top edge.
    ///
    /// Works through a CanvasGroup on this object, so it multiplies with the
    /// HUD options' opacity on the frame rather than fighting it.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Hide Behind Menus")]
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class HideBehindMenus : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds to fade out as a menu opens, and back in as it closes. Unscaled: pause stops the clock.")]
        private float fade = 0.12f;

        private CanvasGroup group;

        /// <summary>Strength the HUD shows at with this many screens open.</summary>
        public static float TargetFor(int openScreens) => openScreens > 0 ? 0f : 1f;

        private void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = TargetFor(MenuScreen.OpenCount);
        }

        private void OnDisable()
        {
            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        private void LateUpdate()
        {
            float target = TargetFor(MenuScreen.OpenCount);
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fade));
        }
    }
}
