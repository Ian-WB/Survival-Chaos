using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{

    [SerializeField]
    private int healthPoints = 1;

    int EXPGain = 5;

    void Update()
    {
        if (healthPoints <= 0)
        {
            Destroy(gameObject);
            //Added by Luis Fernando, Working on the EXP System.
            Death();
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
    // private void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("Shoot"))
    //     {

    //         Destroy(other.gameObject);

    //         // Update Health Points

    //         healthPoints--;

    //         // Check if Health Points is below 0 to destroy it

    //         if (healthPoints <= 0)
    //         {

    //             Destroy(gameObject);
    //         }
    //     }
    // }
}
