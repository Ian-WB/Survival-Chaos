using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript_3 : MonoBehaviour
{
    public GameObject EnemyShip;


    private void OnTriggerEnter(Collider other)
    {
        if (EnemyShip.TryGetComponent(out BossScript bossScript))
        {
            bossScript.lazer = other.CompareTag("Player");
        }
    }
}
