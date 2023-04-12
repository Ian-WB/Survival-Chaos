using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    [SerializeField]
    private float delay = 10f;


    private void Awake()
    {
        Destroy(gameObject, delay);

    }
}
