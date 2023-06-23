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
    private float initialDelay = 1f;
  

    [SerializeField]
    [Range(0f, 60f)]
    private float spawnDelay = 1f;

    [Header("Range")]
    [SerializeField]
    private Range rangeX;

    [SerializeField]
    private Range rangeY;

    [SerializeField]
    private float spawnIncreaseDelay = 1f;

    private bool hasConditionOccurred = false;


    [SerializeField]
    private float spawnRateIncrease = 0.1f;

    private void Start()
    {
        StartCoroutine(SpawnWithDelay());
        StartCoroutine(SpawnDelay());
    }


    private IEnumerator SpawnDelay()
    {
        yield return new WaitForSeconds(spawnIncreaseDelay);

        while (true)
        {
            spawnDelay = spawnDelay * spawnRateIncrease;

            spawnDelay = Mathf.Max(spawnDelay, 0.1f); // Set a minimum spawn delay

            yield return new WaitForSeconds(spawnIncreaseDelay);
        }
    }

    private IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            Spawn();
            
            
            

            yield return new WaitForSeconds(spawnDelay);
        }
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
