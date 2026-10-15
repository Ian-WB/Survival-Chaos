using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// The storm opens over the run: its wall starts a run standing over the
    /// moon and sinks until, as the Leviathan comes, it is back at the scene's
    /// own height with the moon clear above it.
    ///
    /// Ian's idea, 15 October 2026, built on three picks he said yes to: the
    /// wall goes down on the run's clock, the one the eruption keeps, and is
    /// down when the boss is due; the moonlight follows it, the island darker
    /// at the start and brightening as the moon shows; and it ends at the
    /// wall the scene already has, no lower, because more open sky behind the
    /// lane puts dark ships on dark stars.
    ///
    /// **How the wall stands taller.** The scene's layer of cloud tops out
    /// 750 over the arena, which no wall a kilometre and more away can raise
    /// over the moon. So this deepens the layer, through a volume of its own
    /// over the scene's, and the project's cloud shader reads the wall's
    /// height off the layer's depth (StormWallHeight in
    /// VolumetricCloudsUtilities.hlsl). The eye's floor keeps its height;
    /// only the wall stretches. The map's tiling moves with the depth, since
    /// HDRP stretches the map by the layer's middle, so the eye keeps its
    /// size. With the storm open both numbers are the scene's own, and the
    /// clouds are what they were before this existed.
    ///
    /// **Twice its height** hides the whole moon from the starting camera at
    /// every moment of the storm's turn, with the wall's lit crown still at
    /// the top of the frame. At 1.9 the moon showed through gaps; at 2.2 the
    /// crown was out of the frame and the sky one dark murk.
    ///
    /// **The moonlight follows on its own.** The taller wall's shadow takes
    /// the island's moonlit rock from about 32 to about 20 (of 255), from
    /// either side of the island, and the haze with it. All of that is the
    /// clouds' shadow: with the Shadows setting at Off the island is as
    /// bright shut as open, and nothing here makes up for it.
    ///
    /// **It sinks late.** Evenly, the moon's top would show a tenth of the
    /// way into the run and half of it half way. At
    /// <see cref="lateness"/> 1.5 its top shows about a fifth of the way in,
    /// half of it about two thirds of the way, and the last of the fall comes
    /// as the volcano climbs to the boss.
    ///
    /// The clock is the WaveDirector's, so the debug menu's Advance and Skip
    /// to boss move the storm with everything else; it sinks at no more than
    /// <see cref="fastestOpening"/> a second, so a skip is a slow fall and not
    /// a cut. A new run starts it shut again. On the Medium and Low tiers
    /// there are no clouds, and it changes nothing. It changes no rule of the
    /// fight.
    /// </summary>
    public sealed class StormRise : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The scene's own volume, whose clouds are the storm at its lowest.")]
        private Volume sceneVolume;

        [Header("The storm")]
        [SerializeField]
        [Range(1f, 4f)]
        [Tooltip("How many times its own height the wall stands as a run starts. 1 is the scene's wall.")]
        private float tallest = 2f;

        [SerializeField]
        [Range(0.3f, 3f)]
        [Tooltip("How late the wall sinks. 1 sinks evenly across the run. Higher holds it high through the first minutes and saves the fall for the end; lower drops it early.")]
        private float lateness = 1.5f;

        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("The most the storm opens in a second, as a share of the whole fall, so winding the clock on is a slow sink.")]
        private float fastestOpening = 0.08f;

        [Header("Trying it out")]
        [SerializeField]
        [Range(-1f, 1f)]
        [Tooltip("In play, holds the storm this far open so a look can be judged. Under 0 it follows the run, which is how the scene must be saved.")]
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

        /// <summary>How far the storm has opened, 0 shut to 1 open.</summary>
        public float Open { get; private set; }

        /// <summary>How many times its lowest the wall stands now.</summary>
        public float Rise => StormRiseCurve.Rise(Open, tallest);

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

            Volume volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Storm Rise";
            volume.sharedProfile = profile;
            clouds = profile.Add<VolumetricClouds>();
            clouds.altitudeRange.overrideState = true;
            clouds.cloudTiling.overrideState = true;

            Open = Target();
            Apply();
        }

        private void Update()
        {
            Open = Mathf.MoveTowards(Open, Target(), fastestOpening * Time.deltaTime);
            Apply();
        }

        private void OnDestroy()
        {
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

            return StormRiseCurve.Open(director.Elapsed, bossAt, lateness);
        }

        private void Apply()
        {
            float depth = StormRiseCurve.Depth(lowDepth, Rise);
            clouds.altitudeRange.value = depth;
            clouds.cloudTiling.value = lowTiling * StormRiseCurve.Tiling(1f, bottom, lowDepth, depth);
        }
    }
}
