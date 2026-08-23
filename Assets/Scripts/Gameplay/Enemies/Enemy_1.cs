using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class Enemy_1 : MonoBehaviour
    {
        [SerializeField]
        private GameObject childObject;
        [SerializeField]
        private GameObject enemyHit;
        [SerializeField]
        private GameObject explosion;

        [SerializeField]
        [Tooltip("Stats for this enemy. Falls back to the health value below when unset.")]
        private EnemyDefinition definition;

        [SerializeField]
        private int healthPoints = 1;

        [SerializeField]
        private GameObject EnemyShip;

        [Header("Shoot")]
        [SerializeField]
        private Transform shootPivot;

        [SerializeField]
        private Transform shootPivot_1;

        [SerializeField]
        private GameObject shootPrefab;

        [SerializeField]
        private GameObject shootPrefab1;

        [Header("Delay")]
        [SerializeField]
        [Range(0f, 10f)]
        private float initialDelay = 1f;

        [SerializeField]
        [Range(0f, 10f)]
        private float spawnDelay = 1;

        private HealthState health;
        private EnemyMovement movement;
        private HitFlash flash;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Shoot"))
            {
                Vector3 impact = other.transform.position;

                ObjectPool.Despawn(other.gameObject);

                // Death effects and the reward now fire here rather than from
                // Update(), which only ever ran because Destroy is deferred.
                //
                // One effect per outcome, matching Enemy.cs. This used to play the
                // spark on every hit including the fatal one, so a kill fired both
                // and the two outcomes read as the same event.
                if (health.TakeDamage(1))
                {
                    ObjectPool.Spawn(explosion, transform.position, transform.rotation);
                    Death();
                    ObjectPool.Despawn(gameObject);
                }
                else
                {
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

        private void Awake()
        {
            // Once is enough: the reference is to a child of this same prefab, so it
            // survives a trip through the pool.
            if (EnemyShip != null)
            {
                EnemyShip.TryGetComponent(out movement);
            }

            // On this object rather than on EnemyShip, so it gathers every
            // renderer under the prefab rather than only the ship's own.
            flash = HitFlash.On(gameObject);
        }

        /// <summary>
        /// Everything that has to be true at the start of a life, rather than at the
        /// start of the object's existence.
        ///
        /// Enemies are pooled, so Awake runs once and OnEnable runs on every spawn.
        /// Health left in Awake would come back at zero on a reused enemy - it died
        /// there - and it would fall to the next bullet whatever its definition says.
        ///
        /// The firing invoke is cancelled before it is armed because a repeat left
        /// over from the previous life would stack: two invokes, then three, and an
        /// enemy that fires faster the more times it has been recycled.
        /// </summary>
        private void OnEnable()
        {
            health = new HealthState(definition != null ? definition.MaxHealth : healthPoints);

            CancelInvoke(nameof(Shoot));
            InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Shoot));
        }

        private void Shoot()
        {
            // Cached and guarded: this fires on a repeating invoke, so an unguarded
            // lookup would throw for the whole lifetime of a mis-wired prefab.
            //
            // The pivots needed the same guard and did not have it. The movement
            // check below only decides which prefab travels which way; the pivots
            // were dereferenced as arguments on either side of it, so a prefab
            // missing one threw every spawnDelay for as long as that enemy lived -
            // and pooling means it comes back. Player.FireLine refuses the same way.
            // The prefabs are left to ObjectPool.Spawn, which already warns and
            // returns on a null one.
            if (shootPivot == null || shootPivot_1 == null)
            {
                return;
            }

            GameObject bullet = movement != null && movement.TravellingLeft
                ? shootPrefab
                : shootPrefab1;

            ObjectPool.Spawn(bullet, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));
            ObjectPool.Spawn(bullet, shootPivot_1.position, Quaternion.Euler(0f, 0f, 90f));
        }

        private void Death()
        {
            int reward = definition != null ? definition.ExperienceReward : 15;

            if (EXP.Instance != null)
            {
                EXP.Instance.AddEXP(reward);
            }

            PickupLabelBoard.Experience(transform.position, reward);
            RunStats.RecordKill(reward);

            PlayDeathSound();
        }

        /// <summary>
        /// This enemy's own death sound if it has one, otherwise the shared one.
        ///
        /// Matches Enemy.PlayDeathSound. This type had no death sound at all, which
        /// also meant EnemyDefinition.DeathSound was only ever read on the other
        /// enemy path - authoring one here looked wired and produced silence.
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
