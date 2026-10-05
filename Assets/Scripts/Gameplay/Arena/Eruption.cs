using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// The volcano as the run's clock: quiet as a run starts, at full when the
    /// Leviathan arrives, with a swell each time a new group of enemies does.
    ///
    /// Environment roadmap, item 37, 5 October 2026. Until then the lava's
    /// glow, the crater's light and the plume were the same at second 1 and
    /// second 399. This adds nothing to the scene: it moves numbers on things
    /// that were already drawn, so it costs no frame time of its own.
    ///
    /// The level comes from <see cref="EruptionCurve"/> and the run's clock,
    /// which is the WaveDirector's, so the debug menu's Advance and Skip to
    /// boss move the volcano with everything else.
    ///
    /// Every range below is a multiple of what the scene has authored, at rest
    /// and at full. The scene's own numbers are therefore still the look - the
    /// lava's is met about four fifths of the way up - and retuning the lava
    /// or the smoke by hand retunes the whole eruption with it.
    ///
    /// What does not join in is the island. Its lava light is baked, and baked
    /// light cannot brighten. Nor do the lava lights along the flow: two of
    /// the six are baked only, so raising the rest would light one side of the
    /// island and not the other.
    ///
    /// It changes no rule of the fight and does not move the camera.
    /// </summary>
    public sealed class Eruption : MonoBehaviour
    {
        [Header("What it drives")]
        [SerializeField]
        [Tooltip("The lava's renderer. Its glow, how much of it is molten and how fast it flows follow the level.")]
        private Renderer lava;

        [SerializeField]
        [Tooltip("The light in the crater's mouth, which is what reddens the plume.")]
        private Light craterLight;

        [SerializeField]
        [Tooltip("The plume: the fog volume over the crater. It gets taller and thicker.")]
        private LocalVolumetricFog smoke;

        [SerializeField]
        [Tooltip("The warm light on the ships from the island's axis. Leave empty to keep the ships out of it.")]
        private Light shipFill;

        [Header("The clock")]
        [SerializeField]
        [Range(0.5f, 3f)]
        [Tooltip("How late the volcano gets loud. 1 climbs evenly across the run. Higher keeps the first minutes quiet and saves the climb for the end; lower is loud early.")]
        private float lateness = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The level it has reached, between surges, as the Leviathan is due. The Leviathan takes it the rest of the way to 1.")]
        private float restingPeak = 0.75f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("How much a surge adds at its height.")]
        private float surgeHeight = 0.25f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds a surge takes to swell.")]
        private float surgeRise = 0.8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds a surge takes to die away.")]
        private float surgeFall = 9f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("A stream starting after this many seconds in which none did sets off a surge, as the first arrival of each kind of enemy does.")]
        private float quietGap = 30f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the volcano takes to reach full once the Leviathan is out.")]
        private float arrivalSeconds = 3f;

        [Header("At rest (X) and at full (Y), as multiples of what the scene has")]
        [SerializeField]
        [Tooltip("How bright the lava's hot parts are.")]
        private Vector2 glow = new Vector2(0.2f, 1.5f);

        [SerializeField]
        [Tooltip("How much of the lava is molten rather than crust.")]
        private Vector2 molten = new Vector2(0.6f, 1.5f);

        [SerializeField]
        [Tooltip("How fast the lava flows.")]
        private Vector2 flow = new Vector2(0.6f, 2f);

        [SerializeField]
        [Tooltip("The crater light's brightness.")]
        private Vector2 craterBrightness = new Vector2(0.4f, 4f);

        [SerializeField]
        [Tooltip("How tall the plume stands.")]
        private Vector2 plumeHeight = new Vector2(0.6f, 1.25f);

        [SerializeField]
        [Tooltip("How thick the plume is.")]
        private Vector2 plumeThickness = new Vector2(0.35f, 2f);

        [SerializeField]
        [Tooltip("The warm fill on the ships.")]
        private Vector2 shipFillBrightness = new Vector2(0.85f, 1.2f);

        [Header("Trying it out")]
        [SerializeField]
        [Range(-1f, 1f)]
        [Tooltip("In play, holds the volcano at this level so a look can be judged. Under 0 it follows the run, which is how the scene must be saved.")]
        private float holdAt = -1f;

        /// <summary>The fastest the level moves, a second, so winding the clock on is a swell and not a cut.</summary>
        private const float FastestChange = 0.6f;

        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int VeinContrastId = Shader.PropertyToID("_VeinContrast");
        private static readonly int FlowLeadId = Shader.PropertyToID("_FlowLead");

        private readonly List<float> surges = new List<float>();

        private WaveDirector director;
        private float bossAt = -1f;

        /// <summary>When the Leviathan was first seen, in game time; negative until it is.</summary>
        private float bossSeenAt = -1f;

        private Material lavaMaterial;
        private float authoredGlow;
        private float authoredVeins;

        /// <summary>
        /// Seconds the lava's flow is ahead of the clock. The shader scrolls by
        /// time, so changing the speed itself would jump the whole pattern by
        /// the time already gone; this adds or holds back time instead, and the
        /// pattern only ever slides.
        /// </summary>
        private float flowLead;

        private float authoredCrater;
        private float authoredShipFill;
        private Vector3 authoredSmokeSize;
        private float authoredSmokeFloor;
        private float authoredSmokeDistance;

        /// <summary>How far the eruption has got, 0 to 1.</summary>
        public float Level { get; private set; }

        private void Start()
        {
            director = FindAnyObjectByType<WaveDirector>();

            // With no wave to read there is no run to follow, and the scene is
            // left as it was authored.
            if (director == null || director.Wave == null || director.Wave.BossArrivesAt <= 0f)
            {
                enabled = false;
                return;
            }

            bossAt = director.Wave.BossArrivesAt;
            EruptionCurve.SurgeTimes(director.Wave.Streams, quietGap, surges);

            if (lava != null)
            {
                // An instance, so that play never writes to the material asset.
                lavaMaterial = lava.material;
                authoredGlow = lavaMaterial.GetFloat(GlowId);
                authoredVeins = lavaMaterial.GetFloat(VeinContrastId);
            }

            if (craterLight != null)
            {
                authoredCrater = craterLight.intensity;
            }

            if (shipFill != null)
            {
                authoredShipFill = shipFill.intensity;
            }

            if (smoke != null)
            {
                authoredSmokeSize = smoke.parameters.size;
                authoredSmokeFloor = smoke.transform.position.y - authoredSmokeSize.y * 0.5f;
                authoredSmokeDistance = smoke.parameters.meanFreePath;
            }

            Level = Target();
            Apply(0f);
        }

        private void Update()
        {
            Level = Mathf.MoveTowards(Level, Target(), FastestChange * Time.deltaTime);
            Apply(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (lavaMaterial != null)
            {
                Destroy(lavaMaterial);
            }
        }

        private float Target()
        {
            if (holdAt >= 0f)
            {
                return holdAt;
            }

            float elapsed = director.Elapsed;

            // Seen rather than timed: Skip to boss puts it out with the clock
            // wherever it was.
            if (bossSeenAt < 0f && (BossEmitter.Active != null || elapsed >= bossAt))
            {
                bossSeenAt = Time.time;
            }

            return EruptionCurve.Level(
                EruptionCurve.Resting(elapsed, bossAt, restingPeak, lateness),
                EruptionCurve.Surge(elapsed, surges, surgeRise, surgeFall),
                surgeHeight,
                bossSeenAt < 0f ? -1f : Time.time - bossSeenAt,
                arrivalSeconds);
        }

        /// <summary>
        /// Where a range stands at the present level, stepping by ratio and
        /// not by difference: halfway is the geometric middle. Brightness is
        /// seen in ratios, and stepped evenly the lava was nearly at its full
        /// look by half way, with the second half of the run adding little.
        /// </summary>
        private float At(Vector2 range)
        {
            if (range.x <= 0f || range.y <= 0f)
            {
                return Mathf.Lerp(range.x, range.y, Level);
            }

            return range.x * Mathf.Pow(range.y / range.x, Level);
        }

        private void Apply(float deltaTime)
        {
            if (lavaMaterial != null)
            {
                flowLead += (At(flow) - 1f) * deltaTime;
                lavaMaterial.SetFloat(GlowId, authoredGlow * At(glow));
                lavaMaterial.SetFloat(VeinContrastId, authoredVeins * At(molten));
                lavaMaterial.SetFloat(FlowLeadId, flowLead);
            }

            if (craterLight != null)
            {
                craterLight.intensity = authoredCrater * At(craterBrightness);
            }

            if (shipFill != null)
            {
                shipFill.intensity = authoredShipFill * At(shipFillBrightness);
            }

            if (smoke != null)
            {
                // The plume is drawn in the box's own proportions, so a taller
                // box is a taller plume. Its floor stays in the crater.
                float tall = authoredSmokeSize.y * At(plumeHeight);
                smoke.parameters.size = new Vector3(authoredSmokeSize.x, tall, authoredSmokeSize.z);
                smoke.parameters.meanFreePath = authoredSmokeDistance / Mathf.Max(At(plumeThickness), 0.01f);

                Vector3 at = smoke.transform.position;
                at.y = authoredSmokeFloor + tall * 0.5f;
                smoke.transform.position = at;
            }
        }
    }
}
