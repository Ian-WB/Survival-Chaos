using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    // [SerializeField] float moveSpeed = 0.01f;
    // //[SerializeField] float rotationSpeed = 1f;
    [SerializeField] private int healthPoints = 1;

    [Header("Bounds")]
    [SerializeField]
    private BoxCollider2D playerBounds;

     [Header("Shoot")]
    [SerializeField]
    private Transform shootPivot;

    [SerializeField]
    private GameObject shootPrefab;

    [SerializeField]
    private GameObject shootPrefab1;

    [Header("Delay")]
    [SerializeField]
    [Range(0f, 10f)]
    private float initialDelay = 1f;

    [SerializeField]
    [Range(0f, 10f)]
    private float spawnDelay = 1;
    

    Rigidbody2D rb;
    Vector3 moveDirection;
    public float rotationSpeed = 5f;
    public Transform referencePoint;

    private KeyCode lastPressedKey;

   

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
         if (Input.anyKeyDown)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                lastPressedKey = KeyCode.A;
                Debug.Log("Last pressed key: A");
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                lastPressedKey = KeyCode.D;
                Debug.Log("Last pressed key: D");
            }
        }      
        //Shoot();
        // Make the player look at the reference point

        //transform.rotation = Quaternion.Euler(0, transform.rotation.y, referencePoint.transform.rotation.z);
        if(Input.GetAxis("Horizontal") != 0)
        {
        Vector3 referenceDirection = referencePoint.position - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(referenceDirection, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

    

        // Update the player's position to match the reference point
        transform.position = referencePoint.position;


        // Move();

        ApplyBounds();

    }

        private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {

            Destroy(other.gameObject);

            // Update Health Points

            healthPoints--;

            // Check if Health Points is below 0 to destroy it

            if (healthPoints <= 0)
            {

                Application.Quit();
            }
        }
    }

        private void ApplyBounds()
    {
        var minX = -playerBounds.bounds.extents.x + playerBounds.offset.x + playerBounds.transform.position.x;
        var maxX = playerBounds.bounds.extents.x + playerBounds.offset.x + playerBounds.transform.position.x;

        var minY = -playerBounds.bounds.extents.y + playerBounds.offset.y + playerBounds.transform.position.y;
        var maxY = playerBounds.bounds.extents.y + playerBounds.offset.y + playerBounds.transform.position.y;

        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, minX, maxX),
            Mathf.Clamp(transform.position.y, minY, maxY),
            transform.position.z
        );
    }

    private void Awake()
    {
        InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
    }
    private void Shoot()
    {
       

        if (lastPressedKey == KeyCode.A)
        {
            Instantiate(shootPrefab, shootPivot.position, shootPivot.rotation);
        }
        else
        {
            Instantiate(shootPrefab1, shootPivot.position, shootPivot.rotation);
            //Instantiate(shootPrefab, shootPivot.position, shootPivot.rotation);
        }
        

    }
}
