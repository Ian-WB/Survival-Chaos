using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossMovement : MonoBehaviour
{
     public float speed = 5.0f;
    private Transform player;

    public float rotationSpeed;

    private Vector3 center;
    private float centerZ;
    private float centerX;

    public bool leftOrRight_2 = true;

    public float spawnSpeed;



    void Start()
    {
        player = GameObject.Find("Player").transform;
        GameObject scenario = GameObject.FindWithTag("Scenario");
        centerX = scenario.transform.position.x;
        centerZ = scenario.transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }


void Update()
    {
        Debug.Log(leftOrRight_2);
        Vector3 pos = center;
        pos.y = transform.position.y;

        Vector3 position1 = player.position;
        Vector3 position2 = center;
        Vector3 position3 = transform.position;

        // Disregard the Y-axis component
        position1.y = 0f;
        position2.y = 0f;
        position3.y = 0f;
        float distance = Vector3.Distance(position1, position2);

        transform.LookAt(pos);

        if(Vector3.Distance(position2, position3) >= 137.2f)
        {
            Vector3 dir = center - transform.position;
            dir.y = 0;
            transform.position += dir * Time.deltaTime * spawnSpeed;
        }
        else
        {
            if(leftOrRight_2)
            {
                transform.RotateAround(pos, Vector3.up, rotationSpeed  * Time.deltaTime);
            }
            else
            {
                transform.RotateAround(pos, Vector3.up, -rotationSpeed  * Time.deltaTime);
            }
            
        

            
            Vector3 dir = player.position - transform.position;
            dir.x = 0;
            dir.z = 0;
            transform.position += dir * Time.deltaTime * speed;
            
        }
    }
}
