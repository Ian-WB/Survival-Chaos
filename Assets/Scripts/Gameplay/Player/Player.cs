using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    public class Player : MonoBehaviour, ISkillTarget
    {
        [SerializeField]
        private GameObject playerHit;

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
                 "use double this. Scales with the arena - it was 0.3 when the arena was a " +
                 "tenth the size.")]
        private float shotSpacing = 3f;

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
        private GameObject childPrefab;

        [SerializeField]
        private Transform childObject;

        private GameObject instantiatedChild;

        private bool rotate;

        /// <summary>
        /// True while the ship is flipped to fire the other way. This is the single
        /// source of truth - SpaceShipPitch reads it rather than tracking its own
        /// copy, which could drift out of step with this one.
        /// </summary>
        public bool DirectionFlipped => rotate;

        [SerializeField]
        private HealthBar healthBar;

        [Header("XP")]
        [SerializeField]
        private ExpBar expBar;

        [SerializeField]
        private DeathMenu deathMenu;

        [SerializeField] public int currentExperience = 0, maxExperience = 40, currentLevel = 1;

        [SerializeField]
        private SkillSelect skillSelect;

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
                // Despawned rather than destroyed: enemies are pooled now, and
                // destroying one leaves a dead entry in its bucket for the pool to
                // trip over and discard later. Ramming still kills it silently - no
                // reward, no explosion - which is the existing behaviour.
                ObjectPool.Despawn(other.gameObject);
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

            // Captured before any pick can move it, so each Attack Speed pick is
            // measured against the gun's starting rate rather than against whatever
            // the previous pick left behind.
            baseSpawnDelay = spawnDelay;

            InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
        }
        /// <summary>
        /// Where each upgrade stage puts its shots, as vertical offsets in multiples
        /// of <see cref="shotSpacing"/>. Indexed by how many shot upgrades have been
        /// taken, so stage 0 is the opening single shot.
        ///
        /// A table rather than a branch per stage. The same patterns used to be
        /// written out twice - once per direction of travel - as about seventy lines
        /// of near-identical Spawn calls, and that is how the sextuple stage came to
        /// fire five shots with nobody noticing.
        ///
        /// The sextuple row is offset by half a step so its six sit symmetrically
        /// about the pivot. The stages either side put a shot dead centre and pair
        /// the rest around it, which only works for odd counts.
        ///
        /// There used to be a fifth row, Back Shot, which fired five forward and
        /// four the other way round the ring. It is gone: it was the one upgrade
        /// that changed what the gun *is* rather than how much of it there is, and
        /// with the progression stretched over a run twice as long there are now
        /// nineteen other picks doing the stretching. Nothing fires backwards, so
        /// the backward table and the second FireLine went with it.
        /// </summary>
        private static readonly float[][] ForwardPattern =
        {
            new[] { 0f },
            new[] { 0f, 1f },
            new[] { 0f, 1f, -1f },
            new[] { 0.5f, -0.5f, 1.5f, -1.5f, 2.5f, -2.5f }
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
            // Still needed with Back Shot gone - the flip decides which of the two
            // counts as forward, even though only forward is ever fired now.
            GameObject forward = rotate ? shootPrefab : shootPrefab1;

            FireLine(forward, ForwardPattern[stage]);
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
            RunStats.RecordLevel(currentLevel);

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
        [Range(0.02f, 0.5f)]
        [Tooltip("Fire rate added per Attack Speed pick, as a fraction of the starting rate. " +
                 "0.1 is +10% a pick, and every pick is worth the same: eight of them is +80%, " +
                 "not eight compounding steps that are each smaller than the last.")]
        private float attackSpeedStep = 0.10f;

        [SerializeField]
        [Range(MinShotInterval, 1f)]
        [Tooltip("The fastest the player may ever fire, in seconds between volleys. This is the " +
                 "balance cap - the one to move when the gun feels too strong. It is separate from " +
                 "MinShotInterval, which only exists to stop the game hanging.")]
        private float shotIntervalFloor = 0.15f;

        [Header("Move speed")]
        [SerializeField]
        [Range(0.02f, 0.5f)]
        [Tooltip("Movement added per Move Speed pick, as a fraction of the starting speed. " +
                 "Additive like attack speed above, so every pick is worth the same. Raises " +
                 "orbiting and climbing alike, and applies to the camera as well - it has to, " +
                 "or it falls behind the ship.")]
        private float moveSpeedStep = 0.10f;

        /// <summary>
        /// The shortest gap between volleys the game will tolerate at all.
        ///
        /// The rate this divides by only ever grows, so the interval approaches zero
        /// without reaching it - and InvokeRepeating at a near-zero rate is a hang
        /// rather than a fast gun.
        ///
        /// This is a safety limit, not a balance one. Balance lives in
        /// shotIntervalFloor above, which is authored per scene and sits well
        /// clear of this. Keeping the two apart is the point: tuning the gun should
        /// never be able to walk the game into a freeze, and raising maxPicks on the
        /// AttackSpeed asset should stay a design decision rather than a crash.
        /// </summary>
        public const float MinShotInterval = 0.05f;

        /// <summary>
        /// The gap between volleys before any Attack Speed picks, captured once so
        /// each pick can be measured against it rather than against the last one.
        /// </summary>
        private float baseSpawnDelay;

        /// <summary>How many Attack Speed picks have been taken.</summary>
        private int attackSpeedPicks;

        /// <summary>
        /// Speeds the gun up by one pick, and stops where the cap says to.
        ///
        /// Additive in *rate*, which is the thing a player feels. Each pick adds a
        /// fixed slice of the starting rate, so the eighth is worth exactly what the
        /// first was: at 0.1 a pick, from 0.5s between volleys, eight picks reach
        /// 0.5 / 1.8 = 0.278s. Two volleys a second becomes 3.6.
        ///
        /// It used to subtract a fraction of the *current* interval, which compounds
        /// the wrong way round: from the same start, three picks at 0.4 reached
        /// 0.108s and hit the floor on the second, so the third pick bought 0.03s
        /// and anything beyond it bought nothing at all. Eight picks of that would
        /// have been five picks of nothing.
        /// </summary>
        public void IncreaseAttackSpeed(){
            // Clamped up to the safety limit, so a floor authored below it in the
            // Inspector cannot reintroduce the hang this is all guarding against.
            float floor = Mathf.Max(MinShotInterval, shotIntervalFloor);

            attackSpeedPicks++;
            float rate = 1f + (attackSpeedStep * attackSpeedPicks);

            spawnDelay = Mathf.Max(floor, baseSpawnDelay / rate);
            CancelInvoke(nameof(Shoot));
            InvokeRepeating(nameof(Shoot), spawnDelay, spawnDelay);
        }

        /// <summary>
        /// Speeds the player up by one pick, orbiting and climbing alike.
        ///
        /// Handed to <see cref="PlayerMovement"/> rather than applied here, because
        /// the ship is not the only thing that has to speed up: the Main Camera runs
        /// its own copy of that component at the same speed, and that identity is
        /// the whole of how the camera keeps station. Raise one without the other
        /// and the player pulls away from the frame.
        /// </summary>
        public void IncreaseMoveSpeed()
        {
            PlayerMovement.AddSpeedBonus(moveSpeedStep);
        }
    }
}
