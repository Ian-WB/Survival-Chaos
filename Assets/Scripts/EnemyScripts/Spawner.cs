using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    private float number;

    public float initial_value;
    public float ratio;
    public float interval;

    [Header("Prefab")]
    [SerializeField]
    private GameObject spawnPrefab;

    [Header("Delay")]
    [SerializeField]
    [Range(0f, 60f)]
    private float initialDelay = 1f;
    private float initialDelay_1 = 1f;

    [SerializeField]
    [Range(0f, 60f)]
    private float spawnDelay = 1f;
    private float spawnDelay_1 = 1f;

    [Header("Range")]
    [SerializeField]
    private Range rangeX;

    [SerializeField]
    private Range rangeY;


    private void Awake()
    {
        InvokeRepeating(nameof(Spawn), initialDelay, spawnDelay);

        
    
    }

    void Update()
    {
    
    }





    private void Spawn()
    {
        var randomX = Random.Range(rangeX.min, rangeX.max);
        var randomY = Random.Range(rangeY.min, rangeY.max);

        var position = new Vector3(
            transform.position.x + randomX,
            transform.position.y + randomY,
            transform.position.z
        );

        Instantiate(spawnPrefab, position, transform.rotation);
    }
}
