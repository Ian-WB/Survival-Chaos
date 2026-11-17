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
                 "use double this. Scales with the ship and its rounds - the scene's 0.4 halved " +
                 "to 0.2 when both were halved in September 2026, and it was ten times larger " +
                 "again when the arena was.")]
        private float shotSpacing = 0.2f;

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
        [Range(0.5f, 2f)]
        [Tooltip("How large the ship's model is drawn, against the size it was authored at. " +
                 "1.35 from 16 November 2026, when the upgrades began adding parts too small " +
                 "to read at 1; 2 later that day, on 1440p captures where 1.35 drew the ship " +
                 "97 x 60 px. The hit box does not follow it: see hitBoxModelScale.")]
        private float modelScale = 2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How much of the model's growth the hit box takes. 0 leaves it as authored, 1 " +
                 "grows it as much as the model. Half: a bit bigger (Ian, 16 November 2026), " +
                 "without making the ship a third easier to hit for looking a third larger.")]
        private float hitBoxGrowth = 0.5f;

        [SerializeField]
        [Range(0.5f, 2f)]
        [Tooltip("The model scale the hit box is sized for. The box follows this, not modelScale, " +
                 "so drawing the ship larger for readability leaves dodging exactly as it was " +
                 "(16 November 2026: the box Ian played at 1.35).")]
        private float hitBoxModelScale = 1.35f;

        [SerializeField]
        private Transform childObject;

        private GameObject instantiatedChild;

        private ShipRig shipRig;
        private ShipParts shipParts;
        private ShipThrusters thrusters;
        private ShipMuzzles muzzles;
        private HitFlash hullFlash;

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

        [Header("Death")]
        [SerializeField]
        [Range(0f, 3f)]
        [Tooltip("Real seconds between the killing hit and the Ship Lost card: the dial for how long " +
                 "the ship takes to break up. 0 puts the card up on the hit, as it was until " +
                 "30 September 2026.")]
        private float deathBeatSeconds = 1.2f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("The game's speed through that beat. A quarter lets the shot that killed the ship " +
                 "be seen landing.")]
        private float deathBeatSpeed = 0.25f;

        [SerializeField]
        [Tooltip("What the ship breaks up into. The hit effect plays as well.")]
        private GameObject deathExplosion;

        [Header("Hit reaction")]
        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("What the hull lights up as when the ship loses a hit point. White, where an " +
                 "enemy's is red: red on the ring means something of theirs was hit. Kept near 1 " +
                 "at a half: at 1.2 the whole hull clipped to a white blob with no shape left in it.")]
        private Color hitFlashColor = new Color(0.5f, 0.5f, 0.48f);

        [SerializeField]
        [Range(0.01f, 0.5f)]
        [Tooltip("Seconds the hull stays lit.")]
        private float hitFlashSeconds = 0.1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How hard a hit knocks the model away from what hit it, in world units a second. " +
                 "The ship springs back and its hit box does not move. 0 for none.")]
        private float hitKnock = 4.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How hard the same hit rocks the ship, in degrees a second.")]
        private float hitTwist = 420f;

        [SerializeField]
        [Range(0f, 0.2f)]
        [Tooltip("Real seconds the game all but stops for when the ship loses a hit point: the " +
                 "hit-stop the Leviathan's emplacements have, shorter. 0 for none.")]
        private float hitFreezeSeconds = 0.05f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How hard a hit the ship survives shakes the view, 0 to 1. The shake is the square " +
                 "of this, so 0.5 is a quarter of the full 0.9 degrees. 0 for none.")]
        private float hitShake = 0.55f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How hard losing the ship shakes the view.")]
        private float deathShake = 1f;

        [Header("Recoil")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("How hard a volley kicks the model back from its nose, in world units a second. " +
                 "0 for none.")]
        private float recoil = 2f;

        [SerializeField]
        [Tooltip("Degrees a second a volley lifts the nose by.")]
        private float recoilTwist = 70f;

        [SerializeField] public int currentExperience = 0, maxExperience = 40, currentLevel = 1;

        [SerializeField, Min(0)]
        [Tooltip("How much more each level costs than the one before. Max Experience is the " +
                 "first level's cost.")]
        private int levelCostIncrease = 35;

        [SerializeField, Min(0f)]
        [Tooltip("Scales every enemy's Experience Reward, rounded to whole points. A full run " +
                 "reached about 20 level-ups at 1, 29 at 2 and 35 at 3 while each level-up still " +
                 "dropped the experience past its threshold; carrying it over adds about one. " +
                 "Those counts are from the old curve: since 9 November 2026 the scene's curve " +
                 "(95, rising by 180) gives about 17 level-ups a run at 4.5, against 22 upgrade " +
                 "picks in the pool. A level-up with the pool spent leaves a piece of salvage " +
                 "instead.")]
        private float experienceMultiplier = 1f;

        [SerializeField]
        private SkillSelect skillSelect;

        /// <summary>
        /// The EXP this player is subscribed to, so binding can be tried more
        /// than once without subscribing twice, and undone against the object
        /// it was made with rather than whatever EXP.Instance says by then.
        /// </summary>
        private EXP boundExperience;

        private void OnEnable()
        {
            BindExperience();
        }

        private void OnDisable()
        {
            // The object it was made with, even one already destroyed on
            // teardown: taking a handler off a C# event needs nothing from Unity.
            if ((object)boundExperience != null)
            {
                boundExperience.OnEXPChange -= HandleEXPChange;
                boundExperience = null;
            }
        }

        /// <summary>
        /// Subscribes to the scene's EXP, once.
        ///
        /// EXP wakes first now (its DefaultExecutionOrder), so the call from
        /// OnEnable finds it. Start calls this again anyway, because the failure
        /// it guards against is silent and total: a subscription that missed
        /// EXP.Awake was never retried, and every kill of the run earned nothing.
        /// A second call with the same EXP does nothing; a different one is
        /// swapped in, not added.
        /// </summary>
        private void BindExperience()
        {
            EXP current = EXP.Instance;

            if (current == boundExperience)
            {
                return;
            }

            if ((object)boundExperience != null)
            {
                boundExperience.OnEXPChange -= HandleEXPChange;
            }

            boundExperience = current;

            if (current != null)
            {
                current.OnEXPChange += HandleEXPChange;
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            BindExperience();

            healthBar.SetMaxHealth(health.Max);
            rotate = false;
            instantiatedChild = Instantiate(childPrefab, childObject);
            instantiatedChild.transform.localScale *= modelScale;
            GrowHitBox();

            // What the hit and the gun act on: the pose of the model's mount,
            // the hull's own flash, and the flares it carries. Each is looked
            // for and none is required, so a ship missing one still flies.
            shipRig = childObject != null ? ShipRig.On(childObject.gameObject) : null;
            thrusters = instantiatedChild.GetComponentInChildren<ShipThrusters>();
            shipParts = instantiatedChild.GetComponentInChildren<ShipParts>();

            // The guns, on the model the flares are on: its space is the one
            // the muzzles are measured in, and its mesh and material are the
            // ones every other glow on the ship is made of.
            if (thrusters != null)
            {
                muzzles = thrusters.gameObject.AddComponent<ShipMuzzles>();
                muzzles.Build(thrusters.GlowMesh, thrusters.FlareMaterial);
                muzzles.SetStage(shotUpgrades);
            }
            hullFlash = HitFlash.On(instantiatedChild);

            // A shot stage set in the Inspector is on the ship from the start.
            Fit(ShipUpgrade.Hydra, shotUpgrades);

            if (hullFlash != null)
            {
                hullFlash.Set(hitFlashColor, hitFlashSeconds);
            }
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
            if (PauseMenu.GameIsPaused || RunOutcome.Decided || Time.timeScale <= 0f)
            {
                return;
            }

            // The guns heat towards the next volley, and stay cold while
            // nothing is going to fire.
            if (muzzles != null)
            {
                muzzles.Charge(
                    ArenaIntroDirector.IsPlaying ? float.PositiveInfinity : lastVolleyAt + spawnDelay - Time.time,
                    spawnDelay);
            }

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
            // Nothing in this method should happen at all while phased - not the
            // damage, and not the enemy being consumed by the collision either.
            // Invincibility frames that still ate the enemy would hand the player
            // a silent, rewardless kill for every ship they dashed through, which
            // is a way of removing enemies from the game rather than of surviving
            // them.
            //
            // Nor once the run is over, for the reason in TakeHit.
            if (Phased || RunOutcome.Decided)
            {
                return;
            }

            if (other.CompareTag("enemy_Shoot"))
            {
                // A round says what it costs: 2 for a Fighter's or a Heavy's since
                // 10 November 2026, 1 for the Leviathan's and for anything unmarked.
                int cost = other.TryGetComponent(out ShootScript hit) ? hit.Damage : 1;
                TakeHit(spawnHitEffect: true, DamageSource.OfRound(other.gameObject), cost,
                    other.transform.position);

                // A torpedo goes off when it hits. Plain rounds fly on through,
                // which is harmless for a round that never comes back; a torpedo
                // turns round, and one left flying would hit again on its next
                // pass.
                if (other.TryGetComponent(out ShootScript round) && round.IsTorpedo)
                {
                    ObjectPool.Despawn(other.gameObject);
                }
            }
            else if (other.CompareTag("Enemy"))
            {
                // Despawned rather than destroyed: enemies are pooled now, and
                // destroying one leaves a dead entry in its bucket for the pool to
                // trip over and discard later. Ramming pays nothing - no
                // experience, no kill, no salvage - but since 16 November 2026
                // the ship goes up where it was hit and is not simply gone.
                // Named, and placed, before it goes back to the pool.
                string rammed = DamageSource.OfRam(other.gameObject);
                Vector3 where = other.transform.position;
                EnemyBase enemy = other.GetComponentInParent<EnemyBase>();

                if (enemy != null)
                {
                    enemy.Ram();
                }
                else
                {
                    ObjectPool.Despawn(other.gameObject);
                }

                TakeHit(spawnHitEffect: true, rammed, 1, where);
            }
            else if (other.CompareTag("Boss"))
            {
                TakeHit(spawnHitEffect: false, DamageSource.OfBoss(other.gameObject), 1,
                    other.bounds.ClosestPoint(transform.position));
            }
        }

        /// <summary>
        /// Ignores incoming damage. Set by the debug menu and nothing else.
        ///
        /// Deliberately not serialized: a field left ticked in a prefab is exactly
        /// the sort of thing that ships. It resets with the scene because it lives
        /// on this component, so there is no way to leave it on for a real run.
        /// </summary>
        public bool Invulnerable { get; set; }

        /// <summary>
        /// The hull just lost points: where the hit came from, or null for a
        /// hit with no place (the lance). The HUD's screen-edge hit flare
        /// listens (17 November 2026 redesign).
        /// </summary>
        public static event System.Action<Vector3?> Hurt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHurt() => Hurt = null;

        /// <summary>
        /// What the last hit that cost a hit point came from, in the card's words
        /// (see <see cref="DamageSource"/>). Null before the first. The playtest
        /// bot reads it to say what hit it.
        /// </summary>
        public string LastHitBy { get; private set; }

        [SerializeField]
        [Tooltip("The dash, whose burst carries invincibility. Found on this object when left empty.")]
        private PlayerDash dash;

        /// <summary>
        /// True while nothing can touch the player - the dash's own frames, or
        /// the debug toggle.
        ///
        /// Two sources rather than one settable flag, because a dash that wrote
        /// <see cref="Invulnerable"/> would switch the debug toggle off every
        /// time it ended, and the tester would be left wondering which of the two
        /// things they were watching had lied to them.
        /// </summary>
        private bool Phased => Invulnerable || (dash != null && dash.Invincible);

        /// <summary>Current hit points, for the debug menu's readout.</summary>
        public int CurrentHealth => health != null ? health.Current : 0;

        /// <summary>Maximum hit points, for the debug menu's readout.</summary>
        public int MaxHealth => health != null ? health.Max : 0;

        /// <summary>
        /// Damage from something that has no collider to enter - the boss's lance,
        /// which is an arc test against the ring rather than a trigger volume.
        ///
        /// This mirrors what <see cref="OnTriggerEnter"/> does for a projectile
        /// rather than calling straight through to the damage: the dash still
        /// protects, and the hit effect still spawns. Anything that skipped those
        /// would be a second, quieter set of rules for being hit.
        /// </summary>
        public void TakeBeamHit()
        {
            if (Phased)
            {
                return;
            }

            TakeHit(spawnHitEffect: true, DamageSource.LeviathanLance);
        }

        /// <param name="from">
        /// Where the thing that hit the ship is, for the ship to be knocked
        /// away from. Null for a hit with no place, the lance: the ship is
        /// knocked back off its nose.
        /// </param>
        private void TakeHit(bool spawnHitEffect, string source, int damage = 1, Vector3? from = null)
        {
            // The run is over, won or lost, and the first ending stands. Stopping
            // time does not stop the physics step the ending happened in: hits
            // already queued in it - from the engine, and from the rounds' own
            // sweeps - still arrive. A lethal one landing behind the boss's last
            // point would open the death screen, and the two endings are sibling
            // screens, so it would close the victory. Found by ChatGPT's scan on
            // 25 September 2026, in the code rather than in play; BossEmitter and
            // BossWeakPoint refuse the reverse. The beat before the death card
            // counts as over: the ship is already breaking up.
            if (RunOutcome.Decided)
            {
                return;
            }

            // Already dead: the death screen is up and time has stopped, but queued
            // trigger events from the same physics step still arrive. Ignoring them
            // is what stops the death sound stacking on itself. The death screen
            // reports the run ended, so the check above catches these first now;
            // this one reads the player's own state rather than trusting that.
            if (health.IsDead)
            {
                return;
            }

            // Checked after the dead test rather than before it, so switching
            // invulnerability on does not resurrect a player who is already on the
            // death screen.
            if (Invulnerable)
            {
                return;
            }

            // After both checks above, so a hit that could not have landed is not
            // one the deflector spends itself on. Called for every hit once the
            // deflector is held, blocked or not: each one restarts its recharge.
            if (deflector != null && deflector.TryAbsorb(Time.time))
            {
                if (GameSounds.Instance != null)
                {
                    GameSounds.Play(GameSounds.Instance.DeflectorBlock);
                }

                Rumble.Pulse(Rumble.Strength.Block);
                return;
            }

            bool killed = health.TakeDamage(Mathf.Max(1, damage));
            LastHitBy = source;

            if (killed)
            {
                RunStats.RecordKiller(source);
            }

            if (spawnHitEffect)
            {
                ObjectPool.Spawn(playerHit, transform.position, transform.rotation);
            }

            // Was missing, so bullet damage was invisible until death.
            healthBar.SetHealth(health.Current);
            Hurt?.Invoke(from);
            PlayDamageSound(killed);

            if (killed)
            {
                BeginDeath();
            }
            else
            {
                Rumble.Pulse(Rumble.Strength.Hit);
                React(from);
            }
        }

        /// <summary>
        /// The ship's own answer to a hit it survived: the hull lights, the
        /// model is knocked away from what hit it, and the game holds for a
        /// twentieth of a second.
        ///
        /// A hit used to be a spark, a sound and a shorter bar. The spark is
        /// the round's, the bar is in the corner, and neither is the ship: the
        /// thing the player is looking at did not change when it was hurt. Not
        /// on the killing hit, which has its own beat and breaks the ship up.
        /// </summary>
        private void React(Vector3? from)
        {
            if (hullFlash != null)
            {
                hullFlash.Strike();
            }

            if (shipRig != null)
            {
                Vector3 push = from.HasValue ? transform.position - from.Value : -transform.TransformDirection(shipRig.Nose);

                // Mostly along the ring and a little of the height: a round
                // from above should not drive the ship out of its own hit box.
                push.y *= 0.5f;

                if (push.sqrMagnitude < 0.0001f)
                {
                    push = -transform.TransformDirection(shipRig.Nose);
                }

                shipRig.KickWorld(push, hitKnock);

                // Rocked both ways over a run, by which side it came from.
                float side = Vector3.Dot(push, transform.right) >= 0f ? 1f : -1f;
                shipRig.Twist(hitTwist * 0.4f, side * hitTwist);
            }

            RunTime.Freeze(hitFreezeSeconds);
            ScreenShake.Add(hitShake);
        }

        /// <summary>
        /// The beat between the killing hit and the Ship Lost card.
        ///
        /// Until 30 September 2026 the card went up on the frame of the hit
        /// and time stopped with it, so the shot that ended the run was never
        /// seen to land. Now the game slows to a quarter, the ship breaks up,
        /// the music fades (MusicSource hears RunOutcome.Ending), and the card
        /// follows as it did before. The run is lost from the first frame of
        /// it: nothing can hurt the ship, pause the game or win the fight
        /// while it plays.
        /// </summary>
        private void BeginDeath()
        {
            Rumble.Pulse(Rumble.Strength.Death);
            CancelInvoke(nameof(Shoot));

            // Before the ending is reported: a shake is not started once the run is decided.
            ScreenShake.Add(deathShake);

            if (deathBeatSeconds <= 0f || !isActiveAndEnabled)
            {
                ShowDeathCard();
                return;
            }

            RunOutcome.ReportEnding(deathBeatSpeed);
            BreakUp();
            StartCoroutine(DeathBeat());
        }

        private IEnumerator DeathBeat()
        {
            // Real seconds: the game's own are running at a quarter.
            yield return new WaitForSecondsRealtime(deathBeatSeconds);
            ShowDeathCard();
        }

        private void ShowDeathCard()
        {
            if (deathMenu != null)
            {
                deathMenu.ShowDeathMenu();
            }
        }

        /// <summary>
        /// The ship goes up: an explosion where it was, and everything it
        /// draws switched off. Switched off rather than the object disabled,
        /// because the ship carries the audio listener and the card still
        /// needs this component to put it up.
        /// </summary>
        private void BreakUp()
        {
            GameObject blast = deathExplosion != null ? deathExplosion : playerHit;
            if (blast != null)
            {
                ObjectPool.Spawn(blast, transform.position, transform.rotation);
            }

            // The model as a whole: ShipThrusters lives on it and switches its
            // flares and the dash light back on every frame, which put three
            // glowing parts where the ship had been (found in play on 30
            // September 2026). Nothing on it is needed once the ship is gone.
            if (instantiatedChild != null)
            {
                instantiatedChild.SetActive(false);
            }

            foreach (Renderer part in GetComponentsInChildren<Renderer>())
            {
                part.enabled = false;
            }

            foreach (Light glow in GetComponentsInChildren<Light>())
            {
                glow.enabled = false;
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

            if (dash == null)
            {
                TryGetComponent(out dash);
            }

            if (slowMo == null)
            {
                TryGetComponent(out slowMo);
            }

            // Captured before any pick can move it, so each Attack Speed pick is
            // measured against the gun's starting rate rather than against whatever
            // the previous pick left behind.
            baseSpawnDelay = spawnDelay;

            // Where the first volley would fall one interval after, so a pick
            // before it keeps it where it is.
            lastVolleyAt = Time.time + initialDelay - spawnDelay;
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
        /// The four and eight rows are offset by half a step so they sit
        /// symmetrically about the pivot. The double keeps the shape it always
        /// had, one on the pivot and one above. Until 15 November 2026 the rows
        /// were one, two, three and six; Ian asked for a middle stage of four
        /// and a last of eight, and nothing offers the three-row now.
        ///
        /// There used to be a fifth row, Back Shot, which fired five forward and
        /// four the other way round the ring. It is gone: it was the one upgrade
        /// that changed what the gun *is* rather than how much of it there is, and
        /// with the progression stretched over a run twice as long, other picks
        /// can do the stretching - the pool holds forty now. Nothing fires
        /// backwards, so the backward table and the second FireLine went with it.
        /// </summary>
        private static readonly float[][] ForwardPattern =
        {
            new[] { 0f },
            new[] { 0f, 1f },
            new[] { 0.5f, -0.5f, 1.5f, -1.5f },
            new[] { 0.5f, -0.5f, 1.5f, -1.5f, 2.5f, -2.5f, 3.5f, -3.5f }
        };

        /// <summary>How many rounds a volley fires at a shot stage, for the tests.</summary>
        public static int RoundsAtStage(int stage)
        {
            return ForwardPattern[Mathf.Clamp(stage, 0, ForwardPattern.Length - 1)].Length;
        }

        private void Shoot()
        {
            if (ArenaIntroDirector.IsPlaying)
            {
                return;
            }

            lastVolleyAt = Time.time;

            // Once per volley, not once per bullet. The widest pattern fires eight at
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
            Kick();
        }

        /// <summary>
        /// The gun's kick: the model is knocked back off its nose and a flash
        /// shows at the muzzle. Once a volley, like the sound.
        /// </summary>
        private void Kick()
        {
            if (shipRig != null && recoil > 0f)
            {
                shipRig.Kick(-shipRig.Nose * recoil);
                shipRig.Twist(recoilTwist, 0f);
            }

            if (muzzles != null)
            {
                muzzles.Fire();
            }
            else if (thrusters != null)
            {
                thrusters.MuzzleFlash();
            }
        }

        /// <summary>
        /// Spawns one bullet per offset. Each leaves from one of the guns the
        /// ship is wearing and opens out to its place in the pattern, spaced
        /// up the pivot, which is where all of them used to appear at once.
        ///
        /// A round keeps the pivot's distance from the arena's axis: it is
        /// put on the line through the pivot along the nose, at the muzzle's
        /// reach and height. The guns sit across the hull's depth, and a
        /// round holds whatever distance it is fired at for its whole flight.
        /// </summary>
        private void FireLine(GameObject prefab, float[] offsets)
        {
            if (prefab == null || shootPivot == null)
            {
                return;
            }

            Vector3 pivot = shootPivot.position;
            Vector3 nose = Vector3.zero;

            if (muzzles != null)
            {
                nose = muzzles.transform.right;
                nose.y = 0f;
                nose = nose.sqrMagnitude > 0.0001f ? nose.normalized : Vector3.zero;
            }

            for (int i = 0; i < offsets.Length; i++)
            {
                float height = pivot.y + offsets[i] * shotSpacing;
                Vector3 from = new Vector3(pivot.x, height, pivot.z);
                bool fanned = nose != Vector3.zero;

                if (fanned)
                {
                    int row = ShipMuzzles.RowFor(ShipMuzzles.Rank(offsets, i), offsets.Length, muzzles.Rows);
                    Vector3 muzzle = muzzles.WorldPoint(row);

                    from = pivot + nose * Vector3.Dot(muzzle - pivot, nose);
                    from.y = muzzle.y;
                }

                GameObject round = ObjectPool.Spawn(prefab, from, Quaternion.Euler(0f, 0f, 90f));

                if (fanned && round != null && round.TryGetComponent(out ShootScript script))
                {
                    script.Fan(height, ShipMuzzles.FanSeconds);
                }
            }
        }

        /// <summary>
        /// A kill's reward arriving, before the multiplier.
        ///
        /// Scaled here, once, and the scaled number is the one used everywhere:
        /// the bar, the figure floating up from the kill, and the run's total.
        /// The last two used to take the reward before scaling, so at the
        /// multiplier of 3 a Fighter read "+15" while the bar took 45.
        ///
        /// What crosses the threshold carries into the next level rather than
        /// being dropped, and a reward that covers more than one threshold pays
        /// for each. The bar used to empty on every level-up, which threw away
        /// whatever the kill had brought past the line.
        /// </summary>
        private void HandleEXPChange(int reward, Vector3 where)
        {
            // A lost run pays nothing more. Rounds still flying when the ship
            // goes keep killing through the death beat, and each kill used to
            // float a "+XP" over the wreck, fill the bar and could play the
            // level-up sound over the explosion. A won run's beat is left as
            // it is.
            if (RunOutcome.Decided && !RunOutcome.Won)
            {
                return;
            }

            int awarded = Mathf.RoundToInt(reward * experienceMultiplier);

            PickupLabelBoard.Experience(where, awarded);
            RunStats.RecordExperience(awarded);

            currentExperience += awarded;

            // LevelUp raises the threshold, so this ends even at a cost
            // increase of 0; the guard is for a threshold authored at 0.
            while (maxExperience > 0 && currentExperience >= maxExperience)
            {
                currentExperience -= maxExperience;
                LevelUp();
            }

            if (expBar != null)
            {
                expBar.setCurrentExp(currentExperience);
            }
        }

        /// <summary>
        /// One level, with its offer. The experience it cost is spent by the
        /// caller: HandleEXPChange carries the rest over, and the debug menu's
        /// level-up costs nothing, so it leaves the bar where it was.
        /// </summary>
        private void LevelUp(bool offerSkill = true)
        {
            HitchLog.Note("level up");

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.LevelUp);
            }

            // Counted before the offer goes out, so anything the offer reads sees
            // the level just reached rather than the one being left.
            currentLevel += 1;
            RunStats.RecordLevel(currentLevel);

            //Here we'll make it so a popup image appears that pauses the game and the player is able to choose between 3 power ups or something like that
            if (offerSkill && skillSelect != null) { skillSelect.PickSkill(); }

            maxExperience += levelCostIncrease;

            if (expBar != null)
            {
                expBar.setMaxExp(maxExperience);
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
        /// <summary>
        /// Advances the actual level/XP threshold and UI through the normal path.
        /// Presets suppress offers because they apply their selected skills directly.
        /// </summary>
        public void DebugLevelUp(bool offerSkill = true)
        {
            if (!RunOutcome.RunEnded && health != null && !health.IsDead)
            {
                LevelUp(offerSkill);
            }
        }
#endif

        // How many shot upgrades have been taken. Indexes ForwardPattern, which
        // Shoot() reads.
        [SerializeField]
        [Tooltip("Shot pattern stage: 0 one round, 1 two, 2 four, 3 eight. Serialized so a " +
                 "stage can be tried from here without playing up to it - it replaced four " +
                 "separate bools that had to be kept mutually exclusive by hand.")]
        private int shotUpgrades;

        /// <summary>
        /// The number of upgrades after which the pattern stops changing: one
        /// per row of <see cref="ForwardPattern"/> after the opening single
        /// shot. It was 4 until Back Shot was retired, which left a fourth pick
        /// that added nothing.
        /// </summary>
        public const int MaxShotUpgrades = 3;

        [SerializeField]
        [Tooltip("The shot pattern stage each Hydra pick reaches, in pick order: 1 two rounds, " +
                 "2 four, 3 eight. The last entry holds for any pick past the end. Left empty, " +
                 "each pick moves one stage. The scene sets 1, 2 and 3 since 15 November 2026; " +
                 "it set 1 and 3 before, when the second pick jumped from two rounds to six.")]
        private int[] shotStageByPick = new int[0];

        /// <summary>How many Shot Upgrade picks have been taken this run.</summary>
        private int shotPicks;

        public void UpgradeShotPattern(){
            if(shotUpgrades >= MaxShotUpgrades){
                return;
            }

            shotPicks++;

            // Never less than one stage a pick, whatever the list says, so a
            // pick cannot be spent on the pattern the ship already fires.
            int stage = shotUpgrades + 1;

            if (shotStageByPick != null && shotStageByPick.Length > 0)
            {
                stage = Mathf.Max(
                    stage,
                    shotStageByPick[Mathf.Clamp(shotPicks - 1, 0, shotStageByPick.Length - 1)]);
            }

            shotUpgrades = Mathf.Min(stage, MaxShotUpgrades);
            Fit(ShipUpgrade.Hydra, shotUpgrades);

            if (muzzles != null)
            {
                muzzles.SetStage(shotUpgrades);
            }
        }

        /// <summary>How much larger the hit box is for a model drawn this much larger.</summary>
        public static float HitBoxScale(float modelScale, float growth)
        {
            return 1f + (modelScale - 1f) * Mathf.Clamp01(growth);
        }

        /// <summary>
        /// The box follows the model as drawn at <see cref="hitBoxModelScale"/>:
        /// its middle moves out as far as that model's would, and its size takes
        /// its share of the growth. modelScale itself never reaches it.
        /// </summary>
        private void GrowHitBox()
        {
            if (!TryGetComponent(out BoxCollider box))
            {
                return;
            }

            box.center *= hitBoxModelScale;
            box.size *= HitBoxScale(hitBoxModelScale, hitBoxGrowth);
        }

        /// <summary>
        /// Bolts an upgrade's parts onto the model: every stage up to this
        /// many picks. See <see cref="ShipParts"/>.
        /// </summary>
        private void Fit(ShipUpgrade upgrade, int picksTaken)
        {
            if (shipParts != null)
            {
                shipParts.Set(upgrade, picksTaken);
            }
        }

        private int maxHealthPicks;
        private int moveSpeedPicks;
        private int dashPicks;

        public void Heal(int hp){
            health.Heal(hp);
            healthBar.SetHealth(health.Current);
        }

        public void AddMaxHealth(int hp){
            health.RaiseMax(hp);
            healthBar.AddMaxHealth(hp);
            Fit(ShipUpgrade.MaxHealth, ++maxHealthPicks);
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

        [Header("Dash recovery")]
        [SerializeField]
        [Range(0.05f, 5f)]
        [Tooltip("Seconds taken off the dash cooldown per Dash Recovery pick. From 10, three " +
                 "picks at 1.5 reach 5.5. Scaled with the cooldown on 25 September 2026: it was " +
                 "0.15 off a 1 second dash, the same share of it.")]
        private float dashCooldownStep = 1.5f;

        [SerializeField]
        [Range(0f, 10f)]
        [Tooltip("The shortest the dash cooldown may get. A balance cap, like Shot Interval Floor: " +
                 "the burst is invincible, so a dash with no gap behind it is most of the way to " +
                 "god mode.")]
        private float dashCooldownFloor = 4f;

        [Header("Slow Mo")]
        [SerializeField]
        [Tooltip("Seconds from one Slow Mo to the next, one entry per pick: the first pick grants " +
                 "it, the later ones shorten the wait. The last entry holds for any pick past the end. " +
                 "How long each slowdown lasts is Duration on the Player Slow Mo component.")]
        private float[] slowMoCooldown = { 30f, 24f, 18f };

        [SerializeField]
        [Tooltip("The Slow Mo ability, which reads its button and slows the game. Found on this " +
                 "object when left empty.")]
        private PlayerSlowMo slowMo;

        private int slowMoPicks;

        [Header("Deflector")]
        [SerializeField]
        [Tooltip("Seconds without being hit before the deflector's charge comes back, one entry " +
                 "per pick: the first pick grants it, the second shortens the wait. The last entry " +
                 "holds for any pick past the end.")]
        private float[] deflectorRecharge = { 10f, 6f };

        [Header("Magnet")]
        [SerializeField]
        [Tooltip("How near, in world units, a pickup has to be before it drifts to the player, one " +
                 "entry per Magnet pick. The pickups of one offer are a third of the ring apart, over " +
                 "30 units at the lane, so no reach here can take two of them.")]
        private float[] magnetReach = { 4f, 7f };

        [SerializeField]
        [Min(0f)]
        [Tooltip("World units a second a pickup in reach drifts toward the player.")]
        private float magnetPullSpeed = 10f;

        /// <summary>Null until the first Deflector pick.</summary>
        private DeflectorCharge deflector;

        private int deflectorPicks;
        private int magnetPicks;

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

        /// <summary>When the gun last fired, so a pick can keep its rhythm.</summary>
        private float lastVolleyAt;

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

            // The next volley comes one new interval after the last one, not
            // one new interval from now. Restarting the timer from the pick
            // threw away however much of the current gap had already run, so
            // taking a faster gun could cost up to a whole interval of silence
            // (scan of 28 September 2026).
            float untilNext = Mathf.Max(0f, lastVolleyAt + spawnDelay - Time.time);
            CancelInvoke(nameof(Shoot));
            InvokeRepeating(nameof(Shoot), untilNext, spawnDelay);
            Fit(ShipUpgrade.AttackSpeed, attackSpeedPicks);
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
            Fit(ShipUpgrade.MoveSpeed, ++moveSpeedPicks);
        }

        /// <summary>
        /// Shortens the dash cooldown by one step. The dash owns the cooldown,
        /// and the bar reading it follows along without being told.
        /// </summary>
        public void QuickenDash()
        {
            if (dash != null)
            {
                dash.ShortenCooldown(dashCooldownStep, dashCooldownFloor);
            }

            Fit(ShipUpgrade.DashRecovery, ++dashPicks);
        }

        /// <summary>
        /// Grants the deflector charged, or shortens its recharge if it is held.
        /// A later pick does not refill a spent charge: it shortens the wait for
        /// it, which is the thing the pick is for.
        /// </summary>
        public void UpgradeDeflector()
        {
            deflectorPicks++;
            float recharge = PerPick(deflectorRecharge, deflectorPicks, 10f);

            if (deflector == null)
            {
                deflector = new DeflectorCharge(recharge);

                // It has no button, so the line says what it does instead.
                Tutorial.Teach(ControlHint.Deflector);
            }
            else
            {
                deflector.SetRecharge(recharge);
            }

            Fit(ShipUpgrade.Deflector, deflectorPicks);
        }

        /// <summary>Pickups start drifting in from a little further away.</summary>
        public void ExtendMagnet()
        {
            magnetPicks++;
            Fit(ShipUpgrade.Magnet, magnetPicks);
        }

        /// <summary>
        /// Grants Slow Mo ready to use, or shortens the wait between uses once
        /// it is held. Like a Deflector pick, a later one does not refill a wait
        /// already running; it ends it sooner.
        /// </summary>
        public void UpgradeSlowMo()
        {
            if (slowMo == null)
            {
                Debug.LogWarning("Player has no PlayerSlowMo, so the Slow Mo pick did nothing.", this);
                return;
            }

            slowMoPicks++;
            slowMo.Grant(PerPick(slowMoCooldown, slowMoPicks, 30f));
            Fit(ShipUpgrade.SlowMo, slowMoPicks);

            if (slowMoPicks == 1)
            {
                Tutorial.Teach(ControlHint.SlowMo);
            }
        }

        /// <summary>Whether a Deflector pick has been taken this run. Read by the HUD.</summary>
        public bool HasDeflector => deflector != null;

        /// <summary>True when the deflector is held and will block the next hit. Read by DeflectorShield.</summary>
        public bool DeflectorCharged => deflector != null && deflector.IsCharged(Time.time);

        /// <summary>How far the deflector's charge has come back, 0 to 1; 0 when not held.</summary>
        public float DeflectorReadyFraction => deflector != null ? deflector.ReadyFraction(Time.time) : 0f;

        /// <summary>
        /// How near a pickup has to be to drift to the player, 0 with no Magnet
        /// pick. Read by PickupSpawner, which moves the pickups.
        /// </summary>
        public float MagnetReach => PerPick(magnetReach, magnetPicks, 0f);

        /// <summary>World units a second a pickup in reach drifts in at.</summary>
        public float MagnetPullSpeed => magnetPullSpeed;

        /// <summary>
        /// The entry for a pick count, <paramref name="none"/> before the first
        /// pick. Clamped, so picks past the end of a list keep its last entry.
        /// </summary>
        private static float PerPick(float[] values, int picks, float none)
        {
            if (values == null || values.Length == 0 || picks <= 0)
            {
                return none;
            }

            return values[Mathf.Clamp(picks - 1, 0, values.Length - 1)];
        }
    }
}
