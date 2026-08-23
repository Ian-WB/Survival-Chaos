using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SurvivalChaos
{
    /// <summary>
    /// Owns the run's skill pool and turns a level-up into an offer on the ring.
    ///
    /// This used to apply a random skill the instant the bar filled. That is the
    /// single biggest reason the player felt strong from the first minute: every
    /// upgrade was free, arrived without being asked for, and cost no attention at
    /// a moment when attention is the only currency the game charges. Routing the
    /// same rewards through pickups keeps the power curve and puts a price on it.
    /// </summary>
    public class SkillSelect : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Skills this run can draw from. Healing is not one - it arrives on its own " +
                 "cadence from PickupSpawner, and any HealSkill listed here is ignored.")]
        private List<SkillDefinition> skills = new List<SkillDefinition>();

        [SerializeField]
        [Tooltip("Places the offer on the ring. Left empty, level-ups fall back to granting a " +
                 "skill outright so the game still runs in a scene that has not been wired yet.")]
        private PickupSpawner pickups;

        [SerializeField]
        private Player player;
        [SerializeField]
        private TextMeshProUGUI skillText;
        [SerializeField]
        private GameObject skillTextObject;

        private SkillPool pool;

        /// <summary>
        /// Guards the fallback warning. Without it a run with no spawner wired logs
        /// once per level-up, which buries whatever else is in the console.
        /// </summary>
        private bool warnedAboutMissingSpawner;

        void Start()
        {
            pool = new SkillPool(Upgrades());
        }

        /// <summary>
        /// The offerable skills, which is everything in the list that is not healing.
        ///
        /// Filtered here rather than left to whoever edits the scene, because putting
        /// Heal back in the list is a one-click mistake with a confusing symptom:
        /// healing would silently become a skill again, and taking it would forfeit
        /// the two upgrades beside it. The list is data; this is the rule.
        /// </summary>
        private List<SkillDefinition> Upgrades()
        {
            var upgrades = new List<SkillDefinition>();
            int healing = 0;

            foreach (SkillDefinition skill in skills)
            {
                if (skill is HealSkill)
                {
                    healing++;
                    continue;
                }

                upgrades.Add(skill);
            }

            if (healing > 0)
            {
                Debug.Log(
                    $"SkillSelect ignored {healing} healing skill(s) in its list. Health is not an " +
                    "upgrade any more - PickupSpawner drops it on its own level cadence, so that it " +
                    "does not cost the player an upgrade to take.", this);
            }

            return upgrades;
        }

        void Update()
        {
            if(GameInput.DebugLevelUpPressed){
                PickSkill();
            }
        }

        /// <summary>
        /// Answers a level-up. Draws what the offer needs and puts it on the ring;
        /// nothing is granted and nothing is charged against the pool until the
        /// player actually collects one.
        /// </summary>
        public void PickSkill(){
            if(pool == null){
                pool = new SkillPool(Upgrades());
            }

            if (pickups == null)
            {
                GrantDirectly();
                return;
            }

            // Drawn here, placed there. The spawner decides whether health is due and
            // lays everything out in one go - two separate placements from the same
            // player bearing landed on top of each other.
            //
            // An empty draw is not a special case: every upgrade being spent is
            // something the spawner handles, by sending health out on its own.
            pickups.OfferLevelUp(
                player != null ? player.currentLevel : 0,
                pool.Draw(pickups.OfferSize));
        }

        /// <summary>
        /// Applies a skill the player has just flown into, and only now charges it
        /// against its pick limit. The skills that were offered alongside it were
        /// never charged, so they are still available next level.
        /// </summary>
        public void ApplyCollected(SkillDefinition skill)
        {
            if (skill == null)
            {
                return;
            }

            pool?.RecordPick(skill);
            RunStats.RecordSkill(skill.DisplayName);
            skill.Apply(player);

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.SkillPicked);
            }

            int picksTaken = pool?.PicksTaken(skill) ?? 1;
            ShowBanner(skill.GetDisplayName(picksTaken));
        }

        /// <summary>
        /// What a skill would be called if the player took it next, for the label on
        /// its pickup.
        ///
        /// One past the current count, not the current count. An offer is drawn but
        /// not charged - the pick is only recorded in ApplyCollected - so at the
        /// moment the pickup goes on the ring the pool still reads the number of
        /// picks *before* this one. Passing that straight through would label the
        /// second Shot Upgrade "Double Shot!", which is the stage the player already
        /// has.
        ///
        /// Lives here rather than on the spawner because the pool does, and the pool
        /// is the only thing that knows how far through a multi-stage skill a run is.
        /// </summary>
        public string PreviewName(SkillDefinition skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            int taken = pool?.PicksTaken(skill) ?? 0;
            return skill.GetPickupName(taken + 1);
        }

        /// <summary>
        /// The old behaviour, kept only as a fallback for a scene with no spawner
        /// assigned. Warns once, because silently reverting to free upgrades would
        /// look like the pickups were simply not working.
        /// </summary>
        private void GrantDirectly()
        {
            if (!warnedAboutMissingSpawner)
            {
                warnedAboutMissingSpawner = true;
                Debug.LogWarning(
                    "SkillSelect has no PickupSpawner assigned, so level-ups are granting skills " +
                    "outright instead of offering them on the ring.", this);
            }

            SkillDefinition skill = pool.Next();
            if (skill == null)
            {
                return;
            }

            skill.Apply(player);

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.SkillPicked);
            }

            ShowBanner(skill.GetDisplayName(pool.PicksTaken(skill)));
        }

        /// <summary>The banner still counting down, so the next pick can stop it.</summary>
        private Coroutine banner;

        /// <summary>
        /// Shows one level-up banner, replacing whichever is still on screen.
        ///
        /// Two picks inside three seconds used to leave two of these running, and
        /// the first to finish hid the object the second was still using. The newer
        /// skill's name flashed up and disappeared early, on the reading that three
        /// seconds had passed - since a different pick. Nothing pauses on
        /// collection: pickups are flown into, and an offer left on the ring can be
        /// taken moments before the next level-up puts another one out, so the two
        /// are not as far apart as the level curve suggests.
        ///
        /// Stopping the old one is what restarts the three seconds, rather than
        /// merely keeping the object on screen. There is nothing to queue: the
        /// banner is one line of text with no state of its own, and the skill it
        /// named has already been applied.
        /// </summary>
        private void ShowBanner(string skillName)
        {
            if (banner != null)
            {
                StopCoroutine(banner);
            }

            banner = StartCoroutine(showText(skillName));
        }

        IEnumerator showText(string skillName)
        {
            skillTextObject.SetActive(true);
            skillText.text = "Level Up!\n" + skillName;

            yield return new WaitForSeconds(3);
            skillTextObject.SetActive(false);
            banner = null;
        }
    }
}
