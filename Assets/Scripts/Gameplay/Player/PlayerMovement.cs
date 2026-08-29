using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace SurvivalChaos
{
    public class PlayerMovement : MonoBehaviour
    {

        [SerializeField]
        [Tooltip("The arena axis this object orbits. The camera runs this same component, " +
                 "which is how it stays in step with the player rather than following it.")]
        private Transform center;

        [SerializeField]
        [FormerlySerializedAs("speed")]
        [Tooltip("How fast this travels along the ring, in world units per second - the same " +
                 "unit as the climb speed below, so the two numbers are directly comparable.")]
        private float orbitSpeed;

        [SerializeField]
        [FormerlySerializedAs("Up_Down_Speed")]
        [Tooltip("How fast this climbs and dives, in world units per second.")]
        private float climbSpeed;

        /// <summary>
        /// What every Move Speed pick so far multiplies this object's speed by, in
        /// both directions.
        ///
        /// Shared rather than per-instance, and that is the entire point. Two objects
        /// run this component - the ship and the Main Camera - at the same authored
        /// speed, and they stay in formation only because those numbers are equal.
        /// A bonus applied to one and not the other would leave the camera behind
        /// and the player flying out of frame, and nothing about that bug would
        /// point at an upgrade.
        ///
        /// Both axes scale, so the two authored speeds keep whatever relationship
        /// they were given: set equal, they stay equal for the whole run rather than
        /// drifting apart a tenth at a time as the picks come in. That works because
        /// the camera carries ApplyBounds too and is clamped to the same vertical
        /// envelope as the ship, so climbing together keeps them framed exactly as
        /// orbiting together does.
        /// </summary>
        private static float speedMultiplier = 1f;

        /// <summary>Adds one pick's worth of orbit speed, for every object running this.</summary>
        public static void AddSpeedBonus(float fraction)
        {
            speedMultiplier += fraction;
        }

        /// <summary>
        /// The dash, held statically for exactly the reason speedMultiplier above
        /// is: the ship and the Main Camera both run this component, and they stay
        /// in formation only because they are handed the same numbers. A dash
        /// applied to the ship alone would fling it out of frame five times faster
        /// than the camera could follow - which is the same bug as an unshared
        /// speed pick, arriving in a fifth of a second instead of over a run.
        ///
        /// The heading is captured once, when the dash begins, rather than read
        /// live for its duration. A dash is a commitment: letting go of the key
        /// halfway through should not strand the ship, and grabbing a new
        /// direction halfway through should not steer it. It also means the burst
        /// has a direction at all on the frames where the player is holding
        /// nothing.
        /// </summary>
        private static bool dashing;
        private static float dashHorizontal;
        private static float dashVertical;
        private static float dashMultiplier = 1f;

        /// <summary>
        /// Starts the burst, for every object running this. The heading is a unit
        /// vector in the same axes the player steers with.
        /// </summary>
        public static void BeginDash(float horizontal, float vertical, float multiplier)
        {
            dashing = true;
            dashHorizontal = horizontal;
            dashVertical = vertical;

            // Never below 1: a dash that slowed the ship down would be a dash
            // that got the player killed, and the field is authored per scene.
            dashMultiplier = Mathf.Max(1f, multiplier);
        }

        /// <summary>Ends the burst and hands steering back to the player.</summary>
        public static void EndDash()
        {
            dashing = false;
            dashHorizontal = 0f;
            dashVertical = 0f;
            dashMultiplier = 1f;
        }

        /// <summary>True while a dash burst is in progress.</summary>
        public static bool Dashing => dashing;

        /// <summary>
        /// The steering actually being applied this frame - the player's input
        /// normally, the captured heading during a dash.
        ///
        /// Exposed because the ship model has to lean into the burst to sell it,
        /// and SpaceShipPitch reading raw input would leave the ship flying
        /// level through a dash it never appeared to make.
        /// </summary>
        public static float EffectiveHorizontal => dashing ? dashHorizontal : GameInput.Horizontal;

        /// <summary>As <see cref="EffectiveHorizontal"/>, for the climb axis.</summary>
        public static float EffectiveVertical => dashing ? dashVertical : GameInput.Vertical;

        /// <summary>
        /// Statics outlive a scene, and outlive play mode entirely when domain reload
        /// is disabled, so a second run would otherwise start at the speed the first
        /// one ended at.
        ///
        /// That takes both halves and only one was here. SubsystemRegistration fires
        /// when play mode is entered, which covers the editor and the first run of a
        /// build; it does not fire when the player presses Retry, because that is
        /// SceneManager.LoadScene and play mode never ended. Five Move Speed picks
        /// left this at 1.5 and the next run began there rather than at 1 - measured
        /// in play mode, not inferred - and it compounds run on run.
        ///
        /// The scene hook is what resets it between runs within one session:
        /// unloading is the only way a run ends, by any route, so nothing has to
        /// remember to clear it. ObjectPool and RunOutcome hook this same pair of
        /// events for the same reason.
        ///
        /// On unload rather than on load, because nothing reads the multiplier while
        /// a scene is being torn down. That puts the value back to 1 before the next
        /// scene's first Awake rather than shortly after it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            speedMultiplier = 1f;
            EndDash();

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            speedMultiplier = 1f;

            // A run that ended mid-dash would otherwise hand the next one a ship
            // and a camera already travelling at five times their authored speed,
            // with no PlayerDash left alive to switch it off.
            EndDash();
        }

        void Update()
        {
            if (center == null)
            {
                return;
            }

            float h = EffectiveHorizontal;
            float v = EffectiveVertical;

            Vector3 pos = center.position;
            pos.y = transform.position.y;

            // Authored in units per second, converted here to the angle RotateAround
            // actually wants. The conversion divides by the lane's radius rather than
            // by this object's own distance from the axis, and that distinction is
            // load-bearing: the Main Camera orbits 75 units further out, so measuring
            // against its own radius would hand it a smaller angle than the ship and
            // leave it trailing a little further behind every second.
            //
            // Read the other way round, the number means what it says for the ship -
            // which is on the lane - and means "keeps station with the ship" for
            // everything else running this.
            float degreesPerSecond = orbitSpeed * Mathf.Rad2Deg / ArenaGeometry.OrbitRadius;

            // Both axes take the dash, so the two authored speeds keep their
            // relationship through a burst the same way they keep it through a
            // run of speed picks - a dash that only moved you around the ring
            // would be no use against an attack that owns a height.
            float scale = speedMultiplier * dashMultiplier;

            transform.RotateAround(
                pos, Vector3.up, -h * Time.deltaTime * degreesPerSecond * scale);
            transform.LookAt(pos);

            transform.position += Vector3.up * v * Time.deltaTime * climbSpeed * scale;
        }
    }
}
