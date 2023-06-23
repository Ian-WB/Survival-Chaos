using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript_3 : MonoBehaviour
{
    public GameObject EnemyShip;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            EnemyShip.GetComponent<BossScript>().lazer = true;
        }
        else
        {
            EnemyShip.GetComponent<BossScript>().lazer = false;
        }
    }
}
