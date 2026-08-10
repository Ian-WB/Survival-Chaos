using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class Enemy : MonoBehaviour
{

    public GameObject explosion;

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
            ObjectPool.Despawn(other.gameObject);

            if (health.TakeDamage(1))
            {
                ObjectPool.Spawn(explosion, transform.position, transform.rotation);
                Death();
                Destroy(gameObject);
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
