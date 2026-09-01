using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A plate torn off the boss's hull, left hanging in the lane the player
    /// flies in.
    ///
    /// It exists because the second act had lost the thing the first act was
    /// about. Armoured is a fight about height - the emplacements sit at the
    /// floor, the middle and the ceiling of the band, player bullets fly at the
    /// player's own height and never change it, so killing a bank means going to
    /// that bank's height. Exposed threw all of that away: the hull is 156 units
    /// tall against a band of 89, so it spans every height the player can reach
    /// and any height is as good as any other. What was left was holding the
    /// trigger and dashing once every seven seconds.
    ///
    /// Wreckage puts the three heights back with their meaning reversed. It is
    /// shed from the emplacements the player themselves destroyed, at those
    /// emplacements' own heights, so the places that used to be worth flying to
    /// become the places worth avoiding - and the player already knows where they
    /// are.
    ///
    /// It does not move. Everything else in this game rides the ring; a boss
    /// bullet laps it in 4.5 seconds and the player in 12.3. This is the one
    /// thing that does not, which is what makes it read as debris rather than as
    /// another attacker, and it means the boss - cruising at 15 degrees a second
    /// while it sheds - leaves a wake of its own hull behind it and eventually
    /// comes back round into it. The player is navigating a map that grows rather
    /// than tracking one more moving part.
    ///
    /// Tagged Boss, so <see cref="Player"/> already handles it: contact costs a
    /// hit and does not consume the object, which is right for a wall of scrap
    /// and needs no new branch there. What that alone does not give is a contact
    /// that costs only one hit, which is why this holds the guard below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossWreckage : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shots to break one plate. Cheap on purpose: clearing a lane has to stay an " +
                 "option, and the cost that matters is the seconds those shots did not spend " +
                 "on the hull rather than the count itself.")]
        private int healthPoints = 3;

        [SerializeField]
        [Tooltip("Seconds a plate lasts. With the shed interval this sets how crowded the ring " +
                 "gets: life over interval is how many are up at once, and a third of those sit " +
                 "at any one height. 16 against a 1.8 second cadence holds about nine plates, " +
                 "which works out at one forced move every four seconds or so.")]
        private float lifeSeconds = 16f;

        [SerializeField]
        [Tooltip("Seconds a plate spends shrinking out at the end of its life. A plate this " +
                 "size vanishing between two frames reads as a bug; shrinking also takes the " +
                 "collider with it, so it stops being a hazard exactly as fast as it stops " +
                 "looking like one.")]
        private float fadeSeconds = 0.75f;

        [SerializeField]
        [Tooltip("Played where a shot lands without breaking the plate.")]
        private GameObject hitEffect;

        [SerializeField]
        [Tooltip("Played once, here, when the plate breaks.")]
        private GameObject explosion;

        [SerializeField]
        [Tooltip("Degrees per second of tumble. The one cue that separates a piece of scrap " +
                 "from a thing the boss placed there deliberately.")]
        private float tumbleDegreesPerSecond = 40f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How far the tumble axis is allowed to lean off vertical. Small on purpose - " +
                 "see the axis it is applied to.")]
        private float tumbleWobble = 0.25f;

        [SerializeField]
        [Tooltip("How far clear of this plate the player has to get before it can charge them " +
                 "again. Wide enough that drifting along the surface does not read as leaving " +
                 "and coming back.")]
        private float contactClearance = 4f;

        private HealthState health;
        private HitFlash flash;
        private SphereCollider ball;

        /// <summary>
        /// The player, while they are still inside this plate. Null the rest of
        /// the time, which is also what says the plate is solid again.
        /// </summary>
        private Transform touching;

        private Transform body;
        private Vector3 restingScale = Vector3.one;
        private Vector3 tumbleAxis = Vector3.up;
        private float expiresAt;

        /// <summary>
        /// Read before anything can shrink it, so the size restored on reuse is
        /// the authored one rather than whatever the last life faded to.
        /// </summary>
        private void Awake()
        {
            body = transform;
            restingScale = body.localScale;
            flash = HitFlash.On(gameObject);
            ball = GetComponent<SphereCollider>();
        }

        /// <summary>
        /// The state that belongs to a life rather than to the object. These are
        /// pooled and there are a dozen of them alive at once, so all of it has to
        /// be rebuilt here: a reused plate would otherwise arrive broken, shrunk
        /// to nothing, and already expired.
        /// </summary>
        private void OnEnable()
        {
            health = new HealthState(healthPoints);
            body.localScale = restingScale;
            ReleaseContact();
            expiresAt = Time.time + Mathf.Max(lifeSeconds, fadeSeconds);

            // Near-vertical rather than anywhere on the sphere, so that what the
            // plate looks like goes on agreeing with what it is. The hitbox is a
            // ball at the middle and cannot change shape; a plate cartwheeling end
            // over end would sweep its own length in height while that ball stayed
            // put, so the player would watch it pass through them and take
            // nothing. Turning about the vertical keeps the slab lying roughly in
            // the band its ball occupies, and the profile it presents along the
            // ring goes on changing, which is the part that reads as tumbling.
            // Randomised per spawn so a run of plates off the same emplacement
            // does not come out turning in step.
            tumbleAxis = (Vector3.up + Random.insideUnitSphere * tumbleWobble).normalized;
        }

        private void Update()
        {
            body.Rotate(tumbleAxis, tumbleDegreesPerSecond * Time.deltaTime, Space.World);

            ReleaseWhenClear();

            float remaining = expiresAt - Time.time;

            if (remaining <= 0f)
            {
                // No explosion: this is a plate falling away out of reach, not one
                // the player broke. An explosion here would tell them they had
                // scored something they did not do.
                ObjectPool.Despawn(gameObject);
                return;
            }

            if (remaining < fadeSeconds && fadeSeconds > 0f)
            {
                body.localScale = restingScale * (remaining / fadeSeconds);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Shoot"))
            {
                HoldOffAfterTouching(other);
                return;
            }

            // Read before despawning, so the spark lands where the bullet struck
            // rather than at the middle of the plate.
            Vector3 impact = other.transform.position;

            ObjectPool.Despawn(other.gameObject);

            if (health.TakeDamage(1))
            {
                if (explosion != null)
                {
                    ObjectPool.Spawn(explosion, body.position, body.rotation);
                }

                // No experience, no kill recorded, and nothing taken off the
                // boss's health. Breaking a plate opens a lane, and that is the
                // whole of its reward - paying for it as well would make shooting
                // away from the boss the scoring play in the act where shooting
                // the boss is finally worth something.
                ObjectPool.Despawn(gameObject);
                return;
            }

            if (flash != null)
            {
                flash.Strike();
            }

            if (hitEffect != null)
            {
                ObjectPool.Spawn(hitEffect, impact, body.rotation);
            }
        }

        /// <summary>
        /// Goes intangible the moment the player is inside, so one collision costs
        /// one hit.
        ///
        /// The problem is not the plate's shape, which was the first guess and the
        /// wrong one - a sphere hitbox was tried on the theory that the tumble was
        /// sweeping the player in and out, and it changed nothing. It is that the
        /// player turns: PlayerMovement points the ship at the arena's axis every
        /// frame and SpaceShipPitch rolls it, so a ship sitting still inside a
        /// stationary trigger keeps carrying its own collider back out through the
        /// surface and in again, and every one of those is a fresh
        /// OnTriggerEnter.
        ///
        /// Measured with the spawner switched off and the arena verifiably empty -
        /// no enemies, no enemy fire, one plate: a player stopped inside it went
        /// from 19 hit points to 0 in 0.64 seconds, which is close to one hit per
        /// physics step. The same test with this guard in place costs exactly one
        /// hit and nothing further over six seconds. Flying through is the case
        /// that actually matters and it sat between the two, silently, because
        /// this branch of Player spawns no hit effect to count.
        ///
        /// Switching the collider off is what makes it one. The plate cannot be
        /// shot while the player is inside it, which is a fair trade for a moment
        /// in which they are not shooting it.
        /// </summary>
        private void HoldOffAfterTouching(Collider other)
        {
            if (ball == null || touching != null)
            {
                return;
            }

            Player player = other.GetComponentInParent<Player>();

            if (player == null)
            {
                return;
            }

            touching = player.transform;
            ball.enabled = false;
        }

        /// <summary>
        /// Solid again once the player is properly clear - or has stopped existing,
        /// which is what a plate that killed them looks like from here.
        /// </summary>
        private void ReleaseWhenClear()
        {
            if (ball == null)
            {
                return;
            }

            if (touching == null)
            {
                // Either nobody is inside, or the player was destroyed while they
                // were. The second case still has to hand the collider back, or a
                // plate would spend the rest of its life as scenery.
                if (!ball.enabled)
                {
                    ReleaseContact();
                }

                return;
            }

            float reach = ball.radius * body.localScale.x + contactClearance;

            if (Vector3.Distance(body.position, touching.position) > reach)
            {
                ReleaseContact();
            }
        }

        private void ReleaseContact()
        {
            touching = null;

            if (ball != null)
            {
                ball.enabled = true;
            }
        }
    }
}
