using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleMovement : MonoBehaviour
{
    public float speed = 5f;
    public float radius = 5f;
    public float verticalSpeed = 2f;
    private float angle = 0f;
    private float height = 0f;
    private float centerZ;
    private float centerX;
    private Vector3 center;

    void Start()
    {
        centerX = GameObject.FindWithTag("Scenario").transform.position.x;
        centerZ = GameObject.FindWithTag("Scenario").transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }

    void Update()
    {
        // Horizontal movement
        angle += Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;

        // Vertical movement
        height += Input.GetAxis("Vertical") * verticalSpeed * Time.deltaTime;
        float y = height;

        // Set position
        transform.position = center + new Vector3(x, y, z);
    }
}