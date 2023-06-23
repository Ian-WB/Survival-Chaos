using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossSpaceShip : MonoBehaviour
{
    public GameObject EnemyShip;
    // Start is called before the first frame update
    
    // Update is called once per frame
    void Update()
    {
        if (EnemyShip.GetComponent<BossMovement>().leftOrRight_2)
        {
            transform.localRotation = Quaternion.Euler(0, -90, 0);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
    }
}
