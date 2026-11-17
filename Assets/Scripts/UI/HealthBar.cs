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

        [SerializeField]
        [Min(0f)]
        [Tooltip("The longest the bar grows, in canvas units. Past it a Max HP pick packs more, " +
                 "narrower segments into the same length. 0 for no cap.")]
        private float maxWidth = 0f;

        [Header("Plate and count")]
        [SerializeField]
        [Tooltip("The dark plate behind the bar, kept this much wider than the bar on each side " +
                 "as it grows. Optional.")]
        private RectTransform plate;

        [SerializeField]
        [Min(0f)]
        private float platePadding = 12f;

        [SerializeField]
        [Tooltip("Reads the hull as \"18 / 20\", the current number in the stage colour. Optional.")]
        private TMPro.TMP_Text count;

        [Header("Colour stages")]
        [SerializeField]
        [Tooltip("Hull fill above the caution line (#2EE57D): green means hull, and nothing else.")]
        private Color healthyColour = new Color(0.180f, 0.898f, 0.490f, 1f);

        [SerializeField]
        [Tooltip("Hull fill at or below 25% (#FFB830).")]
        private Color cautionColour = new Color(1.000f, 0.722f, 0.188f, 1f);

        [SerializeField]
        [Tooltip("Hull fill at or below 15% (#FF3B4E).")]
        private Color criticalColour = new Color(1.000f, 0.231f, 0.306f, 1f);

        /// <summary>How worried the hull bar should look.</summary>
        public enum Stage { Healthy, Caution, Critical }

        /// <summary>
        /// The stage for a hull reading: caution at or below 25% of the
        /// maximum, critical at or below 15%. Whole-number maths, so at 20
        /// hit points 5 is caution and 3 critical with no rounding doubt.
        /// </summary>
        public static Stage StageFor(int health, int maxHealth)
        {
            if (maxHealth <= 0)
            {
                return Stage.Healthy;
            }

            if (health * 100 <= maxHealth * 15)
            {
                return Stage.Critical;
            }

            return health * 100 <= maxHealth * 25 ? Stage.Caution : Stage.Healthy;
        }

        /// <summary>The fill colour for a stage.</summary>
        public Color ColourFor(Stage stage)
        {
            return stage == Stage.Critical ? criticalColour
                : stage == Stage.Caution ? cautionColour
                : healthyColour;
        }

        [Header("Low-hull edge vignette")]
        [SerializeField]
        [Tooltip("Full-screen edge glow (Edge Glow shader) whose alpha this drives. Optional.")]
        private Graphic vignette;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Peak strength of the slow breath at amber.")]
        private float vignetteCaution = 0.22f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Strength between heartbeats at crimson.")]
        private float vignetteCritical = 0.55f;

        [SerializeField]
        [Range(0.1f, 3f)]
        private float breatheHz = 0.9f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How much each heartbeat adds at crimson.")]
        private float beatKick = 0.45f;

        [SerializeField]
        [Range(0.5f, 20f)]
        [Tooltip("How fast the heartbeat's flash settles, per second.")]
        private float beatDecay = 4f;

        [SerializeField]
        [Tooltip("Vignette tint at amber: a dark warm amber-red. Linear values, as the UI writes them.")]
        private Color vignetteCautionTint = new Color(0.20f, 0.045f, 0.004f, 1f);

        [SerializeField]
        [Tooltip("Vignette tint at crimson: a dark crimson (about #5A1825 on screen).")]
        private Color vignetteCriticalTint = new Color(0.16f, 0.006f, 0.02f, 1f);

        private float vignetteShown;
        private float lastBeat = -10f;
        private Color vignetteTint;

        private HoloBar holo;
        private Stage? shownStage;
        private float nextBeat;
        private bool warning;
        private Coroutine growing;

        /// <summary>The bar's length for a maximum, in canvas units.</summary>
        public static float WidthFor(int maxHealth, float pixelsPerHitPoint)
        {
            return Mathf.Max(0, maxHealth) * Mathf.Max(0f, pixelsPerHitPoint);
        }

        /// <summary>The bar's length for a maximum, held at a cap (0 for none).</summary>
        public static float WidthFor(int maxHealth, float pixelsPerHitPoint, float cap)
        {
            float width = WidthFor(maxHealth, pixelsPerHitPoint);
            return cap > 0f ? Mathf.Min(width, cap) : width;
        }

        /// <summary>"18 / 20", with the current number in its stage colour.</summary>
        public static string CountText(int health, int maxHealth, Color colour)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">" + Mathf.Max(0, health) +
                   "</color> / " + Mathf.Max(0, maxHealth);
        }

        private int ShownMax => slider != null ? Mathf.RoundToInt(slider.maxValue) : 0;

        /// <summary>
        /// Length per hit point as drawn: the set rate until the cap, then the
        /// cap shared out, so the segments always stand for the same two hit
        /// points.
        /// </summary>
        private float DrawnPerPoint
        {
            get
            {
                int max = ShownMax;
                return max > 0 ? WidthFor(max, pixelsPerHitPoint, maxWidth) / max : pixelsPerHitPoint;
            }
        }

        private void RefreshCount()
        {
            if (count == null || slider == null)
            {
                return;
            }

            int health = Mathf.RoundToInt(slider.value);
            count.text = CountText(health, ShownMax, ColourFor(StageFor(health, ShownMax)));
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
                RefreshCount();
                return;
            }

            float width = WidthFor(maxHealth, pixelsPerHitPoint, maxWidth);
            RefreshCount();

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

            if (plate != null)
            {
                plate.sizeDelta = new Vector2(width + 2f * platePadding, plate.sizeDelta.y);
            }

            if (holo != null)
            {
                holo.SetSegments(SegmentsFor(width, DrawnPerPoint, hitPointsPerSegment));
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
            RefreshCount();

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
            if (slider != null && holo != null)
            {
                Stage stage = StageFor(Mathf.RoundToInt(slider.value), Mathf.RoundToInt(slider.maxValue));
                if (stage != shownStage)
                {
                    shownStage = stage;
                    holo.SetFillColour(ColourFor(stage));
                    RefreshCount();
                }
            }

            Stage current = slider != null
                ? StageFor(Mathf.RoundToInt(slider.value), Mathf.RoundToInt(slider.maxValue))
                : Stage.Healthy;

            // The heartbeat and the bar's full pulse start at the crimson line
            // (17 November 2026 redesign), not on the last hit point. The old
            // dial still adds hit points below that line on a very small hull.
            bool now = slider != null
                && slider.value > 0f
                && (current == Stage.Critical
                    || (warnAtOrBelow > 0 && slider.value <= warnAtOrBelow && slider.maxValue > warnAtOrBelow))
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

            UpdateVignette(RunOutcome.Decided || slider == null || slider.value <= 0f ? Stage.Healthy : current);

            if (!warning || PauseMenu.GameIsPaused || Time.time < nextBeat)
            {
                return;
            }

            lastBeat = Time.time;
            nextBeat = Time.time + heartbeatSeconds;
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.LowHealth);
            }
        }

        /// <summary>
        /// How strong the edge vignette is for a stage at a moment: nothing
        /// above a quarter, a slow breath at amber, and at crimson a base that
        /// jumps on each heartbeat and settles before the next.
        /// </summary>
        public static float VignetteFor(Stage stage, float time, float sinceBeat,
            float caution, float critical, float breatheHz, float beatKick, float beatDecay)
        {
            switch (stage)
            {
                case Stage.Caution:
                    float breath = 0.5f - 0.5f * Mathf.Cos(time * breatheHz * 2f * Mathf.PI);
                    return caution * Mathf.Lerp(0.55f, 1f, breath);
                case Stage.Critical:
                    return Mathf.Clamp01(critical + beatKick * Mathf.Exp(-beatDecay * Mathf.Max(0f, sinceBeat)));
                default:
                    return 0f;
            }
        }

        private void UpdateVignette(Stage stage)
        {
            if (vignette == null)
            {
                return;
            }

            float target = VignetteFor(stage, Time.time, Time.time - lastBeat,
                vignetteCaution, vignetteCritical, breatheHz, beatKick, beatDecay);

            // In quickly, out slowly: 0.25 s and 0.4 s.
            float rate = target > vignetteShown ? 1f / 0.25f : 1f / 0.4f;
            float settle = stage == Stage.Healthy || Mathf.Abs(target - vignetteShown) > 0.25f
                ? rate * Time.unscaledDeltaTime
                : 1f;
            vignetteShown = Mathf.MoveTowards(vignetteShown, target, settle);

            // The tint follows the stage it is showing; on the way out it
            // keeps the last one rather than turning colour as it fades.
            if (stage != Stage.Healthy)
            {
                vignetteTint = stage == Stage.Critical ? vignetteCriticalTint : vignetteCautionTint;
            }

            Color c = vignetteTint;
            c.a = vignetteShown;
            if (vignette.color != c)
            {
                vignette.color = c;
            }

            bool on = vignetteShown > 0.001f;
            if (vignette.enabled != on)
            {
                vignette.enabled = on;
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
