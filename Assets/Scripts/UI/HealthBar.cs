using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// The player's health bar, which flinches when the number goes down.
    ///
    /// The bar rather than the screen. A screen shake says something happened
    /// somewhere; this says what happened and where to look, and it cannot be
    /// mistaken for the arena moving - which matters in a game whose camera is
    /// already orbiting. It also needs no accessibility switch of its own: a
    /// 38-pixel bar moving six pixels in the corner is not what motion sensitivity
    /// settings exist to protect against.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {

        [SerializeField]
        private Slider slider;

        [Header("Hit shake")]
        [SerializeField]
        [Range(0f, 40f)]
        [Tooltip("How far the bar is thrown at the start of a hit, in pixels. Zero switches " +
                 "the flinch off without removing it.")]
        private float shakeDistance = 6f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("How long the flinch lasts, in seconds. It decays to nothing across this, " +
                 "so the first frame is the loudest.")]
        private float shakeSeconds = 0.22f;

        /// <summary>
        /// Where the bar sits when nothing is happening to it.
        ///
        /// Captured once, at Awake, rather than when a shake begins. Hits land
        /// close together at the end of a run, and a second one starting while the
        /// first is still running would otherwise record the thrown position as
        /// the resting one - and the bar would walk out of its corner a few pixels
        /// per hit with nothing to put it back. HitFlash hands the hull back to
        /// its shared material, rather than a remembered colour, for the same reason.
        /// </summary>
        private Vector2 resting;

        private RectTransform shaken;
        private Coroutine shaking;

        [Header("Last hit point")]
        [SerializeField]
        [Min(0)]
        [Tooltip("At or below this many hit points the bar pulses at full strength and a heartbeat " +
                 "plays. The dial: 1 is the last hit point, 2 starts a hit earlier. 0 turns it off.")]
        private int warnAtOrBelow = 1;

        [SerializeField]
        [Range(0.4f, 2f)]
        [Tooltip("Game seconds between heartbeats. Game time, so it waits out a pause and slows " +
                 "with Slow Mo.")]
        private float heartbeatSeconds = 0.9f;

        [Header("Size")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Bar length per hit point, in canvas units, so every Max HP pick makes the bar " +
                 "longer. 11 puts the starting 20 at 220 and two picks level with the row above. " +
                 "0 keeps the length the bar was built with.")]
        private float pixelsPerHitPoint = 11f;

        [SerializeField]
        [Min(1)]
        [Tooltip("Hit points per segment of the bar. The segments keep their width as the bar " +
                 "grows, so a longer bar has more of them rather than wider ones.")]
        private int hitPointsPerSegment = 2;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Seconds the bar takes to grow to its new length after a Max HP pick.")]
        private float growSeconds = 0.35f;

        private HoloBar holo;
        private float nextBeat;
        private bool warning;
        private Coroutine growing;

        /// <summary>The bar's length for a maximum, in canvas units.</summary>
        public static float WidthFor(int maxHealth, float pixelsPerHitPoint)
        {
            return Mathf.Max(0, maxHealth) * Mathf.Max(0f, pixelsPerHitPoint);
        }

        /// <summary>
        /// How many segments a bar of this length is ruled into: fractional
        /// while it grows, so the ticks hold still on screen and the new
        /// stretch arrives with its own.
        /// </summary>
        public static float SegmentsFor(float width, float pixelsPerHitPoint, int hitPointsPerSegment)
        {
            float segment = Mathf.Max(0.0001f, pixelsPerHitPoint * Mathf.Max(1, hitPointsPerSegment));
            return Mathf.Max(1f, width / segment);
        }

        private void Awake()
        {
            holo = slider != null ? slider.GetComponent<HoloBar>() : null;
            if (holo == null)
            {
                holo = GetComponentInChildren<HoloBar>();
            }

            shaken = transform as RectTransform;

            if (shaken != null)
            {
                resting = shaken.anchoredPosition;
            }
        }

        public void SetMaxHealth(int health){
            slider.maxValue = health;
            slider.value = health;
            Resize(health, animate: false);
        }

        /// <summary>
        /// A Max HP pick: the bar grows by what was added, rather than the same
        /// bar re-ruled to a bigger number (Ian, 16 November 2026). Before this
        /// the pick only moved the fill, which a full bar does not show at all.
        /// </summary>
        public void AddMaxHealth(int health){
            slider.maxValue += health;
            slider.value += health;
            Resize(Mathf.RoundToInt(slider.maxValue), animate: true);
        }

        private void Resize(int maxHealth, bool animate)
        {
            if (shaken == null || pixelsPerHitPoint <= 0f)
            {
                return;
            }

            float width = WidthFor(maxHealth, pixelsPerHitPoint);

            if (growing != null)
            {
                StopCoroutine(growing);
                growing = null;
            }

            if (!animate || !isActiveAndEnabled)
            {
                SetWidth(width);
                return;
            }

            growing = StartCoroutine(Growing(shaken.sizeDelta.x, width));
        }

        private IEnumerator Growing(float from, float to)
        {
            float elapsed = 0f;

            while (elapsed < growSeconds)
            {
                // Unscaled for the reason the shake is: a pick taken just as the
                // run ends would otherwise leave the bar half grown behind the card.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / growSeconds);
                SetWidth(Mathf.Lerp(from, to, 1f - (1f - t) * (1f - t)));
                yield return null;
            }

            SetWidth(to);
            growing = null;
        }

        private void SetWidth(float width)
        {
            shaken.sizeDelta = new Vector2(width, shaken.sizeDelta.y);

            if (holo != null)
            {
                holo.SetSegments(SegmentsFor(width, pixelsPerHitPoint, hitPointsPerSegment));
            }
        }

        /// <summary>
        /// Sets the bar, and flinches if this was damage.
        ///
        /// Damage rather than every write, and worked out here rather than asked
        /// of the caller. Player.Heal and the Max Health skill both arrive through
        /// this same method, and a bar that flinches when the player is healed
        /// says the opposite of what happened. Deciding it from the value means no
        /// future damage path has to remember to ask.
        /// </summary>
        public void SetHealth(int health){
            bool hurt = slider != null && health < slider.value;

            slider.value = health;

            if (hurt)
            {
                Shake();
            }
        }

        /// <summary>
        /// The warning on the last hit point: the bar pulsing at full strength,
        /// and a soft heartbeat until salvage takes the ship off it. The bar in
        /// the corner was the only sign before, and a player who is dodging is
        /// watching the ship. Only while there is more health to have lost: a
        /// ship with one hit point to its name would beat the whole run.
        /// </summary>
        private void Update()
        {
            bool now = slider != null && warnAtOrBelow > 0
                && slider.value > 0f
                && slider.value <= warnAtOrBelow
                && slider.maxValue > warnAtOrBelow
                && !RunOutcome.Decided;

            if (now != warning)
            {
                warning = now;
                nextBeat = Time.time;

                if (holo != null)
                {
                    holo.PulseFloor = now ? 1f : 0f;
                }
            }

            if (!warning || PauseMenu.GameIsPaused || Time.time < nextBeat)
            {
                return;
            }

            nextBeat = Time.time + heartbeatSeconds;
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.LowHealth);
            }
        }

        private void Shake()
        {
            if (shaken == null || shakeDistance <= 0f)
            {
                return;
            }

            // Restart rather than stack. Two coroutines would each be writing
            // anchoredPosition every frame, and the one that finished first would
            // put the bar back while the other was still throwing it.
            if (shaking != null)
            {
                StopCoroutine(shaking);
            }

            shaking = StartCoroutine(Shaking());
        }

        private IEnumerator Shaking()
        {
            float elapsed = 0f;

            while (elapsed < shakeSeconds)
            {
                // Unscaled, and here that is not a nicety. The hit that kills runs
                // this and then puts the death screen up in the same frame, which
                // stops time outright - so on scaled time the bar would freeze
                // mid-throw, sitting visibly off its corner behind the death
                // screen for as long as the screen was up. The last hit of a run
                // is the one this fires on most memorably.
                elapsed += Time.unscaledDeltaTime;

                float left = 1f - Mathf.Clamp01(elapsed / shakeSeconds);

                // A shrinking circle rather than a decaying sine on one axis: a
                // single axis reads as the bar sliding, and the throw should have
                // no direction the player could mistake for information.
                shaken.anchoredPosition = resting + Random.insideUnitCircle * (shakeDistance * left);

                yield return null;
            }

            shaken.anchoredPosition = resting;
            shaking = null;
        }
    }
}
