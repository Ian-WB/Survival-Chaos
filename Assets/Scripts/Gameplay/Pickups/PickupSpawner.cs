using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Puts pickups on the ring and resolves them when they are taken.
    ///
    /// Two sources feed it, and they are deliberately different:
    ///
    /// Upgrades arrive on level-up, as a set the player chooses one from. They
    /// are the reward for killing things, and making them a choice is what stops
    /// a run being the same build every time.
    ///
    /// Health arrives on a timer instead. Tying it to level-ups made healing a
    /// consequence of killing well, which is backwards - the player who most
    /// needs it is the one who is struggling to kill anything. On a clock it
    /// arrives when it arrives, and the player has to decide whether breaking
    /// position to reach it is worth more than holding a firing line.
    /// </summary>
    public class PickupSpawner : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField]
        [Tooltip("The arena axis everything orbits. Falls back to the object tagged Scenario.")]
        private Transform arenaCenter;

        [SerializeField]
        [Tooltip("Used for the bearing pickups are placed relative to. Falls back to the " +
                 "object tagged Player.")]
        private Transform player;

        [SerializeField]
        [Tooltip("Receives the upgrade when a pickup is collected. Falls back to the " +
                 "Player component on the transform above.")]
        private Player playerTarget;

        [Header("Prefab")]
        [SerializeField]
        [Tooltip("Spawned for every pickup. One prefab serves all of them - the payload " +
                 "and colour are applied per instance.")]
        private GameObject pickupPrefab;

        [SerializeField]
        [Tooltip("How many to build up front. The default covers one full offer plus a " +
                 "health drop overlapping it.")]
        private int warmup = 5;

        [Header("Upgrade offers")]
        [SerializeField]
        [Range(1, 6)]
        [Tooltip("Pickups per level-up. At 1 this is the old behaviour with a flight " +
                 "attached; from 2 up it becomes a choice, because they cannot all be reached.")]
        private int offerSize = 3;

        [SerializeField]
        [Tooltip("Degrees around the ring between the player and the nearest pickup of an " +
                 "offer. Clamped to half the gap between neighbours - see PickupPlacement.")]
        private float offerSeparation = 55f;

        [SerializeField]
        [Tooltip("Seconds an upgrade pickup stays out. Running out forfeits it, which is " +
                 "what gives the offer stakes.")]
        private float offerLifetime = 14f;

        [Header("Health drops")]
        [SerializeField]
        [Tooltip("Seconds between health drops. Zero or less turns them off.")]
        private float healthInterval = 30f;

        [SerializeField]
        [Tooltip("Seconds before the first one.")]
        private float healthStartDelay = 20f;

        [SerializeField]
        private int healthAmount = 2;

        [SerializeField]
        [Tooltip("Seconds a health drop stays out. Longer than an upgrade offer - it is not " +
                 "a choice between alternatives, just something to go and get.")]
        private float healthLifetime = 20f;

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        private Color healthColor = new Color(0.2f, 2f, 0.6f);

        [Header("Feedback")]
        [SerializeField]
        [Tooltip("Shows the banner when a pickup is collected. Optional.")]
        private SkillSelect skillSelect;

        [SerializeField]
        [Tooltip("Spawned where a pickup was taken. Optional - any one-shot effect prefab.")]
        private GameObject collectEffect;

        /// <summary>
        /// How many skills a level-up should draw. Read by SkillSelect, which
        /// owns the pool - the count belongs here because it is a property of
        /// how the offer is laid out on the ring, not of the pool.
        /// </summary>
        public int OfferSize => offerSize;

        /// <summary>
        /// Offers still live. Kept so a pickup can find its siblings, and so the
        /// list can be cleaned up as they resolve.
        /// </summary>
        private readonly List<SkillOffer> offers = new List<SkillOffer>();

        private float nextHealthAt;

        private void Start()
        {
            ResolveReferences();

            if (pickupPrefab != null)
            {
                ObjectPool.Warm(pickupPrefab, warmup);
            }

            nextHealthAt = Time.time + Mathf.Max(0f, healthStartDelay);
        }

        private void Update()
        {
            if (healthInterval <= 0f || Time.time < nextHealthAt)
            {
                return;
            }

            nextHealthAt = Time.time + healthInterval;
            SpawnHealth();
        }

        /// <summary>
        /// Puts an offer on the ring. Called on level-up in place of granting a
        /// skill outright.
        /// </summary>
        /// <param name="skills">
        /// What to offer, already drawn from the pool but not yet charged
        /// against it - see SkillPool.Draw.
        /// </param>
        public void OfferSkills(IReadOnlyList<SkillDefinition> skills)
        {
            if (skills == null || skills.Count == 0 || pickupPrefab == null)
            {
                return;
            }

            float bearing = CurrentPlayerBearing();
            float height = player != null ? player.position.y : transform.position.y;

            float[] bearings = PickupPlacement.Bearings(
                bearing,
                skills.Count,
                offerSeparation,
                clockwise: Random.value < 0.5f);

            var offer = new SkillOffer();

            for (int i = 0; i < skills.Count && i < bearings.Length; i++)
            {
                SkillDefinition skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                Pickup pickup = Place(bearings[i], height, skill, 0, skill.PickupColor, offerLifetime);
                offer.Add(pickup);
            }

            if (offer.LiveCount > 0)
            {
                offers.Add(offer);
            }
        }

        /// <summary>
        /// A single health drop, on the ring, with no offer attached. Placed
        /// clear of the player for the same reason an offer is - a drop that
        /// lands underfoot is a grant, not a decision.
        /// </summary>
        private void SpawnHealth()
        {
            if (pickupPrefab == null)
            {
                return;
            }

            float bearing = CurrentPlayerBearing();
            float height = player != null ? player.position.y : transform.position.y;

            float[] bearings = PickupPlacement.Bearings(
                bearing,
                count: 1,
                minSeparationDegrees: offerSeparation,
                clockwise: Random.value < 0.5f);

            Place(bearings[0], height, null, healthAmount, healthColor, healthLifetime);
        }

        private Pickup Place(
            float bearing,
            float height,
            SkillDefinition skill,
            int healAmount,
            Color color,
            float lifetime)
        {
            Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
            Vector3 position = PickupPlacement.PointAt(
                bearing, center, CurrentOrbitRadius(center), height);

            GameObject instance = ObjectPool.Spawn(pickupPrefab, position, Quaternion.identity);
            if (instance == null)
            {
                return null;
            }

            if (!instance.TryGetComponent(out Pickup pickup))
            {
                Debug.LogError($"{pickupPrefab.name} has no Pickup component.", pickupPrefab);
                ObjectPool.Despawn(instance);
                return null;
            }

            pickup.Configure(this, skill, healAmount, color, lifetime);
            return pickup;
        }

        /// <summary>
        /// Applies a collected pickup and clears whatever it was offered against.
        /// Called by the pickup itself on contact.
        /// </summary>
        public void OnCollected(Pickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            if (collectEffect != null)
            {
                ObjectPool.Spawn(collectEffect, pickup.transform.position, Quaternion.identity);
            }

            if (pickup.Skill != null)
            {
                // Explicit rather than ?. - Unity overloads == on Object to treat
                // a destroyed object as null, and the null-conditional operator
                // does not go through that overload. On a scene reference this is
                // the difference between a skipped call and a call into a corpse.
                if (skillSelect != null)
                {
                    // The pick is charged here rather than when the offer went
                    // out, so the skills the player passed over stay available.
                    skillSelect.ApplyCollected(pickup.Skill);
                }
                else
                {
                    // Applied anyway rather than dropped: losing an upgrade the
                    // player flew across the ring for is a worse failure than
                    // missing a banner. The pick goes uncharged, which at worst
                    // offers the same skill again.
                    Debug.LogWarning(
                        "PickupSpawner has no SkillSelect assigned, so the upgrade was applied " +
                        "without being recorded or announced.", this);

                    if (playerTarget != null)
                    {
                        pickup.Skill.Apply(playerTarget);
                    }
                }
            }
            else if (playerTarget != null)
            {
                playerTarget.Heal(pickup.HealAmount);
                PlayCollectSound();
            }

            ClearOffer(pickup);
            ObjectPool.Despawn(pickup.gameObject);
        }

        /// <summary>Called by a pickup whose clock ran out.</summary>
        public void OnExpired(Pickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            SkillOffer offer = pickup.Offer;
            if (offer != null && offer.Remove(pickup))
            {
                offers.Remove(offer);
            }

            ObjectPool.Despawn(pickup.gameObject);
        }

        /// <summary>Takes the rest of a collected pickup's offer off the ring.</summary>
        private void ClearOffer(Pickup taken)
        {
            SkillOffer offer = taken.Offer;
            if (offer == null)
            {
                return;
            }

            foreach (Pickup forfeited in offer.Claim(taken))
            {
                ObjectPool.Despawn(forfeited.gameObject);
            }

            offers.Remove(offer);
        }

        private void PlayCollectSound()
        {
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.SkillPicked);
            }
        }

        /// <summary>
        /// The radius to place pickups at: whatever the player is actually
        /// orbiting at, falling back to the arena constant.
        ///
        /// Measured rather than taken from ArenaGeometry because the two can
        /// legitimately differ. SnapToOrbit carries a radiusOffset - it is how
        /// the camera sits further out than the lane - and a player given one
        /// would orbit at a radius the constant does not describe. Pickups on
        /// the wrong circle are not slightly off, they are unreachable, and
        /// nothing about the bug would point at this line.
        /// </summary>
        private float CurrentOrbitRadius(Vector3 center)
        {
            if (player == null)
            {
                return ArenaGeometry.OrbitRadius;
            }

            Vector3 flat = player.position - center;
            flat.y = 0f;

            // Before SnapToOrbit has run, or if the player somehow sits on the
            // axis, the measurement is meaningless and the constant is better.
            return flat.sqrMagnitude < 0.01f ? ArenaGeometry.OrbitRadius : flat.magnitude;
        }

        private float CurrentPlayerBearing()
        {
            if (player == null)
            {
                return 0f;
            }

            Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
            return PickupPlacement.BearingOf(player.position, center);
        }

        /// <summary>
        /// Fills in anything left empty in the Inspector.
        ///
        /// The scene wires these up by hand, but the arena centre and the player
        /// are both findable by tag, and getting a silent null here means pickups
        /// spawn at the world origin - visible as a bug only if you happen to be
        /// looking at the middle of the arena.
        /// </summary>
        private void ResolveReferences()
        {
            if (arenaCenter == null)
            {
                GameObject scenario = GameObject.FindWithTag("Scenario");
                if (scenario != null)
                {
                    arenaCenter = scenario.transform;
                }
            }

            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                }
            }

            if (playerTarget == null && player != null)
            {
                player.TryGetComponent(out playerTarget);
            }

            if (arenaCenter == null)
            {
                Debug.LogWarning(
                    "PickupSpawner found no arena centre; pickups will orbit the world origin.",
                    this);
            }
        }
    }
}
