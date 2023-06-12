using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript : MonoBehaviour
{

    public GameObject EnemyShip;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            EnemyShip.GetComponent<EnemyMovement>().leftOrRight = false;
        }
    }
}
