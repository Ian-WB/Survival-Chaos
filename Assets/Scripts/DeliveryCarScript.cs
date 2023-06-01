using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryCarScript : MonoBehaviour
{
    [SerializeField] float moveSpeed = 0.01f;
    //[SerializeField] float rotationSpeed = 1f;
    [SerializeField] private int healthPoints = 1;


    //Added by Luis Fernando working on the EXP System.

    //Exp Values, Added by Luis Fernando working on the EXP System.
    [SerializeField] int currentExperience, maxExperience, currentLevel;

    private void OnEnable()
    {
        //Enables EXP
        EXP.Instance.OnEXPChange += HandleEXPChange;
    }

    private void OnDisable()
    {
        //Disables EXP
        EXP.Instance.OnEXPChange -= HandleEXPChange;
    }

    [Header("Bounds")]
    [SerializeField]
    private BoxCollider2D playerBounds;

    [Header("Shoot")]
    [SerializeField]
    private Transform shootPivot;

    [SerializeField]
    private GameObject shootPrefab;

    [Header("Delay")]
    [SerializeField]
    [Range(0f, 10f)]
    private float initialDelay = 1f;

    [SerializeField]
    [Range(0f, 10f)]
    private float spawnDelay = 1f;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Move();

        ApplyBounds();

        LevelUp();
    }
    private void Move()
    {
        var h = Input.GetAxis("Horizontal");
        var v = Input.GetAxis("Vertical");


        Vector3 movementDirection = new Vector3(h, 0, 0);

        movementDirection.Normalize();
        /*if (movementDirection != Vector3.zero){
            Quaternion toRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }*/


        if (movementDirection != Vector3.zero)
        {
            transform.forward = movementDirection;
        }

        var move = new Vector3(
            0f,
            v * moveSpeed * Time.deltaTime,
            0f
        );

        transform.Translate(move);
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


        // Shoot

        Instantiate(shootPrefab, shootPivot.position, shootPivot.rotation);

    }
    private void HandleEXPChange(int newExperience)
    {
        currentExperience += newExperience;
        if (currentExperience >= maxExperience)
        {
            LevelUp();
            //this will show a popup on screen that "press space to level up, still to do
        }
    }

    private void LevelUp()
    {
        //Here we'll make it so a popup image appears that pauses the game and the player is able to choose between 3 power ups or something like that
        currentLevel += 1;

        currentExperience = 0;
        maxExperience += 50;
    }
}