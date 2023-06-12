using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 5.0f;
    private Transform player;

    public float rotationSpeed;

    private Vector3 center;
    private float centerZ;
    private float centerX;

    public bool leftOrRight = true;



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
        Vector3 pos = center;
        pos.y = transform.position.y;
        if(leftOrRight)
        {
            transform.RotateAround(pos, Vector3.up, rotationSpeed  * Time.deltaTime);
        }
        else
        {
            transform.RotateAround(pos, Vector3.up, -rotationSpeed  * Time.deltaTime);
        }
        transform.LookAt(pos);
        Vector3 dir = player.position - transform.position;
        dir.x = 0;
        dir.z = 0;


        transform.position += dir * Time.deltaTime * speed;
    }

    
}