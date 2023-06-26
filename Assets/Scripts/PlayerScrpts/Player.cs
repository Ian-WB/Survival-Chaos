using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public GameObject playerHit;
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
    
    [SerializeField]
    public GameObject childPrefab;

    [SerializeField]
    public Transform childObject;
    
    private GameObject instantiatedChild;

    private bool rotate;

    [SerializeField]
    public HealthBar healthBar;

    [Header("XP")]
    [SerializeField]
    public ExpBar expBar;

    [SerializeField]
    public GameObject levelUpButton;
    public DeathMenu deathMenu;

    [SerializeField] int currentExperience = 0, maxExperience = 40, currentLevel = 1;
    public bool tiroDuplo = false;
    public bool tiroTriplo = false;
    public bool tiroAtras = false;
    public SkillSelect skillSelect;

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

    // Start is called before the first frame update
    void Start()
    {   
        healthBar.SetMaxHealth(healthPoints);
        rotate = false;
        GameObject instantiatedChild = Instantiate(childPrefab, childObject);
        //Rigidbody childRigidbody = instantiatedChild.GetComponent<Rigidbody>();
        //instantiatedChild.transform.localPosition = prefabOffset;
        expBar.setMaxExp(maxExperience);
        expBar.setCurrentExp(currentExperience);
    }

    // Update is called once per frame
    void Update()
    {
        
        if(Input.GetKeyUp(KeyCode.LeftShift))
        {
            rotate = !rotate;
            Debug.Log("Variable state: " + rotate);
        }
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("enemy_Shoot"))
        {
             
            healthPoints--;
            Instantiate (playerHit , transform.position , transform.rotation);
           

            if (healthPoints <= 0)
            {

                deathMenu.ShowDeathMenu();
            }
        }
        if (other.CompareTag("Enemy"))
        {

            Destroy(other.gameObject);

            // Update Health Points
            
            healthPoints--;
             Instantiate (playerHit , transform.position , transform.rotation);
            
            healthBar.SetHealth(healthPoints);

            // Check if Health Points is below 0 to destroy it

            if (healthPoints <= 0)
            {

                deathMenu.ShowDeathMenu();
            }
        } else if (other.CompareTag("Boss"))
        {


            // Update Health Points

            healthPoints--;
            healthBar.SetHealth(healthPoints);

            // Check if Health Points is below 0 to destroy it

            if (healthPoints <= 0)
            {

                deathMenu.ShowDeathMenu();
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

            // tiro duplo
            if(tiroDuplo){
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro triplo
            else if(tiroTriplo){
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro para ambos os lados
            else if(tiroAtras)
            {
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
        }
        else
        {
            Instantiate(shootPrefab1, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));

            // tiro duplo
            if(tiroDuplo){
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro triplo
            else if(tiroTriplo){
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro para ambos os lados
            else if(tiroAtras)
            {
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Instantiate(shootPrefab1, shootPivot.position + new Vector3(0f, -1.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
        }
    }

    private void HandleEXPChange(int newExperience)
    {
        currentExperience += newExperience;
        expBar.setCurrentExp(currentExperience);
        if (currentExperience >= maxExperience)
        {
            LevelUp();
            //this will show a popup on screen that "press space to level up, still to do
        }
    }

    private void LevelUp()
    {
        //Here we'll make it so a popup image appears that pauses the game and the player is able to choose between 3 power ups or something like that
        skillSelect.PickSkill();
        currentLevel += 1;
        
        currentExperience = 0;
        expBar.setCurrentExp(currentExperience);
        maxExperience += 35;
        expBar.setMaxExp(maxExperience);
    }

    public void addHp(int hp){
        healthPoints += hp;
        if(healthBar.slider.maxValue < healthPoints){
            healthPoints = (int) healthBar.slider.maxValue;
        }
        healthBar.slider.value = healthPoints;
    }
    public void addMaxHp(int hp){
        healthPoints += hp;
        healthBar.AddMaxHealth(hp);
    }

    public void addAttackSpeed(){
        spawnDelay = spawnDelay - (0.40f * spawnDelay);
    }
}
