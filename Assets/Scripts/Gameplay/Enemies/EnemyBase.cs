using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// What every enemy on the ring shares: health, being hit, dying and the
    /// debug menu's two ways out.
    ///
    /// Enemy and Enemy_1 used to carry two copies of all of this, and the copies
    /// drifted. The gunship went without a death sound, and later without Kill
    /// and DebugDespawn, so Clear Arena left both of the ships that shoot on the
    /// ring (scan of 28 September 2026). Code that wants every enemy should ask
    /// for this type, and then the next thing added reaches both.
    ///
    /// The serialized fields kept their names when they moved here from the two
    /// subclasses, which is all Unity matches them on, so the four enemy prefabs
    /// load exactly as they did.
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour
    {
        [SerializeField]
        private GameObject explosion;

        /// <summary>
        /// Played where a shot lands without killing.
        ///
        /// Mattered little while every enemy on this script died to one bullet - the
        /// explosion was the hit feedback, because a hit was always a kill. Now that
        /// the Drone takes three, two of every three shots at the most common enemy
        /// in the game produce nothing at all, and a shot that lands like a shot that
        /// missed is the worst thing a shooter can do.
        /// </summary>
        [SerializeField]
        private GameObject enemyHit;

        [SerializeField]
        [Tooltip("Stats for this enemy. Falls back to the health value below when unset.")]
        private EnemyDefinition definition;

        [SerializeField]
        private int healthPoints = 1;

        [SerializeField]
        private GameObject EnemyShip;

        private HealthState health;
        private HitFlash flash;

        /// <summary>The ship model under this object, for the gunship to find its movement on.</summary>
        protected GameObject Ship => EnemyShip;

        /// <summary>Experience for a kill when no definition is wired.</summary>
        protected abstract int FallbackReward { get; }

        /// <summary>
        /// Found once. The component is added to this same object the first time
        /// it is asked for, so a trip through the pool keeps it.
        ///
        /// On this object rather than on EnemyShip, so it gathers every renderer
        /// under the prefab rather than only the ship's own.
        /// </summary>
        protected virtual void Awake()
        {
            flash = HitFlash.On(gameObject);
        }

        /// <summary>
        /// Health is rebuilt on every spawn, not just the first.
        ///
        /// This was Awake, which only runs on an object's first life. Enemies are
        /// pooled now, so a reused one would come back with whatever health it died
        /// on - which is zero, so it would die to the next bullet that touched it
        /// regardless of what its definition says.
        /// </summary>
        protected virtual void OnEnable()
        {
            health = new HealthState(definition != null ? definition.MaxHealth : healthPoints);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Shoot"))
            {
                // Read before despawning. The effect belongs where the bullet struck
                // rather than at the enemy's centre - on a ship several units wide
                // those are visibly different places, and the whole point is to show
                // the player their shot connected.
                Vector3 impact = other.transform.position;

                // Spends the round. One already spent on another enemy in this
                // physics step is not a second hit.
                if (!ShootScript.Land(other))
                {
                    return;
                }

                // One effect per outcome: a spark for a hit, an explosion for a kill.
                // Playing both on the fatal shot buries the explosion in its own
                // sparks and makes the two outcomes harder to tell apart, which is
                // exactly the distinction multi-hit enemies need the player to read.
                if (health.TakeDamage(1))
                {
                    ObjectPool.Spawn(explosion, transform.position, transform.rotation);
                    Death();
                    ObjectPool.Despawn(gameObject);
                }
                else
                {
                    // The hull reacts whether or not a spark prefab is wired, so
                    // the two halves of the feedback fail independently rather
                    // than an unassigned field costing both.
                    if (flash != null)
                    {
                        flash.Strike();
                    }

                    if (enemyHit != null)
                    {
                        ObjectPool.Spawn(enemyHit, impact, transform.rotation);
                    }
                }
            }
        }

        /// <summary>
        /// Kills this enemy outright, for the debug menu.
        ///
        /// Takes the same exit as a fatal shot - explosion, reward, despawn -
        /// rather than destroying the object. These are pooled, and a debug tool
        /// that removed one from circulation would quietly starve the pool it was
        /// meant to be testing.
        /// </summary>
        public void Kill()
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            health.TakeDamage(health.Current);
            ObjectPool.Spawn(explosion, transform.position, transform.rotation);
            Death();
            ObjectPool.Despawn(gameObject);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
        /// <summary>Returns a live enemy to its pool without XP, kill credit or effects.</summary>
        public void DebugDespawn()
        {
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(health.Current);
                ObjectPool.Despawn(gameObject);
            }
        }
#endif

        private void Death()
        {
            int reward = definition != null ? definition.ExperienceReward : FallbackReward;

            // The player scales the reward, so the player also shows it and
            // adds it to the run's total - see Player.HandleEXPChange.
            if (EXP.Instance != null)
            {
                EXP.Instance.AddEXP(reward, transform.position);
            }

            RunStats.RecordKill();
            PickupSpawner.ReportWreck(transform.position);

            PlayDeathSound();
        }

        /// <summary>
        /// This enemy's own death sound if it has one, otherwise the shared one.
        ///
        /// Positional, because enemies die all around the ring and where a kill
        /// happened is information the player can use.
        /// </summary>
        private void PlayDeathSound()
        {
            GameSounds sounds = GameSounds.Instance;
            SoundDefinition sound = definition != null && definition.DeathSound != null
                ? definition.DeathSound
                : (sounds != null ? sounds.EnemyDeath : null);

            GameSounds.PlayAt(sound, transform.position);
        }
    }
}
