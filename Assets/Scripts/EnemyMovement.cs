using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 5f;
    public float radius = 5f;
    //public Transform player;
    private Vector3 center;
    private float angle = 0f;

    void Start()
    {
        center = GameObject.FindWithTag("Scenario").transform.position;
    }

    void Update()
    {
        Vector3 playerPos = GameObject.Find("Player").transform.position;
        playerPos.y = transform.position.y;
        transform.LookAt(playerPos);

        angle += speed * Time.deltaTime;
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;
        transform.position = center + new Vector3(x, 0f, z);
    }
}