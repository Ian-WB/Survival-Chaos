using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript : MonoBehaviour
{

    [SerializeField]
    private GameObject EnemyShip;

    // Value applied to EnemyMovement.leftOrRight when the player crosses this trigger.
    [SerializeField]
    private bool leftOrRight;


    private bool warned;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (EnemyShip == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning(
                    "ColliderScript has no Enemy Ship assigned, so crossing this trigger " +
                    "will not turn the enemy around.", this);
            }

            return;
        }

        if (EnemyShip.TryGetComponent(out EnemyMovement enemyMovement))
        {
            enemyMovement.leftOrRight = leftOrRight;
        }
    }
}
