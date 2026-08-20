using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpaceShip : MonoBehaviour
{

    [SerializeField]
    private GameObject EnemyShip;

    private EnemyMovement movement;

    private void Awake()
    {
        // Cached rather than fetched every frame, and guarded so a prefab
        // without EnemyMovement cannot throw once per frame forever.
        if (EnemyShip != null)
        {
            EnemyShip.TryGetComponent(out movement);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (movement == null)
        {
            return;
        }

        if(movement.TravellingLeft)
        {
            transform.localRotation = Quaternion.Euler(0, -90, 0);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
    }
}
