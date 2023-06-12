using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpaceShip : MonoBehaviour
{

    public GameObject EnemyShip;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
         if(EnemyShip.GetComponent<EnemyMovement>().leftOrRight)
        {
            transform.localRotation = Quaternion.Euler(0, -90, 0);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
    }
}
