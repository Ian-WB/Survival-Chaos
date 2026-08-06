using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript : MonoBehaviour
{

    public GameObject EnemyShip;

    // Value applied to EnemyMovement.leftOrRight when the player crosses this trigger.
    [SerializeField]
    private bool leftOrRight;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (EnemyShip.TryGetComponent(out EnemyMovement enemyMovement))
            {
                enemyMovement.leftOrRight = leftOrRight;
            }
        }
    }
}
