using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_1 : MonoBehaviour
{
    public GameObject childObject;
    public GameObject enemyHit;
    public GameObject explosion;

     [SerializeField]
    private int healthPoints = 1;

    public GameObject EnemyShip;

    int EXPGain = 2;

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Shoot"))
        {

            Destroy(other.gameObject);

            // Update Health Points

            healthPoints--;
             Instantiate (enemyHit , childObject.transform.position , childObject.transform.rotation);

            // Check if Health Points is below 0 to destroy it

            if (healthPoints <= 0)
            {

                Destroy(gameObject);
            }
        }
    }


    void Update()
    {
        if (healthPoints <= 0)
        {
            Instantiate(explosion , transform.position , transform.rotation);
            Destroy(gameObject);
            //Added by Luis Fernando, Working on the EXP System.
            Death();
        }
    }


    private void Awake()
    {
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


    //Added by Luis Fernando, Working on the EXP System.
    void Death()
    {
        EXP.Instance.AddEXP(EXPGain);
    }
    public void adjustHealth(int health)
    {
        healthPoints += health;
    }
}
