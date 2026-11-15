using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// The storm grows at the end of the run, from nothing: no cloud at all
    /// for the first three quarters of the way to the Leviathan, no eye, no
    /// wall and no cloud floor, only the island, the moon and the stars.
    /// Then the clouds condense and the wall rises until, as the boss is
    /// due, it stands full over the moon, and it stays full through the
    /// fight.
    ///
    /// Ian asked for this on 15 November 2026: "grow to full instead of
    /// decreasing", starting at 75% of the run and full when the boss
    /// arrives, and then that the eye and the clouds only show up when the
    /// storm starts, "so basically it should start from nothing". Before
    /// that it ran the other way round, his idea of 15 October: the wall
    /// started a run over the moon and sank to almost nothing by the boss
    /// (start a bit higher and end way lower, at his word, 16 October). The
    /// heights at either end are the ones he tuned then.
    ///
    /// **From nothing.** The clouds' density is the scene's times
    /// <see cref="Formed"/>, which comes up from 0 over the first
    /// <see cref="formsBy"/> of the growth while the wall begins to rise
    /// from its lowest. At 0 there is no cloud to see, though the clouds are
    /// still traced: switching them off with the volume's Enable was tried
    /// first, on 15 November, and they never came back when it was turned on
    /// again, and the moon and the stars went with them under a flat haze.
    /// The storm's lightning waits for the clouds to be whole
    /// (StormLightning).
    ///
    /// **How the wall stands taller or shorter.** The scene's layer of cloud
    /// tops out 750 over the arena, which no wall a kilometre and more away
    /// can raise over the moon. So the project's cloud shader is told how
    /// many times its painted height the wall stands (_StormWallRise, set on
    /// both cloud tracers) and reads each column of wall stretched or squeezed
    /// by it (StormWallHeight in VolumetricCloudsUtilities.hlsl); while the
    /// wall stands over its painted height, this also deepens the layer,
    /// through a volume of its own over the scene's. The eye's floor keeps its
    /// height either way, so the layer is never made shallower than the
    /// scene's: only the wall moves. The map's tiling moves with the depth,
    /// since HDRP stretches the map by the layer's middle, so the eye keeps
    /// its size.
    ///
    /// **Its height, calm to full.** Calm is 0.4 of the painted height, Ian's
    /// number (16 October), which sinks the wall's tops under the horizon:
    /// nothing is left but the flat cloud floor, as at 0.5 and 0.6. At 0.7 a
    /// low band stood just over the horizon, and 0.8 is a thin bank. Full is
    /// 2.2: twice the painted height hides the whole moon from the starting
    /// camera at every moment of the storm's turn, with the wall's lit crown
    /// at the top of the frame, and Ian asked for a bit higher than 2.
    ///
    /// **The moonlight follows on its own.** The taller wall's shadow takes
    /// the island's moonlit rock from about 32 to about 20 (of 255), from
    /// either side of the island, and the haze with it, so the boss is fought
    /// on a darker island than the run before it. All of that is the clouds'
    /// shadow: with the Shadows setting at Off the island is as bright full
    /// as calm, and nothing here makes up for it.
    ///
    /// The clock is the WaveDirector's, so the debug menu's Advance and Skip
    /// to boss move the storm with everything else; it grows at no more than
    /// <see cref="fastestGrowth"/> a second, so a skip is a slow rise and not
    /// a cut. A new run starts it calm again. On the Low tier there are no
    /// clouds, and it changes nothing. It changes no rule of the fight.
    /// </summary>
    public sealed class StormRise : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The scene's own volume, whose clouds are the storm at its lowest.")]
        private Volume sceneVolume;

        [Header("The storm")]
        [SerializeField]
        [Range(1f, 4f)]
        [Tooltip("How many times its painted height the wall stands when the storm is full, as the boss arrives. 1 is the scene's wall.")]
        private float tallest = 2.2f;

        [SerializeField]
        [Range(0.3f, 1f)]
        [Tooltip("How many times its painted height the wall stands while the storm is calm, from the start of a run. 1 is the scene's wall; at 0.6 and under only the flat cloud floor is left.")]
        private float lowest = 0.4f;

        [SerializeField]
        [Tooltip("The project's copy of HDRP's cloud tracer: Art/Shaders/VolumetricCloudsTrace. The wall's height is set on it.")]
        private ComputeShader tracer;

        [SerializeField]
        [Tooltip("The project's copy of HDRP's cloud shadow tracer: Art/Shaders/VolumetricCloudsTraceShadows. The wall's shadow stands as tall as the wall.")]
        private ComputeShader shadowTracer;

        [SerializeField]
        [Range(0f, 0.95f)]
        [Tooltip("How far into the time before the boss the storm starts to grow, as a share of it. 0.75 is three quarters of the way; from there it rises evenly to full as the boss arrives.")]
        private float startsAt = 0.75f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("How much of the growth the clouds take to condense from nothing to the scene's density, as a share of it. 0.3 is the first 30 s of the 100 from 300 s to the boss.")]
        private float formsBy = 0.3f;

        [SerializeField]
        [Range(0.01f, 1f)]
        [FormerlySerializedAs("fastestOpening")]
        [Tooltip("The most the storm grows in a second, as a share of the whole rise, so winding the clock on is a slow rise.")]
        private float fastestGrowth = 0.08f;

        [Header("Trying it out")]
        [SerializeField]
        [Range(-1f, 1f)]
        [Tooltip("In play, holds the storm this far grown (0 calm, 1 full) so a look can be judged. Under 0 it follows the run, which is how the scene must be saved.")]
        private float holdAt = -1f;

        /// <summary>A volume over the scene's and under GraphicsDirector's, which sets the clouds' steps and shadows.</summary>
        private const float Priority = 5000f;

        private WaveDirector director;
        private float bossAt = -1f;

        private VolumeProfile profile;
        private VolumetricClouds clouds;

        private float bottom;
        private float lowDepth;
        private Vector2 lowTiling;
        private float density;

        /// <summary>How far the storm has grown, 0 calm to 1 full.</summary>
        public float Grown { get; private set; }

        private static readonly int WallRiseId = Shader.PropertyToID("_StormWallRise");

        /// <summary>How many times its painted height the wall stands now.</summary>
        public float Rise => StormRiseCurve.Rise(Grown, tallest, lowest);

        /// <summary>How much of the cloud is there, 0 none to 1 the scene's density.</summary>
        public float Formed => StormRiseCurve.Formed(Grown, formsBy);

        private void Start()
        {
            director = FindAnyObjectByType<WaveDirector>();

            // With no run to follow, or no clouds to raise, the scene is left as authored.
            if (director == null || director.Wave == null || director.Wave.BossArrivesAt <= 0f ||
                sceneVolume == null || sceneVolume.sharedProfile == null ||
                !sceneVolume.sharedProfile.TryGet(out VolumetricClouds sceneClouds))
            {
                enabled = false;
                return;
            }

            bossAt = director.Wave.BossArrivesAt;
            bottom = sceneClouds.bottomAltitude.value;
            lowDepth = sceneClouds.altitudeRange.value;
            lowTiling = sceneClouds.cloudTiling.value;
            density = sceneClouds.densityMultiplier.value;

            Volume volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Storm Rise";
            volume.sharedProfile = profile;
            clouds = profile.Add<VolumetricClouds>();
            clouds.altitudeRange.overrideState = true;
            clouds.cloudTiling.overrideState = true;
            clouds.densityMultiplier.overrideState = true;

            Grown = Target();
            Apply();
        }

        private void Update()
        {
            Grown = Mathf.MoveTowards(Grown, Target(), fastestGrowth * Time.deltaTime);
            Apply();
        }

        private void OnDestroy()
        {
            // The tracers are assets: what is set on them outlives play in the editor. 0 is the painted wall.
            SetWallRise(0f);

            if (profile != null)
            {
                Destroy(profile);
            }
        }

        private float Target()
        {
            if (holdAt >= 0f)
            {
                return holdAt;
            }

            // Seen rather than timed: Skip to boss puts it out with the clock wherever it was.
            if (BossEmitter.Active != null)
            {
                return 1f;
            }

            return StormRiseCurve.Grown(director.Elapsed, bossAt, startsAt);
        }

        private void Apply()
        {
            float depth = StormRiseCurve.Depth(lowDepth, Rise);
            clouds.altitudeRange.value = depth;
            clouds.cloudTiling.value = lowTiling * StormRiseCurve.Tiling(1f, bottom, lowDepth, depth);

            clouds.densityMultiplier.value = density * Formed;
            SetWallRise(Rise);
        }

        private void SetWallRise(float rise)
        {
            if (tracer != null)
            {
                tracer.SetFloat(WallRiseId, rise);
            }

            if (shadowTracer != null)
            {
                shadowTracer.SetFloat(WallRiseId, rise);
            }
        }
    }
}
