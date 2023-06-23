using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossScript : MonoBehaviour
{
 [SerializeField]
    private int healthPoints = 1;

    public GameObject EnemyShip;

    int EXPGain = 2;

    [Header("Shoot")]
    [SerializeField]
    private Transform shootPivot;

    [SerializeField]
    private Transform shootPivot_1, shootPivot_2, shootPivot_3, shootPivot_4, shootPivot_5, shootPivot_6, shootPivot_7,
    shootPivot_8, shootPivot_9, shootPivot_10, shootPivot_11, shootPivot_12, shootPivot_13, shootPivot_14, shootPivot_15, 
    shootPivot_16, shootPivot_17, shootPivot_18, shootPivot_19, shootPivot_20, shootPivot_21, shootPivot_22, shootPivot_23,
    shootPivot_24, shootPivot_25, shootPivot_26, shootPivot_27, shootPivot_28, shootPivot_29, shootPivot_30, shootPivot_31;


    [SerializeField]
    private GameObject shootPrefab;

    [SerializeField]
    private GameObject shootPrefab_1;

    [SerializeField]
    private GameObject shootPrefab_2;

    [SerializeField]
    private GameObject shootPrefab_3;

    [Header("Delay")]
    [SerializeField]
    [Range(0f, 10f)]
    private float initialDelay = 1f;

    [SerializeField]
    [Range(0f, 10f)]
    private float spawnDelay = 1;

    [Header("Delay1")]
    [SerializeField]
    [Range(0f, 10f)]
    private float initialDelay1 = 1f;

    [SerializeField]
    [Range(0f, 10f)]
    private float spawnDelay1 = 1;

    [Header("Delay3")]
    [SerializeField]
    [Range(0f, 10f)]
    private float initialDelay2 = 1f;

    [SerializeField]
    [Range(0f, 10f)]
    private float spawnDelay2 = 1;

    [Header("Delay4")]
    [SerializeField]
    [Range(0f, 10f)]
    private float initialDelay3 = 1f;

    public bool lazer = false;

    [SerializeField]
    [Range(0f, 10f)]
    private float spawnDelay3 = 1;

    private bool phase2 = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Shoot"))
        {

            Destroy(other.gameObject);

            // Update Health Points

            healthPoints--;

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
            Destroy(gameObject);
            //Added by Luis Fernando, Working on the EXP System.
            Death();
        }

        if(lazer)
        {
            Shoot_1();
        }
        
    }


    private void Awake()
    {
        InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
        InvokeRepeating(nameof(Shoot_2), initialDelay, spawnDelay);
    }

    
    private void Shoot()
    {
        
        if (EnemyShip.GetComponent<EnemyMovement>().leftOrRight)
        {
            Instantiate(shootPrefab, shootPivot.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_1.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_2.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_3.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_4.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_5.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_6.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_7.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_8.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_9.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_10.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_11.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_12.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_13.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_14.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_15.position, Quaternion.Euler(0f, 0f, 0f));
               
        }
        
        else 
        {
            Instantiate(shootPrefab_1, shootPivot.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_1.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_2.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_3.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_4.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_5.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_6.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_7.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_8.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_9.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_10.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_11.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_12.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_13.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_14.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_15.position, Quaternion.Euler(0f, 0f, 0f));    
        }
        
    }

    private void Shoot_1()
    {
        if (EnemyShip.GetComponent<EnemyMovement>().leftOrRight)
        {
            Instantiate(shootPrefab_2, shootPivot_16.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_2, shootPivot_29.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_2, shootPivot_30.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_2, shootPivot_31.position, Quaternion.Euler(0f, 0f, 0f));
        }
        
        else 
        {
            Instantiate(shootPrefab_3, shootPivot_16.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_3, shootPivot_29.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_3, shootPivot_30.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_3, shootPivot_31.position, Quaternion.Euler(0f, 0f, 0f));  
        }
    }

    private void Shoot_2()
    {
        if (EnemyShip.GetComponent<EnemyMovement>().leftOrRight)
        {
            Instantiate(shootPrefab, shootPivot_16.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_17.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_18.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_19.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_20.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_21.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_22.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_23.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_24.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_25.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_26.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_27.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab, shootPivot_28.position, Quaternion.Euler(0f, 0f, 0f));    
        }
        
        else 
        {
            Instantiate(shootPrefab_1, shootPivot_16.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_17.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_18.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_19.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_20.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_21.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_22.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_23.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_24.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_25.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_26.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_27.position, Quaternion.Euler(0f, 0f, 0f));
            Instantiate(shootPrefab_1, shootPivot_28.position, Quaternion.Euler(0f, 0f, 0f));    
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
