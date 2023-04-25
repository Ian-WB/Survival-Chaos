using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMovement : MonoBehaviour
{
    public CircleMovement player;
    [HideInInspector]
    public float speed;
    public float distance = 5f;
    private float angle = 0f;
    private float centerZ;
    private float centerX;
    private Vector3 center;

    void Start()
    {
        distance += player.radius;
        speed = player.speed;
        centerX = GameObject.Find("Scenary1").transform.position.x;
        centerZ = GameObject.Find("Scenary1").transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }

    void Update()
    {
        // Horizontal movement
        angle += Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float x = Mathf.Cos(angle) * distance;
        float z = Mathf.Sin(angle) * distance;

        // Set position
        transform.position = center + new Vector3(x, transform.position.y, z);
    }
}