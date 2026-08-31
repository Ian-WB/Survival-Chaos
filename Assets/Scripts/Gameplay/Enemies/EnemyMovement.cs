using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class EnemyMovement : MonoBehaviour
    {
        [SerializeField]
        private float speed= 5.0f;
        private Transform player;

        [SerializeField]
        private float rotationSpeed;

        private Vector3 center;
        private float centerZ;
        private float centerX;

        [SerializeField]
        [Tooltip("Which way round the ring this enemy sets off. Held only until the player is " +
                 "found, after which the direction is worked out every frame from the two bearings.")]
        private bool leftOrRight = true;

        [SerializeField]
        [Tooltip("How far past the decision point the player has to be before this enemy commits " +
                 "to turning round. Zero turns on the instant, which chatters when the enemy is " +
                 "sitting on the player; the default is about what the old trigger volumes imposed.")]
        private float turnDeadbandDegrees = RingChase.DefaultDeadbandDegrees;

        /// <summary>
        /// Which way round the ring this enemy is currently travelling.
        ///
        /// Read-only now. It used to be settable because turning enemies around
        /// was something done to them from outside - ColliderScript wrote it when
        /// the player crossed one of six trigger volumes carried by every enemy
        /// prefab. Those are gone and <see cref="RingChase"/> answers the same
        /// question from the two bearings, so a setter would only be a way to
        /// write a value that the next frame overwrites.
        ///
        /// The field keeps its old name so the value already authored on every
        /// prefab carries over as the starting direction; the property is named
        /// for what the bool actually means, which "leftOrRight" never said.
        /// </summary>
        public bool TravellingLeft => leftOrRight;

        [SerializeField]
        private float spawnSpeed;

        [SerializeField]
        [Tooltip("How close the player has to be before this enemy starts matching their height, " +
                 "as a multiple of the arena orbit radius. Both sit on the ring, so this is a " +
                 "straight-line distance across it: 1.1 is roughly a 66 degree arc either side of " +
                 "the player, 0.11 is about 6 degrees.")]
        private float chaseRadiusFraction = 1.1f;

        /// <summary>
        /// The distance inside which this enemy chases the player's height.
        ///
        /// A multiple of the orbit radius rather than a literal, so it survives
        /// another rescale of the arena. It was the bare number 15.
        ///
        /// That literal was flagged as stale on the grounds that 15 exceeds the
        /// orbit radius of 13.72 and so could never be false. That reasoning was
        /// wrong: both the enemy and the player sit on the ring, so the distance
        /// between them runs up to the *diameter*, 27.44, not the radius. 15 is a
        /// real threshold there - it covers about 66 degrees of arc either side of
        /// the player, a wide band but far from the whole ring.
        ///
        /// Setting it to a tenth, which is what the arena's tenfold shrink would
        /// imply if the original intent was an arc, collapsed the band to about 6
        /// degrees and the chase all but stopped happening. The default restores
        /// what the game actually played like; it is serialized because this is a
        /// feel value, not a constant, and different enemies may want different
        /// answers.
        /// </summary>
        private float ChaseRadius => ArenaGeometry.OrbitRadius * chaseRadiusFraction;

        /// <summary>
        /// The direction the prefab was authored to travel, captured before anything
        /// can turn it.
        ///
        /// leftOrRight is not read-only state: Update rewrites it every frame to
        /// whichever way closes on the player. Enemies are pooled, so without
        /// restoring this a reused one would set off in whatever direction it was
        /// last turned to rather than the one its prefab says - and that reads as
        /// enemies spawning already going the wrong way. It matters for the frame
        /// before the first Update, and for the whole life of an enemy that never
        /// finds a player to chase.
        /// </summary>
        private bool authoredDirection;

        private void Awake()
        {
            authoredDirection = leftOrRight;
        }

        /// <summary>
        /// A multiplier on how fast this enemy travels round the ring, 1 being
        /// the authored speed.
        ///
        /// Exists for the boss's ram, which has to outrun the player. Nothing
        /// else uses it, and it is a multiplier rather than an override so the
        /// authored speed stays the thing being read - "three times as fast"
        /// survives a retune of the boss's cruise, and "45 degrees a second"
        /// would quietly stop meaning three times anything.
        /// </summary>
        public float OrbitSpeedScale { get; set; } = 1f;

        /// <summary>
        /// Runs on every spawn, including reuse from the pool. Awake does not.
        /// </summary>
        private void OnEnable()
        {
            leftOrRight = authoredDirection;

            // Belongs to an attack rather than to the enemy, and an attack
            // interrupted by death leaves it set. A pooled boss brought back
            // mid-ram would otherwise arrive at triple speed and stay there for
            // the rest of the fight.
            OrbitSpeedScale = 1f;
        }

        void Start()
        {
            GameObject playerObj = GameObject.Find("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }

            GameObject scenario = GameObject.FindWithTag("Scenario");
            if (scenario != null)
            {
                centerX = scenario.transform.position.x;
                centerZ = scenario.transform.position.z;
                center = new Vector3(centerX, 0f, centerZ);
            }
        }


    void Update()
        {
            if (player == null)
            {
                return;
            }

            // Before anything moves, because Enemy_1 and EnemySpaceShip read
            // TravellingLeft to pick which side their shot leaves from.
            leftOrRight = RingChase.ShouldTravelLeft(
                PickupPlacement.BearingOf(transform.position, center),
                PickupPlacement.BearingOf(player.position, center),
                leftOrRight,
                turnDeadbandDegrees);

            Vector3 pos = center;
            pos.y = transform.position.y;

            // Distance from the axis, ignoring height - the enemy flies in to the
            // orbit radius first, then rides it.
            Vector3 flat = transform.position;
            flat.y = 0f;

            transform.LookAt(pos);

            // Both approaches below go through ShipMotion.Approach, which eases the
            // same amount per second whatever the frame rate and cannot overshoot.
            // They were written as `position += (target - position) * deltaTime *
            // speed`, which is the same curve only while deltaTime is small: once
            // deltaTime * speed passes 2 the step is larger than the gap and the
            // enemy diverges instead of arriving. That is reachable on a slow
            // machine, and presents as enemies flying off rather than as a frame
            // rate complaint.
            if(Vector3.Distance(center, flat) >= ArenaGeometry.OrbitRadius)
            {
                Vector3 next = transform.position;
                next.x = ShipMotion.Approach(next.x, center.x, spawnSpeed, Time.deltaTime);
                next.z = ShipMotion.Approach(next.z, center.z, spawnSpeed, Time.deltaTime);
                transform.position = next;
            }
            else
            {
                float direction = leftOrRight ? 1f : -1f;
                transform.RotateAround(
                    pos, Vector3.up, direction * rotationSpeed * OrbitSpeedScale * Time.deltaTime);

                if(Vector3.Distance(transform.position, player.position) <= ChaseRadius)
                {
                    // Height only: the ring position is handled by the orbit above.
                    Vector3 next = transform.position;
                    next.y = ShipMotion.Approach(next.y, player.position.y, speed, Time.deltaTime);
                    transform.position = next;
                }
            }
        }
    }
}
