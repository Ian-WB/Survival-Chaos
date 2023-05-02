using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float rotationSpeed = 5f;

    public float followSpeed = 1f;
    public Transform player;
    private float centerZ;
    private float centerX;
    private Vector3 center;

    public float enemyDistance = 1f;


    void Start()
    {
        player = GameObject.Find("Player").transform;
        centerX = GameObject.FindWithTag("Scenario").transform.position.x;
        centerZ = GameObject.FindWithTag("Scenario").transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }

    void Update()
    {
        followPlayer();

        transform.RotateAround(center, Vector3.up, -rotationSpeed * Time.deltaTime);
    }

    void followPlayer()
    {
        if(Vector3.Distance(transform.position, player.position) <= 20){
        Vector3 dir = player.position - transform.position;
        transform.position = transform.position + dir.normalized * followSpeed * Time.deltaTime;
        }
    }
}