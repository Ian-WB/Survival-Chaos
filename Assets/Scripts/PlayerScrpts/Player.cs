using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
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

    public GameObject childPrefab;
    public Vector3 prefabOffset;
    public Transform childObject;
    private GameObject instantiatedChild;
    private KeyCode lastPressedKey;

    private bool rotate;

    // Start is called before the first frame update
    void Start()
    {   
        rotate = false;
        GameObject instantiatedChild = Instantiate(childPrefab, childObject);
        //Rigidbody childRigidbody = instantiatedChild.GetComponent<Rigidbody>();
        //instantiatedChild.transform.localPosition = prefabOffset;
    }

    // Update is called once per frame
    void Update()
    {
        
        if (Input.anyKeyDown)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                lastPressedKey = KeyCode.LeftArrow;
                //Debug.Log("Last pressed key: LeftArrow");
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                lastPressedKey = KeyCode.RightArrow;
                //Debug.Log("Last pressed key: RightArrow");
            }
            
        }      
        //Shoot();
        // Make the player look at the reference point
        
        // Move();
        if(Input.GetKeyUp(KeyCode.LeftShift))
        {
            rotate = !rotate;
            Debug.Log("Variable state: " + rotate);
        }
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

                SceneManager.LoadScene( SceneManager.GetActiveScene().name );
            }
        }
    }

    private void Awake()
    {
        InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
    }
    private void Shoot()
    {
       

        if (rotate)
        {
            Instantiate(shootPrefab, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));
            Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            
        }
        
        else 
        {
            Instantiate(shootPrefab1, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));
            Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            
        }
        

    }
}
