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
            Destroy(other.gameObject);

            if (health.TakeDamage(1))
            {
                Instantiate(explosion, transform.position, transform.rotation);
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
    }
}
