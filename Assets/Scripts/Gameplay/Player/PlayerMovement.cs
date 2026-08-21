using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        /// Statics outlive a scene, and outlive play mode entirely when domain reload
        /// is disabled, so a second run would otherwise start at the speed the first
        /// one ended at.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            speedMultiplier = 1f;
        }

        void Update()
        {
            if (center == null)
            {
                return;
            }

            float h = GameInput.Horizontal;
            float v = GameInput.Vertical;

            Vector3 pos = center.position;
            pos.y = transform.position.y;

            // Authored in units per second, converted here to the angle RotateAround
            // actually wants. The conversion divides by the lane's radius rather than
            // by this object's own distance from the axis, and that distinction is
            // load-bearing: the Main Camera orbits 7.5 units further out, so measuring
            // against its own radius would hand it a smaller angle than the ship and
            // leave it trailing a little further behind every second.
            //
            // Read the other way round, the number means what it says for the ship -
            // which is on the lane - and means "keeps station with the ship" for
            // everything else running this.
            float degreesPerSecond = orbitSpeed * Mathf.Rad2Deg / ArenaGeometry.OrbitRadius;

            transform.RotateAround(
                pos, Vector3.up, -h * Time.deltaTime * degreesPerSecond * speedMultiplier);
            transform.LookAt(pos);

            transform.position += Vector3.up * v * Time.deltaTime * climbSpeed * speedMultiplier;
        }
    }
}
