using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    /// Health arrives as salvage: a small piece of repair scrap left where
    /// something the player destroyed went down, due more often the more health
    /// is missing - see <see cref="SalvageClock"/>. It is never part of an offer.
    ///
    /// This is health's third design. As a skill in the pool it was one of three
    /// pickups, so healing cost an upgrade and taking an upgrade cost the heal.
    /// As a drop every second level it sat in the same fan as the upgrades,
    /// looking like one of them without being one, and it ran on the levelling
    /// clock - so it came least to a struggling player, who levels slowly, and
    /// hardly at all in the boss fight, where the waves have stopped.
    ///
    /// Salvage answers need rather than progress, and it turns up in the fights
    /// where the damage is being taken. Fetching it means breaking position in
    /// the middle of one, which is the decision the old timed drop asked for
    /// and the level cadence lost.
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
        [Tooltip("How many to build up front. The default covers one full offer plus two " +
                 "pieces of salvage overlapping it.")]
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
        private float offerLifetime = 28f;

        [Header("Salvage")]
        [SerializeField]
        [Min(1f)]
        [Tooltip("Seconds between pieces of salvage at half health. The wait follows how much " +
                 "health is missing: twice this with a quarter gone, two thirds of it with three " +
                 "quarters gone, never at full health. A piece that is due waits for the next " +
                 "thing the player destroys.")]
        private float salvageSecondsAtHalfHealth = 12f;

        [SerializeField]
        [Min(1)]
        [Tooltip("Health one piece restores.")]
        private int salvageAmount = 1;

        [SerializeField]
        [Tooltip("Seconds a piece stays out. Half a lap of the ring takes about 10.5 at base " +
                 "speed, so a piece anywhere can be reached - the cost of fetching it is leaving " +
                 "the fight, not the distance.")]
        private float salvageLifetime = 15f;

        [SerializeField]
        [Range(0.2f, 1f)]
        [Tooltip("Size of a piece against an upgrade. Only the glowing core shrinks; the grab " +
                 "radius stays the same, so it reads as scrap without being harder to take.")]
        private float salvageSize = 0.6f;

        [SerializeField]
        [FormerlySerializedAs("healthColor")]
        [ColorUsage(showAlpha: false, hdr: true)]
        private Color salvageColor = new Color(0.2f, 2f, 0.6f);

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

        private readonly SalvageClock salvage = new SalvageClock();

        /// <summary>
        /// Holds the player's height band, so salvage from a wreck above or below
        /// it still lands where the player can fly. Found on the player.
        /// </summary>
        private ApplyBounds band;

        /// <summary>
        /// The spawner wrecks report to. One per scene - the callers are pooled
        /// enemies and hull plates, which have no reference to it.
        /// </summary>
        private static PickupSpawner active;

        private void OnEnable()
        {
            active = this;
        }

        private void OnDisable()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private void Start()
        {
            ResolveReferences();

            if (pickupPrefab != null)
            {
                ObjectPool.Warm(pickupPrefab, warmup);
            }
        }

        /// <summary>
        /// Scaled time, so the clock stops with the game: a paused player is not
        /// owed repairs for the time they spent on the pause screen.
        /// </summary>
        private void Update()
        {
            if (playerTarget != null)
            {
                salvage.Advance(
                    Time.deltaTime,
                    playerTarget.CurrentHealth,
                    playerTarget.MaxHealth,
                    salvageSecondsAtHalfHealth);
            }
        }

        /// <summary>
        /// Something the player destroyed went down here. Leaves a piece of
        /// salvage when one is due.
        ///
        /// Static because the callers are enemies and hull plates dying, and
        /// none of them should have to know whether the scene has a spawner.
        /// Silent when it does not.
        /// Only real kills report - an enemy that rams the player dies silently
        /// and leaves nothing, the same as it earns no experience.
        /// </summary>
        public static void ReportWreck(Vector3 where)
        {
            if (active != null)
            {
                active.Salvage(where);
            }
        }

        private void Salvage(Vector3 where)
        {
            if (pickupPrefab == null || playerTarget == null || RunOutcome.RunEnded)
            {
                return;
            }

            if (!salvage.TrySpend(playerTarget.CurrentHealth, playerTarget.MaxHealth))
            {
                return;
            }

            Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
            PlaceSalvage(PickupPlacement.BearingOf(where, center), ReachableHeight(where.y));
        }

        /// <summary>
        /// A height inside the band the player is held in.
        ///
        /// Everything the player can shoot is at a height they can fly to - their
        /// rounds never change height - so this is a guard rather than a routine
        /// correction. A piece a few units outside the band would sit in plain
        /// view and never be collectable, and nothing about it would say why.
        /// </summary>
        private float ReachableHeight(float height)
        {
            return band != null && band.TryGetBand(out float floor, out float ceiling)
                ? Mathf.Clamp(height, floor, ceiling)
                : height;
        }

        /// <summary>
        /// Puts the upgrade offer a level-up brings on the ring.
        ///
        /// Health is no longer part of this. It rode along every second level,
        /// placed in the same fan, and a green pickup among three labelled ones
        /// read as a fourth choice whatever the code said about it.
        /// </summary>
        /// <param name="skills">
        /// What to offer, already drawn from the pool but not yet charged
        /// against it - see SkillPool.Draw. May be empty once every upgrade is
        /// spent, in which case a piece of salvage goes out instead.
        /// </param>
        public void OfferLevelUp(IReadOnlyList<SkillDefinition> skills)
        {
            if (pickupPrefab == null)
            {
                return;
            }

            int skillCount = skills != null ? skills.Count : 0;

            float bearing = CurrentPlayerBearing();
            float height = player != null ? player.position.y : transform.position.y;

            float[] bearings = PickupPlacement.Bearings(
                bearing,
                Mathf.Max(1, skillCount),
                offerSeparation,
                clockwise: Random.value < 0.5f);

            // A level-up that puts nothing on the ring reads as broken pickups
            // rather than as a finished build, so a spent pool leaves salvage.
            if (skillCount == 0)
            {
                if (bearings.Length > 0)
                {
                    PlaceSalvage(bearings[0], height);
                }

                return;
            }

            var offer = new SkillOffer();

            for (int i = 0; i < skillCount && i < bearings.Length; i++)
            {
                SkillDefinition skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                offer.Add(Place(
                    bearings[i], height, skill, 0, skill.PickupColor,
                    CaptionFor(skill), offerLifetime, 1f));
            }

            if (offer.LiveCount > 0)
            {
                offers.Add(offer);
            }
        }

        /// <summary>
        /// One piece of salvage, uncaptioned.
        ///
        /// The health drop carried a label because it landed among labelled
        /// upgrades, where an unlabelled pickup read as a label that had failed.
        /// Salvage lands on its own, out of an explosion, and a caption on every
        /// piece would be clutter in the middle of the fights it turns up in.
        /// What it restored is shown as it is taken, where the player is looking.
        /// </summary>
        private void PlaceSalvage(float bearing, float height)
        {
            Place(
                bearing, height, null, salvageAmount, salvageColor,
                string.Empty, salvageLifetime, salvageSize);
        }

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
        /// Puts one pickup on the ring at a bearing.
        ///
        /// A null skill with a heal amount is salvage: no SkillOffer is attached,
        /// so Pickup.Offer stays null and ClearOffer walks away from it when an
        /// upgrade is taken - and it walks away from the upgrades when it is
        /// taken. That absence is the whole of what makes health independent.
        /// </summary>
        private Pickup Place(
            float bearing,
            float height,
            SkillDefinition skill,
            int healAmount,
            Color color,
            string caption,
            float lifetime,
            float size)
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

            pickup.Configure(this, skill, healAmount, color, caption, lifetime, size);
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

                    // Played here because the sound normally rides along inside
                    // ApplyCollected, which this branch is standing in for. Without
                    // it the degraded path is silent as well as unannounced, and
                    // flying into an upgrade would feel like missing it.
                    PlayCollectSound();
                }
            }
            else if (playerTarget != null)
            {
                int before = playerTarget.CurrentHealth;
                playerTarget.Heal(pickup.HealAmount);

                // What it actually restored, which at full health is nothing -
                // and a "+1" that did not happen is worse than no number.
                PickupLabelBoard.Health(
                    pickup.transform.position, playerTarget.CurrentHealth - before);
                PlayCollectSound();
            }

            ClearOffer(pickup);
            ObjectPool.Despawn(pickup.gameObject);

            // After ClearOffer, so the offer just answered is off the list and
            // only the others are checked. Salvage charges nothing, so it
            // cannot have made anything stale.
            if (pickup.Skill != null)
            {
                RefreshOffers();
            }
        }

        /// <summary>
        /// Checks every upgrade still out on the ring against the pool, now that
        /// a pick has been charged. Called whenever one is.
        ///
        /// An offer is drawn for the level-up that put it out, and the ones
        /// before it may still be waiting. Take Shot Upgrade from one and the
        /// same skill in another is no longer what it says: its label names the
        /// stage the player now has, and taking it would hand over the next
        /// stage whatever that stage's level gate says, or a pick past the
        /// limit. The level-4 and level-5 offers both carrying it gave Triple
        /// Shot at level 5 instead of 10, under a pickup that read "Double
        /// Shot".
        ///
        /// A pickup whose skill can still be taken is relabelled for its next
        /// stage. One that cannot gets another skill the pool can still field,
        /// keeping its place and its remaining time, so the level-up it came
        /// from is still worth something. With nothing left to swap in it
        /// leaves the ring, and if that empties its offer, a piece of salvage
        /// takes its place - what a level-up with the pool spent gets anyway.
        /// </summary>
        public void RefreshOffers()
        {
            if (skillSelect == null)
            {
                return;
            }

            for (int i = offers.Count - 1; i >= 0; i--)
            {
                SkillOffer offer = offers[i];

                // A copy: members are swapped or removed as the loop goes.
                var members = new List<Pickup>(offer.Members);
                var onOffer = new List<SkillDefinition>(members.Count);

                foreach (Pickup member in members)
                {
                    if (member != null && member.Skill != null)
                    {
                        onOffer.Add(member.Skill);
                    }
                }

                foreach (Pickup member in members)
                {
                    if (member == null || member.Skill == null)
                    {
                        continue;
                    }

                    if (skillSelect.CanOffer(member.Skill))
                    {
                        member.Retarget(member.Skill, member.Skill.PickupColor, CaptionFor(member.Skill));
                        continue;
                    }

                    SkillDefinition replacement = skillSelect.DrawReplacement(onOffer);

                    if (replacement != null)
                    {
                        onOffer.Add(replacement);
                        member.Retarget(replacement, replacement.PickupColor, CaptionFor(replacement));
                        continue;
                    }

                    Vector3 where = member.transform.position;
                    bool spent = offer.Remove(member);
                    ObjectPool.Despawn(member.gameObject);

                    if (spent)
                    {
                        offers.RemoveAt(i);

                        Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
                        PlaceSalvage(PickupPlacement.BearingOf(where, center), ReachableHeight(where.y));
                    }
                }
            }
        }

        /// <summary>
        /// Where a pickup resting at <paramref name="anchor"/> should be after
        /// drifting toward the player for <paramref name="seconds"/>, for the
        /// Magnet upgrade. Unchanged with no magnet, or out of its reach.
        ///
        /// Straight at the ship rather than round the ring. Inside the widest
        /// reach the chord runs at most a third of a unit inside the lane, and
        /// the path ends on the ship either way. Asked by each pickup rather than
        /// swept from here, because pickups hold their own anchor and the spawner
        /// keeps no list of salvage.
        /// </summary>
        public Vector3 Attract(Vector3 anchor, float seconds)
        {
            if (playerTarget == null || player == null || seconds <= 0f || RunOutcome.RunEnded)
            {
                return anchor;
            }

            float reach = playerTarget.MagnetReach;
            Vector3 ship = player.position;

            if (reach <= 0f || (ship - anchor).sqrMagnitude > reach * reach)
            {
                return anchor;
            }

            return Vector3.MoveTowards(anchor, ship, playerTarget.MagnetPullSpeed * seconds);
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
        /// orbiting at, falling back to the lane radius.
        ///
        /// Measured rather than taken from ArenaGeometry because the two can
        /// still differ, if only for a moment. The player's radius offset now
        /// moves the lane itself, so they agree once SnapToOrbit has run - but
        /// pickups on the wrong circle are not slightly off, they are
        /// unreachable, and nothing about that bug would point at this line.
        /// </summary>
        private float CurrentOrbitRadius(Vector3 center)
        {
            if (player == null)
            {
                return ArenaGeometry.LaneRadius;
            }

            Vector3 flat = player.position - center;
            flat.y = 0f;

            // Before SnapToOrbit has run, or if the player somehow sits on the
            // axis, the measurement is meaningless and the lane is better.
            return flat.sqrMagnitude < 0.01f ? ArenaGeometry.LaneRadius : flat.magnitude;
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

            // Where BossEmitter looks for it too. Without one, salvage keeps the
            // wreck's own height, which is inside the band for everything the
            // player can reach to destroy.
            if (band == null && player != null)
            {
                band = player.GetComponentInParent<ApplyBounds>();

                if (band == null)
                {
                    band = player.GetComponentInChildren<ApplyBounds>();
                }
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
