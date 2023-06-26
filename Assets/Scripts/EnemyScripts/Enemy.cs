using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    
    public GameObject explosion;

    [SerializeField]
    private int healthPoints = 1;

    public GameObject EnemyShip;
    int EXPGain = 5;

    

  
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
                Instantiate (explosion , transform.position , transform.rotation);
                Destroy(gameObject);
                Death();
            }
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
