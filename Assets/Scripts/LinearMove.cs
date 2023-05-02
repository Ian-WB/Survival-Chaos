using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinearMove : MonoBehaviour
{
    [SerializeField]
    private Vector3 direction;

    [SerializeField]
    private float moveSpeed = 1f;

    private void Update()
    {
        transform.Translate(direction * (moveSpeed * Time.deltaTime));
    }
}
