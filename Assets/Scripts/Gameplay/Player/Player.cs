using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SurvivalChaos;

public class Player : MonoBehaviour, ISkillTarget
{
    public GameObject playerHit;

    [SerializeField]
    [Tooltip("Starting health. The live value is held by HealthState from Awake onwards.")]
    private int healthPoints = 1;

    /// <summary>
    /// The player's health, under the same rules as every other combatant.
    ///
    /// Only the hit that brings this to zero reports a kill, and only once - so
    /// three colliders arriving in one physics step cannot each open the death
    /// screen and stack the death sound on top of itself.
    /// </summary>
    private HealthState health;

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
        healthBar.SetMaxHealth(health.Max);
        rotate = false;
        instantiatedChild = Instantiate(childPrefab, childObject);
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

    /// <summary>
    /// The three ways of being hurt, resolved through one path.
    ///
    /// They used to be three copied blocks, which is how the Boss branch ended up
    /// as the only one without a hit effect. The only thing that actually differs
    /// between them is whether the other object is consumed by the collision.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("enemy_Shoot"))
        {
            TakeHit(spawnHitEffect: true);
        }
        else if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
            TakeHit(spawnHitEffect: true);
        }
        else if (other.CompareTag("Boss"))
        {
            TakeHit(spawnHitEffect: false);
        }
    }

    private void TakeHit(bool spawnHitEffect)
    {
        // Already dead: the death screen is up and time has stopped, but queued
        // trigger events from the same physics step still arrive. Ignoring them
        // is what stops the death sound stacking on itself.
        if (health.IsDead)
        {
            return;
        }

        bool killed = health.TakeDamage(1);

        if (spawnHitEffect)
        {
            ObjectPool.Spawn(playerHit, transform.position, transform.rotation);
        }

        // Was missing, so bullet damage was invisible until death.
        healthBar.SetHealth(health.Current);
        PlayDamageSound(killed);

        if (killed)
        {
            deathMenu.ShowDeathMenu();
        }
    }

    /// <summary>The hit sound, or the death sound when that hit was the last one.</summary>
    private void PlayDamageSound(bool killed)
    {
        GameSounds sounds = GameSounds.Instance;
        if (sounds == null)
        {
            return;
        }

        GameSounds.Play(killed ? sounds.PlayerDeath : sounds.PlayerHit);
    }

    private void Awake()
    {
        health = new HealthState(healthPoints);
        InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
    }
    /// <summary>
    /// Where each upgrade stage puts its shots, as vertical offsets in multiples
    /// of <see cref="shotSpacing"/>. Indexed by how many shot upgrades have been
    /// taken, so stage 0 is the opening single shot.
    ///
    /// A table rather than a branch per stage. The same five patterns used to be
    /// written out twice - once per direction of travel - as about seventy lines
    /// of near-identical Spawn calls, and that is how the sextuple stage came to
    /// fire five shots with nobody noticing.
    ///
    /// The sextuple row is offset by half a step so its six sit symmetrically
    /// about the pivot. The stages either side put a shot dead centre and pair
    /// the rest around it, which only works for odd counts.
    /// </summary>
    private static readonly float[][] ForwardPattern =
    {
        new[] { 0f },
        new[] { 0f, 1f },
        new[] { 0f, 1f, -1f },
        new[] { 0.5f, -0.5f, 1.5f, -1.5f, 2.5f, -2.5f },
        new[] { 0f, 1f, -1f, 2f, -2f }
    };

    /// <summary>
    /// The same, for shots sent the other way round the ring. Only the last
    /// stage fires backwards - that is what it buys.
    /// </summary>
    private static readonly float[][] BackwardPattern =
    {
        new float[0],
        new float[0],
        new float[0],
        new float[0],
        new[] { 1f, -1f, 2f, -2f }
    };

    private void Shoot()
    {
        // Once per volley, not once per bullet. The widest pattern fires nine at
        // the same instant and should still read as one shot.
        if (GameSounds.Instance != null)
        {
            GameSounds.Play(GameSounds.Instance.PlayerShot);
        }

        int stage = Mathf.Clamp(shotUpgrades, 0, ForwardPattern.Length - 1);

        // Which prefab travels with the ship is the whole difference between the
        // two directions: they carry opposite angular speeds, so the one that
        // goes forward while flipped is the one that goes backward otherwise.
        GameObject forward = rotate ? shootPrefab : shootPrefab1;
        GameObject backward = rotate ? shootPrefab1 : shootPrefab;

        FireLine(forward, ForwardPattern[stage]);
        FireLine(backward, BackwardPattern[stage]);
    }

    /// <summary>Spawns one bullet per offset, spaced up the pivot.</summary>
    private void FireLine(GameObject prefab, float[] offsets)
    {
        if (prefab == null || shootPivot == null)
        {
            return;
        }

        foreach (float offset in offsets)
        {
            ObjectPool.Spawn(
                prefab,
                shootPivot.position + new Vector3(0f, offset * shotSpacing, 0f),
                Quaternion.Euler(0f, 0f, 90f));
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
        if (GameSounds.Instance != null)
        {
            GameSounds.Play(GameSounds.Instance.LevelUp);
        }

        // Counted before the offer goes out, so what PickSkill sees is the level
        // just reached rather than the one being left. The health cadence keys
        // off it, and off-by-one there is the difference between health on the
        // even levels and health on the odd ones.
        currentLevel += 1;

        //Here we'll make it so a popup image appears that pauses the game and the player is able to choose between 3 power ups or something like that
        skillSelect.PickSkill();

        currentExperience = 0;
        expBar.setCurrentExp(currentExperience);
        maxExperience += 35;
        expBar.setMaxExp(maxExperience);
    }

    // How many shot upgrades have been taken. Drives the pattern flags below,
    // which Shoot() reads.
    [SerializeField]
    [Tooltip("Shot pattern stage: 0 single, 1 double, 2 triple, 3 sextuple, 4 plus rear shots. " +
             "Serialized so a stage can be tried from here without playing up to it - it " +
             "replaced four separate bools that had to be kept mutually exclusive by hand.")]
    private int shotUpgrades;

    /// <summary>The number of upgrades after which the pattern stops changing.</summary>
    public const int MaxShotUpgrades = 4;

    public void UpgradeShotPattern(){
        if(shotUpgrades >= MaxShotUpgrades){
            return;
        }

        shotUpgrades++;
    }

    public void Heal(int hp){
        health.Heal(hp);
        healthBar.SetHealth(health.Current);
    }

    public void AddMaxHealth(int hp){
        health.RaiseMax(hp);
        healthBar.AddMaxHealth(hp);
    }

    [Header("Attack speed")]
    [SerializeField]
    [Range(0.05f, 0.6f)]
    [Tooltip("Fraction taken off the gap between volleys per Attack Speed pick. At 0.4 each " +
             "pick is 40% faster than the last, which compounds: three picks nearly quintuple " +
             "the fire rate. Lower it to spread the gain more evenly across the picks.")]
    private float attackSpeedStep = 0.40f;

    [SerializeField]
    [Range(MinShotInterval, 1f)]
    [Tooltip("The fastest the player may ever fire, in seconds between volleys. This is the " +
             "balance cap - the one to move when the gun feels too strong. It is separate from " +
             "MinShotInterval, which only exists to stop the game hanging.")]
    private float shotIntervalFloor = 0.15f;

    /// <summary>
    /// The shortest gap between volleys the game will tolerate at all.
    ///
    /// IncreaseAttackSpeed multiplies rather than subtracts, so the arithmetic
    /// never reaches zero on its own - but it approaches it, and InvokeRepeating
    /// at a near-zero rate is a hang rather than a fast gun.
    ///
    /// This is a safety limit, not a balance one. Balance lives in
    /// shotIntervalFloor above, which is authored per scene and sits well
    /// clear of this. Keeping the two apart is the point: tuning the gun should
    /// never be able to walk the game into a freeze, and raising maxPicks on the
    /// AttackSpeed asset should stay a design decision rather than a crash.
    /// </summary>
    public const float MinShotInterval = 0.05f;

    /// <summary>
    /// Speeds the gun up by one pick, and stops where the cap says to.
    ///
    /// The compounding is what needed bounding. Each pick takes a fraction off
    /// whatever the interval currently is, so the picks multiply rather than
    /// add - from the scene's 0.5s start, three picks at 0.4 reach 0.108s, which
    /// is nine volleys a second and the widest shot pattern firing five bullets
    /// in each. The only thing that used to stand in the way of that was the
    /// hang guard.
    /// </summary>
    public void IncreaseAttackSpeed(){
        // Clamped up to the safety limit, so a floor authored below it in the
        // Inspector cannot reintroduce the hang this is all guarding against.
        float floor = Mathf.Max(MinShotInterval, shotIntervalFloor);

        spawnDelay = Mathf.Max(floor, spawnDelay - (attackSpeedStep * spawnDelay));
        CancelInvoke(nameof(Shoot));
        InvokeRepeating(nameof(Shoot), spawnDelay, spawnDelay);
    }
}
