using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript_2 : MonoBehaviour
{
    public GameObject EnemyShip;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            EnemyShip.GetComponent<EnemyMovement>().leftOrRight = true;
            EnemyShip.GetComponent<BossMovement>().leftOrRight_2 = true;
        }
    }
}
