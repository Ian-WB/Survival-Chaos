using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class Enemy_1 : MonoBehaviour
{
    public GameObject childObject;
    public GameObject enemyHit;
    public GameObject explosion;

    [SerializeField]
    [Tooltip("Stats for this enemy. Falls back to the health value below when unset.")]
    private EnemyDefinition definition;

    [SerializeField]
    private int healthPoints = 1;

    public GameObject EnemyShip;

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Shoot"))
        {
            Destroy(other.gameObject);

            Instantiate(enemyHit, childObject.transform.position, childObject.transform.rotation);

            // Death effects and the reward now fire here rather than from
            // Update(), which only ever ran because Destroy is deferred.
            if (health.TakeDamage(1))
            {
                Instantiate(explosion, transform.position, transform.rotation);
                Death();
                Destroy(gameObject);
            }
        }
    }

    private void Awake()
    {
        health = new HealthState(definition != null ? definition.MaxHealth : healthPoints);
        InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
    }

    private void Shoot()
    {
        if (EnemyShip.GetComponent<EnemyMovement>().leftOrRight)
        {
            Instantiate(shootPrefab, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));
            Instantiate(shootPrefab, shootPivot_1.position, Quaternion.Euler(0f, 0f, 90f));
        }

        else
        {
            Instantiate(shootPrefab1, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));
            Instantiate(shootPrefab1, shootPivot_1.position, Quaternion.Euler(0f, 0f, 90f));
        }
    }

    private void Death()
    {
        int reward = definition != null ? definition.ExperienceReward : 15;

        if (EXP.Instance != null)
        {
            EXP.Instance.AddEXP(reward);
        }
    }
}
