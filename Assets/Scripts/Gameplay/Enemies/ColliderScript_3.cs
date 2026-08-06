using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class ColliderScript_3 : MonoBehaviour
{
    public GameObject EnemyShip;


    private void OnTriggerEnter(Collider other)
    {
        if (EnemyShip.TryGetComponent(out BossEmitter boss))
        {
            boss.LaserActive = other.CompareTag("Player");
        }
    }
}
