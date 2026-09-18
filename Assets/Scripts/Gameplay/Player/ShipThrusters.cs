using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// Flares on the ship that show what it is doing: a main plume out of the
    /// rocket when it accelerates, two retros under the nose when it backs up, a
    /// burst of both through a dash, and a light on the hull that goes out when
    /// the dash is spent and relights when it comes back.
    ///
    /// The ship had no such cue at all. It leans, through
    /// <see cref="SpaceShipPitch"/>, and that is the whole of what told the
    /// player the throttle was doing anything - a cue the ship shares with the
    /// idle drift it also does while standing still. The dash had a HUD bar in
    /// the corner, which is a real answer to a real question and in the wrong
    /// place for it: a 0.22 second commitment is decided while looking at the
    /// boss, and a glance at the corner costs more than the window is long.
    ///
    /// <para>
    /// <b>Rigid geometry, not particles.</b> Every flare is a mesh that moves
    /// with the ship and writes its own motion vectors. That is the lesson the
    /// player's rounds paid for: the game's settings run FSR, and a trail whose
    /// pixels do not carry their own motion gets dragged by whatever is behind
    /// them - see the streak note in <c>PlayerRoundBuilder</c>. A particle plume
    /// would be the same bug in a different costume.
    /// </para>
    ///
    /// <para>
    /// <b>Built at runtime, from serialized offsets.</b> The flares are not
    /// children of the ship prefab, because the ship itself is not a child of
    /// anything until the run starts: <see cref="Player"/> instantiates the model
    /// at Start, so there is no edit-time hierarchy to place them against and
    /// nothing to look at if there were. The offsets below were measured off the
    /// model's own meshes by <c>ShipThrusterBuilder</c> - the rocket's back face,
    /// the two under-nose blocks, the top of the hull - and they are the thing to
    /// edit if a flare sits wrong.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipThrusters : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField]
        [Tooltip("The tapered plume, a unit long and a unit wide along +X from its nozzle. " +
                 "Scaled to the lengths and widths below, so those are the numbers to tune.")]
        private Mesh plumeMesh;

        [SerializeField]
        [Tooltip("The ready light: crossed fans a unit across, so it reads as a glow from any angle.")]
        private Mesh glowMesh;

        [SerializeField]
        [Tooltip("Additive, unlit, in the player's green. Shared by every flare; each one's " +
                 "brightness is set per renderer, so one material covers all four.")]
        private Material flareMaterial;

        [Header("Main engine")]
        [SerializeField]
        [Tooltip("Where the main plume starts, in the model's own space. The back face of the " +
                 "rocket body.")]
        private Vector3 mainOffset = new Vector3(-0.2083f, 0.0386f, 0f);

        [SerializeField]
        [Tooltip("World length of the main plume at full throttle. A dash overshoots it.")]
        private float mainLength = 0.26f;

        [SerializeField]
        [Tooltip("World width of the main plume at the nozzle. About the rocket's own girth.")]
        private float mainWidth = 0.075f;

        [SerializeField]
        [Tooltip("Emission multiplier at full throttle.")]
        private float mainBrightness = 6f;

        [Header("Retros")]
        [SerializeField]
        [Tooltip("Where the retro plumes start, in the model's own space. The front faces of the " +
                 "two blocks under the nose.")]
        private Vector3[] retroOffsets =
        {
            new Vector3(0.0951f, -0.0908f, -0.0306f),
            new Vector3(0.0951f, -0.0908f, 0.0306f),
        };

        [SerializeField]
        [Tooltip("World length of a retro plume at full reverse. Deliberately short: braking " +
                 "should read as braking rather than as flying backwards under power.")]
        private float retroLength = 0.1f;

        [SerializeField]
        private float retroWidth = 0.03f;

        [SerializeField]
        private float retroBrightness = 4f;

        [Header("Dash")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("What a nozzle burns at during a dash that is not asking it for anything, before " +
                 "the boost. Covers the straight climb or dive, where neither throttle axis is " +
                 "pushed and the engines would otherwise sit dark through the one manoeuvre that " +
                 "most needs to look like one. It fades out as a burst turns against a nozzle, so " +
                 "a dash along the ring burns one end of the ship rather than both.")]
        private float dashFloor = 0.7f;

        [SerializeField]
        [Range(1f, 4f)]
        [Tooltip("Multiplies the whole burst. Above 1 on purpose - the flare overshoots its " +
                 "authored length, which is what separates a dash from a hard press of the key.")]
        private float dashBoost = 2f;

        [Header("Dash ready light")]
        [SerializeField]
        [Tooltip("Where the ready light sits, in the model's own space. On top of the hull rather " +
                 "than on a side, because the flip turns the model around and would carry a " +
                 "side-mounted light to the far face.")]
        private Vector3 readyOffset = new Vector3(-0.0385f, 0.1475f, 0f);

        [SerializeField]
        private float readySize = 0.042f;

        [SerializeField]
        private float readyBrightness = 2.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How bright the light is at the very end of the cooldown, as a fraction of ready. " +
                 "Low on purpose: the reading that matters is not-yet against now, and the bar " +
                 "in the corner already carries the remainder.")]
        private float readyCarry = 0.25f;

        [SerializeField]
        [Tooltip("How long the flash on coming back lasts, in seconds.")]
        private float popSeconds = 0.25f;

        [SerializeField]
        [Tooltip("How far over full the flash goes. A step from dark to lit is legible in a still " +
                 "and easy to miss in motion; an overshoot that decays has a leading edge.")]
        private float popStrength = 1.5f;

        [Header("Response")]
        [SerializeField]
        [Tooltip("How quickly a flare lights. High - an engine catches, it does not fade in.")]
        private float riseResponse = 26f;

        [SerializeField]
        [Tooltip("How quickly a flare dies back. Lower than the rise, so letting go leaves an " +
                 "afterglow rather than an off switch.")]
        private float fallResponse = 9f;

        /// <summary>
        /// The property the flare shader takes its brightness from.
        ///
        /// Written through a property block rather than by assigning
        /// <c>Renderer.material</c>, which would quietly clone the material four
        /// times per ship and leave those copies to be destroyed by hand. The
        /// block costs these four renderers their place in the SRP batcher,
        /// which is a fair price for four quads that are switched off most of
        /// the time.
        /// </summary>
        private static readonly int FlameIntensityId = Shader.PropertyToID("_FlameIntensity");

        private Player player;
        private PlayerDash dash;

        private Renderer mainFlare;
        private Renderer[] retroFlares;
        private Renderer readyLight;

        private MaterialPropertyBlock block;

        private float main;
        private float retro;

        /// <summary>
        /// When the dash last came back, for the flash. Negative infinity rather
        /// than a flag, for the reason <see cref="DashCycle"/> gives: it puts the
        /// event infinitely far in the past, which is exactly "not flashing".
        /// </summary>
        private float readyAt = float.NegativeInfinity;
        private bool wasReady = true;

        private void Start()
        {
            player = GetComponentInParent<Player>();
            dash = GetComponentInParent<PlayerDash>();

            // Both live on the ship this model is instantiated under, so a miss
            // is a hierarchy that has changed rather than a race. Said out loud
            // because either failure is quiet: without the Player the flares come
            // out of the wrong end once the ship is flipped, and without the dash
            // the ready light simply stays on and always looks correct.
            if (player == null || dash == null)
            {
                Debug.LogWarning(
                    "ShipThrusters found no " + (player == null ? "Player" : "PlayerDash")
                    + " above it, so the ship's flares will not read the "
                    + (player == null ? "flip." : "dash's cooldown."), this);
            }

            if (plumeMesh == null || glowMesh == null || flareMaterial == null)
            {
                Debug.LogWarning(
                    "ShipThrusters has no mesh or material, so the ship would fly with no " +
                    "exhaust. Run Survival Chaos/Build Ship Thrusters.", this);
                enabled = false;
                return;
            }

            // Aft: the plume mesh runs along +X from its nozzle, and the model's
            // nose points along +X, so the main engine is the one turned around.
            mainFlare = BuildFlare("Thruster Main", plumeMesh, mainOffset, Quaternion.Euler(0f, 180f, 0f));

            retroFlares = new Renderer[retroOffsets.Length];
            for (int i = 0; i < retroOffsets.Length; i++)
            {
                retroFlares[i] = BuildFlare("Thruster Retro " + i, plumeMesh, retroOffsets[i], Quaternion.identity);
            }

            readyLight = BuildFlare("Dash Ready Light", glowMesh, readyOffset, Quaternion.identity);

            block = new MaterialPropertyBlock();

            // Nothing is burning yet and the dash starts ready, so the first frame
            // draws a dark ship with its light on rather than a flash.
            Apply(mainFlare, 0f, mainLength, mainWidth, mainBrightness);
            foreach (Renderer flare in retroFlares)
            {
                Apply(flare, 0f, retroLength, retroWidth, retroBrightness);
            }

            ApplyGlow(1f);
        }

        private void Update()
        {
            if (mainFlare == null || PauseMenu.GameIsPaused || RunOutcome.RunEnded || Time.timeScale <= 0f)
            {
                return;
            }

            float delta = Time.deltaTime;
            bool flipped = player != null && player.DirectionFlipped;
            bool dashing = PlayerMovement.Dashing;

            float noseward = ThrusterResponse.Noseward(PlayerMovement.EffectiveHorizontal, flipped);

            main = Ease(main, ThrusterResponse.NozzleLevel(noseward, dashing, dashFloor, dashBoost), delta);
            retro = Ease(retro, ThrusterResponse.NozzleLevel(-noseward, dashing, dashFloor, dashBoost), delta);

            Apply(mainFlare, main, mainLength, mainWidth, mainBrightness);
            foreach (Renderer flare in retroFlares)
            {
                Apply(flare, retro, retroLength, retroWidth, retroBrightness);
            }

            UpdateReadyLight();
        }

        /// <summary>
        /// Lights fast and dies back slowly. One response for both would make the
        /// engine either mushy on the way up or abrupt on the way down; the
        /// asymmetry is most of what reads as combustion rather than as a lamp on
        /// a dimmer.
        /// </summary>
        private float Ease(float current, float target, float delta)
        {
            return ShipMotion.Approach(current, target, target > current ? riseResponse : fallResponse, delta);
        }

        private void UpdateReadyLight()
        {
            float fraction = dash != null ? dash.ReadyFraction : 1f;
            bool ready = fraction >= 1f;

            if (ready && !wasReady)
            {
                readyAt = Time.time;
            }

            wasReady = ready;

            float level = ThrusterResponse.ReadyLevel(fraction, readyCarry)
                          * ThrusterResponse.ReadyPop(Time.time - readyAt, popSeconds, popStrength);

            ApplyGlow(level);
        }

        private void ApplyGlow(float level)
        {
            Apply(readyLight, level, readySize, readySize, readyBrightness);
        }

        /// <summary>
        /// Sizes one flare and sets its brightness.
        ///
        /// The length rides the transform and the brightness rides the material,
        /// because they are two different readings: how hard the engine is
        /// pushing is a shape you can see from any distance, and how hot it is
        /// survives being two dozen pixels tall. A flare at nothing is switched
        /// off rather than drawn at zero size, so a ship coasting costs no draws.
        /// </summary>
        private void Apply(Renderer flare, float level, float length, float width, float brightness)
        {
            bool lit = level > 0.002f;
            flare.enabled = lit;

            if (!lit)
            {
                return;
            }

            flare.transform.localScale = new Vector3(length * level, width, width);

            flare.GetPropertyBlock(block);
            block.SetFloat(FlameIntensityId, brightness * level);
            flare.SetPropertyBlock(block);
        }

        private Renderer BuildFlare(string flareName, Mesh mesh, Vector3 offset, Quaternion rotation)
        {
            var go = new GameObject(flareName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = rotation;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = flareMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Object: the flare moves rigidly with the ship, so the ship's own
            // previous matrix gives every vertex its true motion on screen. This
            // is the line that keeps FSR from smearing it.
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;

            return renderer;
        }
    }
}
