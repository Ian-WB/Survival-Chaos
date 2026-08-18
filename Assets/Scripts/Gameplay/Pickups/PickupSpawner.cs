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
    /// Health arrives on its own cadence, every few levels, and is never part of
    /// an offer. It used to be both a timed drop and a skill in the pool, which
    /// meant the commonest way to meet it was as one of three pickups - so
    /// healing cost an upgrade, and taking an upgrade cost the heal. Neither is a
    /// trade worth making the player think about: one is a reward for killing
    /// well and the other is what you need when you are not.
    ///
    /// Separating them costs the tension a timer had - health no longer arrives
    /// mid-fight and forces a decision about breaking position - and buys a
    /// player who is losing a reliable idea of when relief comes. It also means
    /// health cannot arrive during the stretch when a struggling player most
    /// needs it, because they are levelling slowly. That is the known cost of
    /// this arrangement.
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
        [Min(0)]
        [Tooltip("Levels between health drops. 2 means one on every even level. Zero turns " +
                 "them off entirely.")]
        private int healthEveryLevels = 2;

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

        private void Start()
        {
            ResolveReferences();

            if (pickupPrefab != null)
            {
                ObjectPool.Warm(pickupPrefab, warmup);
            }
        }

        /// <summary>Whether the level just reached is one the cadence drops on.</summary>
        private bool HealthDueAt(int level)
        {
            return healthEveryLevels > 0 && level > 0 && level % healthEveryLevels == 0;
        }

        /// <summary>
        /// Puts everything a level-up brings on the ring: the upgrade offer, and
        /// a health drop when the level is due one.
        ///
        /// One call because they share the ring. Placed separately, each went
        /// through PickupPlacement.Bearings from the same player bearing with its
        /// own coin flip for direction - and the nearest bearing of a set is the
        /// same number whatever the set's size, so whenever the two flips agreed
        /// the health drop landed exactly on top of the first upgrade. Asking for
        /// all of them at once is what lets the spacing do its job.
        ///
        /// Sharing a placement does not make health part of the offer. It goes
        /// down with no SkillOffer attached, so taking it forfeits no upgrade and
        /// taking an upgrade does not clear it.
        /// </summary>
        /// <param name="level">The level just reached, for the health cadence.</param>
        /// <param name="skills">
        /// What to offer, already drawn from the pool but not yet charged
        /// against it - see SkillPool.Draw. May be empty once every upgrade is
        /// spent, in which case health goes out on its own.
        /// </param>
        public void OfferLevelUp(int level, IReadOnlyList<SkillDefinition> skills)
        {
            if (pickupPrefab == null)
            {
                return;
            }

            int skillCount = skills != null ? skills.Count : 0;

            // Health also stands in when there is nothing left to offer: a
            // level-up that puts nothing on the ring reads as broken pickups
            // rather than as a finished build.
            bool health = HealthDueAt(level) || skillCount == 0;

            int total = skillCount + (health ? 1 : 0);
            if (total == 0)
            {
                return;
            }

            float bearing = CurrentPlayerBearing();
            float height = player != null ? player.position.y : transform.position.y;

            float[] bearings = PickupPlacement.Bearings(
                bearing,
                total,
                offerSeparation,
                clockwise: Random.value < 0.5f);

            // A slot at random rather than always the nearest or always the
            // furthest. Either fixed choice teaches the player which pickup to
            // fly at before they have looked at what is on offer.
            int healthSlot = health ? Random.Range(0, total) : -1;

            var offer = new SkillOffer();
            int nextSkill = 0;

            for (int i = 0; i < total && i < bearings.Length; i++)
            {
                if (i == healthSlot)
                {
                    Place(
                        bearings[i], height, null, healthAmount, healthColor,
                        HealthCaption(), healthLifetime);
                    continue;
                }

                SkillDefinition skill = skills[nextSkill++];
                if (skill == null)
                {
                    continue;
                }

                offer.Add(Place(
                    bearings[i], height, skill, 0, skill.PickupColor,
                    CaptionFor(skill), offerLifetime));
            }

            if (offer.LiveCount > 0)
            {
                offers.Add(offer);
            }
        }

        /// <summary>
        /// Puts one pickup on the ring at a bearing.
        ///
        /// A null skill with a heal amount is a health drop: no SkillOffer is
        /// attached, so Pickup.Offer stays null and ClearOffer walks away from it
        /// when an upgrade is taken - and it walks away from the upgrades when it
        /// is taken. That absence is the whole of what makes health independent.
        /// </summary>
        /// <summary>
        /// What the label above an upgrade should say.
        ///
        /// Asked of SkillSelect rather than of the skill directly, because the
        /// wording of a multi-stage upgrade depends on how many times it has
        /// already been taken and only the pool knows that. Falls back to the
        /// plain display name when no SkillSelect is wired - the same scene state
        /// that makes OnCollected grant skills without recording them, where a
        /// label naming the wrong stage would be a worse failure than a generic
        /// one.
        /// </summary>
        private string CaptionFor(SkillDefinition skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            // The fallback assumes a first pick, which is the best guess available
            // without the pool. It only ever shows in a scene with no SkillSelect
            // wired - the same state where OnCollected grants skills without
            // recording them - so being one stage optimistic is the least of it.
            return skillSelect != null ? skillSelect.PreviewName(skill) : skill.GetPickupName(1);
        }

        /// <summary>
        /// What the label above a health drop says.
        ///
        /// Labelled for the same reason the upgrades are, even though health is
        /// not one: it lands in the same set, at the same moment, looking like
        /// the same kind of object. An unlabelled pickup among labelled ones
        /// reads as a label that failed rather than as a different sort of thing.
        /// </summary>
        private string HealthCaption()
        {
            return $"+{healthAmount} Health";
        }

        private Pickup Place(
            float bearing,
            float height,
            SkillDefinition skill,
            int healAmount,
            Color color,
            string caption,
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

            pickup.Configure(this, skill, healAmount, color, caption, lifetime);
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
