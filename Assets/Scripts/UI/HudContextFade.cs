using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Fades one HUD element back when it has nothing to say, and brings it up
    /// the moment it does (17 November 2026 redesign, gemini-spec section 4):
    ///
    /// - experience: 30% when idle, full on a gain, at 80% full or more, and
    ///   for 2.5 s after;
    /// - hull: dims to 65% only after 4 s untouched at full, never hides, full
    ///   whenever damaged and through the boss fight;
    /// - an ability: 30% while full and ready, full while it drains or
    ///   recharges and for 0.6 s after.
    ///
    /// Under ALWAYS ON (HUD options) nothing rests below 85%.
    ///
    /// Works through a CanvasGroup when it has one, or else straight on a list
    /// of graphics through their CanvasRenderers: the hull plate has the
    /// docked abilities as children, and a CanvasGroup there would dim them
    /// with it.
    /// </summary>
    [AddComponentMenu("Survival Chaos/HUD Context Fade")]
    public sealed class HudContextFade : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where the value comes from: a Slider (hull) ...")]
        private Slider slider;

        [SerializeField]
        [Tooltip("... or an Image's fill (experience, the abilities).")]
        private Image fill;

        [SerializeField]
        [Tooltip("What fades, when it can fade as one. Optional.")]
        private CanvasGroup group;

        [SerializeField]
        [Tooltip("What fades, one by one, when it cannot (no CanvasGroup).")]
        private List<Graphic> graphics = new List<Graphic>();

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Strength at rest under SMART.")]
        private float rest = 0.3f;

        [SerializeField]
        [Tooltip("Seconds it stays up after the last reason to.")]
        private float hold = 2.5f;

        [SerializeField]
        private float fadeIn = 0.15f;

        [SerializeField]
        private float fadeOut = 0.8f;

        [SerializeField]
        [Tooltip("Up whenever the value changes.")]
        private bool wakeOnChange = true;

        [SerializeField]
        [Tooltip("Up whenever the value is below full (the hull while damaged; an ability while " +
                 "it drains or recharges).")]
        private bool awakeBelowFull;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Up whenever the value is at or above this (experience nearly at a level). " +
                 "Above 1 never.")]
        private float awakeAtOrAbove = 2f;

        [SerializeField]
        [Tooltip("Up whenever this is active in the hierarchy (the hull during the boss fight). " +
                 "Optional.")]
        private GameObject awakeWhileActive;

        private float lastValue = -1f;
        private float lastAwake = -100f;
        private float shown = 1f;

        /// <summary>
        /// The strength an element should show at: full while it has a reason,
        /// held for <paramref name="hold"/> after, then easing to its rest.
        /// </summary>
        public static float TargetFor(bool awake, float sinceAwake, float hold, float rest)
        {
            return awake || sinceAwake < hold ? 1f : rest;
        }

        private float Read()
        {
            if (slider != null)
            {
                return slider.normalizedValue;
            }

            return fill != null ? fill.fillAmount : 1f;
        }

        /// <summary>Brings it up now, as if its value had just changed: an offer near the ship points at it.</summary>
        public void Wake()
        {
            lastAwake = Time.unscaledTime;
        }

        private void OnEnable()
        {
            lastValue = Read();
            lastAwake = Time.unscaledTime;
            shown = 1f;
            Apply(1f);
        }

        private void LateUpdate()
        {
            float value = Read();
            bool changed = wakeOnChange && !Mathf.Approximately(value, lastValue);
            lastValue = value;

            bool awake = changed
                || (awakeBelowFull && value < 0.999f)
                || value >= awakeAtOrAbove
                || (awakeWhileActive != null && awakeWhileActive.activeInHierarchy);

            float now = Time.unscaledTime;
            if (awake)
            {
                lastAwake = now;
            }

            float target = TargetFor(awake, now - lastAwake, hold, HudSettings.Rest(rest, HudSettings.Mode));
            float seconds = target > shown ? fadeIn : fadeOut;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
            Apply(shown);
        }

        private void Apply(float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
                return;
            }

            for (int i = 0; i < graphics.Count; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].canvasRenderer.SetAlpha(alpha);
                }
            }
        }
    }
}
