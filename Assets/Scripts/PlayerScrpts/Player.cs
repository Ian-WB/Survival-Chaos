using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SurvivalChaos;

public class Player : MonoBehaviour, ISkillTarget
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

    [SerializeField]
    [Tooltip("Vertical gap between multi-shot bullets, in world units. The wider spreads " +
             "use double this. Scales with the arena - it was 3 when the arena was ten times larger.")]
    private float shotSpacing = 0.3f;

    [Header("Pooling")]
    [SerializeField]
    [Tooltip("How many of each bullet type to build before the run starts, so the opening " +
             "volleys don't create them mid-frame. Roughly (bullet lifetime / fire interval) " +
             "x shots per volley - 2s / 0.5s x 5 is 20, plus headroom for attack speed upgrades, " +
             "which shorten the interval and so raise how many are in the air at once.")]
    private int projectileWarmup = 24;

    /// <summary>
    /// Hit effects last longer than bullets but only appear when the player is
    /// struck, so a few is plenty; the pool grows on its own if it needs to.
    /// </summary>
    private const int HitEffectWarmup = 4;

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

    /// <summary>
    /// True while the ship is flipped to fire the other way. This is the single
    /// source of truth - SpaceShipPitch reads it rather than tracking its own
    /// copy, which could drift out of step with this one.
    /// </summary>
    public bool DirectionFlipped => rotate;

    [SerializeField]
    public HealthBar healthBar;

    [Header("XP")]
    [SerializeField]
    public ExpBar expBar;

    [SerializeField]
    public GameObject levelUpButton;
    public DeathMenu deathMenu;

    [SerializeField] public int currentExperience = 0, maxExperience = 40, currentLevel = 1;
    public bool tiroDuplo = false;
    public bool tiroTriplo = false;
    public bool tiroAtras = false;
    public bool sextuplo = false;
    public SkillSelect skillSelect;

    private void OnEnable()
    {
        // EXP.Instance is assigned in EXP.Awake() on a different object, and
        // Unity does not order Awake across objects - so it can legitimately be
        // null here, and on teardown EXP may already be gone.
        if (EXP.Instance != null)
        {
            EXP.Instance.OnEXPChange += HandleEXPChange;
        }
    }

    private void OnDisable()
    {
        if (EXP.Instance != null)
        {
            EXP.Instance.OnEXPChange -= HandleEXPChange;
        }
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

        // Runs before the first volley - Awake schedules Shoot with a delay.
        ObjectPool.Warm(shootPrefab, projectileWarmup);
        ObjectPool.Warm(shootPrefab1, projectileWarmup);
        ObjectPool.Warm(playerHit, HitEffectWarmup);
    }

    // Update is called once per frame
    void Update()
    {
        
        if(GameInput.ToggleDirectionReleased)
        {
            rotate = !rotate;
        }
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("enemy_Shoot"))
        {

            healthPoints--;
            ObjectPool.Spawn(playerHit, transform.position , transform.rotation);

            // Was missing, so bullet damage was invisible until death.
            healthBar.SetHealth(healthPoints);

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
             ObjectPool.Spawn(playerHit, transform.position , transform.rotation);
            
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
            ObjectPool.Spawn(shootPrefab, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));

            // tiro duplo
            if(tiroDuplo){
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro triplo
            else if(tiroTriplo){
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro para ambos os lados
            else if(tiroAtras)
            {
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                
            }
            else if(sextuplo)
            {
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
        }
        else
        {
            ObjectPool.Spawn(shootPrefab1, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));

            // tiro duplo
            if(tiroDuplo){
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro triplo
            else if(tiroTriplo){
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            // tiro para ambos os lados
            else if(tiroAtras)
            {
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position + new Vector3(0f, -shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            else if(sextuplo)
            {
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                ObjectPool.Spawn(shootPrefab1, shootPivot.position + new Vector3(0f, -shotSpacing * 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
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

    // How many shot upgrades have been taken. Drives the pattern flags below,
    // which Shoot() reads.
    private int shotUpgrades;

    /// <summary>The number of upgrades after which the pattern stops changing.</summary>
    public const int MaxShotUpgrades = 4;

    public void UpgradeShotPattern(){
        if(shotUpgrades >= MaxShotUpgrades){
            return;
        }

        shotUpgrades++;
        tiroDuplo = shotUpgrades == 1;
        tiroTriplo = shotUpgrades == 2;
        sextuplo = shotUpgrades == 3;
        tiroAtras = shotUpgrades == 4;
    }

    public void Heal(int hp){
        healthPoints += hp;
        if(healthBar.slider.maxValue < healthPoints){
            healthPoints = (int) healthBar.slider.maxValue;
        }
        healthBar.slider.value = healthPoints;
    }

    public void AddMaxHealth(int hp){
        healthPoints += hp;
        healthBar.AddMaxHealth(hp);
    }

    public void IncreaseAttackSpeed(){
        spawnDelay = spawnDelay - (0.40f * spawnDelay);
    }
}
