using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class Enemy : MonoBehaviour
{

    public GameObject explosion;

    /// <summary>
    /// Played where a shot lands without killing.
    ///
    /// Mattered little while every enemy on this script died to one bullet - the
    /// explosion was the hit feedback, because a hit was always a kill. Now that
    /// the Drone takes three, two of every three shots at the most common enemy
    /// in the game produce nothing at all, and a shot that lands like a shot that
    /// missed is the worst thing a shooter can do.
    /// </summary>
    public GameObject enemyHit;

    [SerializeField]
    [Tooltip("Stats for this enemy. Falls back to the health value below when unset.")]
    private EnemyDefinition definition;

    [SerializeField]
    private int healthPoints = 1;

    public GameObject EnemyShip;

    private HealthState health;

    private void Awake()
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

            ObjectPool.Despawn(other.gameObject);

            // One effect per outcome: a spark for a hit, an explosion for a kill.
            // Playing both on the fatal shot buries the explosion in its own
            // sparks and makes the two outcomes harder to tell apart, which is
            // exactly the distinction multi-hit enemies need the player to read.
            if (health.TakeDamage(1))
            {
                ObjectPool.Spawn(explosion, transform.position, transform.rotation);
                Death();
                Destroy(gameObject);
            }
            else if (enemyHit != null)
            {
                ObjectPool.Spawn(enemyHit, impact, transform.rotation);
            }
        }
    }

    private void Death()
    {
        int reward = definition != null ? definition.ExperienceReward : 5;

        if (EXP.Instance != null)
        {
            EXP.Instance.AddEXP(reward);
        }

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
