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
            // Regular enemies only have EnemyMovement, so both lookups are optional.
            if (EnemyShip.TryGetComponent(out EnemyMovement enemyMovement))
            {
                enemyMovement.leftOrRight = true;
            }

            if (EnemyShip.TryGetComponent(out BossMovement bossMovement))
            {
                bossMovement.leftOrRight_2 = true;
            }
        }
    }
}
