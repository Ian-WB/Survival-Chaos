using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// The boss: health, three acts, and the attacks each act allows.
    ///
    /// Replaces BossScript, which held 32 named pivot fields, four projectile
    /// fields, eight delay fields (six of them unread), and three near-identical
    /// firing methods that all fired every gun at once forever.
    ///
    /// The fight it runs now is one rule borrowed from Resogun and one fact the
    /// arena was already true about. The rule: the hull is not the target, the
    /// things bolted to it are, and wrecking one takes an attack out of the
    /// fight. The fact: a boss bullet laps the ring in 4.5 seconds and the player
    /// in 12.3, so nothing the boss fires ever really leaves - it comes back
    /// round and arrives from behind. Between them they turn 300 hit points from
    /// a number that has to be worn down into a fight with a shape.
    ///
    /// Health is one pool for all three acts. The emplacements spend the first
    /// half of it and the hull the second, which is what keeps the bar moving
    /// through a phase where the hull itself is taking nothing - and means
    /// retuning an emplacement cannot quietly change what the boss is worth.
    /// </summary>
    public sealed class BossEmitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Stats for the boss. Falls back to the health value below when unset.")]
        private EnemyDefinition definition;

        [SerializeField]
        private int healthPoints = 1;

        [SerializeField]
        [Tooltip("The object carrying EnemyMovement, used to tell which way the boss is " +
                 "travelling and to drive the ram.")]
        private GameObject enemyShip;

        [SerializeField]
        [Tooltip("Sparks thrown when a shot hits the armoured hull. Deliberately not the same " +
                 "feedback as a shot that lands: while the armour is up the hull does not react " +
                 "at all, the bullet just breaks on it.")]
        private GameObject hullSpark;

        [SerializeField]
        [Tooltip("Health at or below which the last act begins. Only reachable once the hull " +
                 "is exposed, since the emplacements cannot take it that low.")]
        private int scuttleThreshold = 30;

        [SerializeField]
        [Tooltip("Seconds of silence when the fight changes act. The beat the player is given " +
                 "to notice that it did - every attack restarts its cadence from here.")]
        private float phaseChangeSilence = 2f;

        [SerializeField]
        private List<BossAttack> attacks = new List<BossAttack>();

        private HealthState health;
        private EnemyMovement movement;
        private Slider hpBar;
        private HitFlash flash;
        private BossPhaseState phase;
        private BossWeakPoint[] emplacements;

        private VolleyTimer[] timers;

        /// <summary>
        /// How many volleys each attack has fired this life. The curtain steps its
        /// gap by it and the rake alternates direction by it, so it is what makes
        /// both of them learnable rather than random.
        /// </summary>
        private int[] volleys;

        /// <summary>
        /// The row each muzzle of each attack belongs to, worked out once from the
        /// heights on the model. Fixed for the life of the object - the muzzle rig
        /// turns about the vertical axis, which is the one axis a row cares about.
        /// </summary>
        private int[][] rows;
        private int[] rowCounts;

        /// <summary>
        /// Which emplacements are wrecked, reused between volleys.
        ///
        /// One array rather than one per shed, because the shedding attack fires
        /// on a cadence for the whole of the second act and this is the only
        /// allocation it would otherwise make.
        /// </summary>
        private bool[] wreckedScratch;

        /// <summary>
        /// Which attacks are part-way through a volley that takes time.
        ///
        /// A rake lasts a third of a second and a lance nearly two, and both hold
        /// state outside themselves - the lance stops the boss changing height,
        /// the ram takes over its speed. Two copies of one attack running at once
        /// would each release what the other was still using.
        /// </summary>
        private bool[] running;

        private void Awake()
        {
            if (enemyShip != null)
            {
                enemyShip.TryGetComponent(out movement);
            }

            // Before the flash, so the pods' own flashes already exist and the
            // hull's leaves their renderers alone.
            emplacements = GetComponentsInChildren<BossWeakPoint>(includeInactive: true);

            flash = HitFlash.On(gameObject);

            BuildRows();
        }

        /// <summary>
        /// The state that belongs to a life rather than to the object.
        ///
        /// The boss arrives through a spawn stream, so it comes from the pool and
        /// Awake runs only on its first appearance. Health left there would come
        /// back at zero, the phase would come back at the last act, and volley
        /// timers would still be counting from whenever the previous boss entered.
        /// In practice its stream fires once a run and the pool is cleared between
        /// runs, so none of it is reachable today - but "unreachable" is a
        /// property of the wave asset, not of this file.
        /// </summary>
        private void OnEnable()
        {
            health = new HealthState(definition != null ? definition.MaxHealth : healthPoints);
            phase = new BossPhaseState(emplacements != null ? emplacements.Length : 0, scuttleThreshold);

            volleys = new int[attacks.Count];
            running = new bool[attacks.Count];
            RestartCadence(Time.time, 0f);
        }

        /// <summary>
        /// Hands back everything this was holding on another object.
        ///
        /// The lance and the ram both reach into the movement and leave it
        /// changed for as long as they run, and a boss killed mid-charge stops its
        /// coroutines where they stand - after the reach, before the release. The
        /// movement resets these on its own spawn as well; this covers the case
        /// where the object is disabled without being respawned. Same argument
        /// PlayerDash makes about the burst it shares with the camera.
        /// </summary>
        private void OnDisable()
        {
            ReleaseMovement();
            ClearTelegraphs();
        }

        private void Start()
        {
            Bar();
            WarmProjectilePools();
        }

        /// <summary>
        /// The boss health bar, looked up again each time until it is found.
        ///
        /// It used to be resolved once, in Start, and that was a race the boss
        /// could lose. Timer switches the bar on at the same moment in the run
        /// that the spawn stream releases the boss - both at 600 seconds - and a
        /// tag search does not see inactive objects. Whichever of the two Unity
        /// happens to run first decides whether the boss spends the whole fight
        /// with a working health bar or with none at all, and the failure is
        /// silent: the fight plays correctly and the bar simply sits full.
        ///
        /// Retrying costs a tag lookup per hit until it succeeds, which in
        /// practice means one, and nothing at all once it has.
        /// </summary>
        private Slider Bar()
        {
            if (hpBar != null)
            {
                return hpBar;
            }

            GameObject bar = GameObject.FindGameObjectWithTag("bossHpBar");

            if (bar == null || !bar.TryGetComponent(out hpBar))
            {
                return null;
            }

            hpBar.maxValue = health.Max;
            hpBar.value = health.Current;

            return hpBar;
        }

        /// <summary>
        /// Builds one volley's worth of every projectile the boss can fire,
        /// during its entrance rather than during the fight.
        ///
        /// The opening volley is the largest simultaneous spawn in the game, and
        /// it lands at the climax. One volley per travel direction is the useful
        /// amount: it covers the first shot of each attack and lets the rest grow
        /// on its own, without stalling the entrance to build a pool six volleys
        /// deep. The size needs no tuning - an attack fires one projectile per
        /// pivot, so the pivots already say how many.
        /// </summary>
        private void WarmProjectilePools()
        {
            foreach (BossAttack attack in attacks)
            {
                if (attack == null || attack.Pivots == null)
                {
                    continue;
                }

                // At least one, because a pattern can fire without muzzles: the
                // wreckage attack has no pivots at all, and warming zero of it
                // would move its first allocation into the fight.
                int perVolley = Mathf.Max(1, attack.Pivots.Length);
                attack.EachProjectile(projectile => ObjectPool.Warm(projectile, perVolley));
            }
        }

        /// <summary>
        /// Sorts every attack's muzzles into rows, once.
        ///
        /// Read off the model rather than authored, because the rows are already
        /// there in the heights the artist placed the guns at, and the second copy
        /// of a fact is the one that goes stale.
        /// </summary>
        private void BuildRows()
        {
            rows = new int[attacks.Count][];
            rowCounts = new int[attacks.Count];

            for (int i = 0; i < attacks.Count; i++)
            {
                rows[i] = MuzzleRows.Assign(MuzzleRows.HeightsOf(attacks[i].Pivots));
                rowCounts[i] = MuzzleRows.Count(rows[i]);
            }
        }

        /// <summary>
        /// Starts every attack's cadence from now, after a pause.
        ///
        /// Used at the top of a life and again at every change of act, which is
        /// what the silence between phases is made of. Each attack keeps its own
        /// authored delay on top of the pause, so the stagger that keeps three
        /// attacks from landing on the same frame survives the reset.
        /// </summary>
        private void RestartCadence(float now, float pause)
        {
            timers = new VolleyTimer[attacks.Count];

            for (int i = 0; i < attacks.Count; i++)
            {
                timers[i] = new VolleyTimer(now, pause + attacks[i].InitialDelay);
            }
        }

        private void Update()
        {
            if (timers == null)
            {
                return;
            }

            float now = Time.time;

            for (int i = 0; i < attacks.Count; i++)
            {
                BossAttack attack = attacks[i];

                if (running[i] || !attack.Available(phase.Phase))
                {
                    continue;
                }

                if (timers[i].TryFire(now, attack.Interval))
                {
                    Fire(attack, i);
                }
            }
        }

        /// <summary>
        /// Runs one volley of an attack, in whatever shape its pattern says.
        ///
        /// The volley number is taken here and passed down rather than read
        /// inside the patterns, because two of them step by it and the timed ones
        /// carry on running after it has moved on.
        /// </summary>
        private void Fire(BossAttack attack, int index)
        {
            int volley = volleys[index]++;

            switch (attack.Pattern)
            {
                case BossFirePattern.Curtain:
                    FireCurtain(attack, index, volley);
                    break;

                case BossFirePattern.Sequence:
                    StartCoroutine(RunSequence(attack, index, volley));
                    break;

                case BossFirePattern.Lance:
                    StartCoroutine(RunLance(attack, index));
                    break;

                case BossFirePattern.Ram:
                    StartCoroutine(RunRam(attack, index));
                    break;

                case BossFirePattern.Wreckage:
                    ShedWreckage(attack, volley);
                    break;

                default:
                    FireMuzzles(attack, attack.ProjectileFor(TravellingLeft), rows[index], -1);
                    break;
            }
        }

        /// <summary>
        /// Fires the muzzles of one attack, either all of them or one row.
        ///
        /// One method for both because the only difference is a comparison, and
        /// the sound and the direction handling below it are the parts worth not
        /// having two copies of.
        /// </summary>
        /// <param name="row">The row to fire, or -1 for every muzzle.</param>
        private void FireMuzzles(BossAttack attack, GameObject projectile, int[] muzzleRows, int row)
        {
            Transform[] pivots = attack.Pivots;

            if (projectile == null || pivots == null)
            {
                return;
            }

            bool fired = false;

            for (int i = 0; i < pivots.Length; i++)
            {
                if (pivots[i] == null || (row >= 0 && muzzleRows[i] != row))
                {
                    continue;
                }

                ObjectPool.Spawn(projectile, pivots[i].position, Quaternion.identity);
                fired = true;
            }

            if (fired)
            {
                PlayVolleySound();
            }
        }

        /// <summary>
        /// A wall with a gap in it, the gap stepping upward one row per volley.
        ///
        /// Stepped rather than random so it can be learned, which is the whole
        /// difference between a curtain and a coin toss. The gap moves by exactly
        /// one row each time and wraps at the top, so watching two volleys is
        /// enough to know where the third one will be.
        /// </summary>
        private void FireCurtain(BossAttack attack, int index, int volley)
        {
            int count = rowCounts[index];
            GameObject projectile = attack.ProjectileFor(TravellingLeft);

            for (int row = 0; row < count; row++)
            {
                if (MuzzleRows.IsGap(row, volley, count, attack.OpenRows))
                {
                    continue;
                }

                FireMuzzles(attack, projectile, rows[index], row);
            }
        }

        /// <summary>
        /// The same muzzles as a plain volley, one row at a time, alternating
        /// direction each cycle so it is a staircase to climb over as often as one
        /// to dive under.
        ///
        /// The projectile is chosen once at the start. A rake that changed
        /// direction half way through would leave its top half travelling the
        /// other way round the ring, which is a different attack and not one
        /// anybody asked for.
        /// </summary>
        private IEnumerator RunSequence(BossAttack attack, int index, int volley)
        {
            running[index] = true;

            GameObject projectile = attack.ProjectileFor(TravellingLeft);
            int count = rowCounts[index];
            bool upward = (volley & 1) == 0;

            for (int step = 0; step < count; step++)
            {
                FireMuzzles(attack, projectile, rows[index], upward ? step : count - 1 - step);

                if (step + 1 < count)
                {
                    yield return new WaitForSeconds(attack.StepSeconds);
                }
            }

            running[index] = false;
        }

        /// <summary>
        /// A telegraph, then a dense stream from the whole bank.
        ///
        /// The prow is the only bank that reaches the middle of the playable
        /// band - the keel covers its bottom quarter and the crown its top - so
        /// this is the only attack that makes the middle dangerous, and the
        /// middle is where a player with nothing to dodge would otherwise sit.
        /// It is the answer to loitering, and it is the reason the other two
        /// attacks are worth flying into: staying between them is not free
        /// either.
        ///
        /// The 1.2s default is one full crossing of the band at the player's
        /// climb rate, so the warning is always escapable and never escapable for
        /// free.
        /// </summary>
        private IEnumerator RunLance(BossAttack attack, int index)
        {
            running[index] = true;

            BossWeakPoint pod = attack.WeakPoint;
            float charge = attack.ChargeSeconds;

            for (float elapsed = 0f; elapsed < charge; elapsed += Time.deltaTime)
            {
                // Shot off its mounting part way through the wind-up. The warning
                // has to stop with it, or the player is dodging a shot that is
                // never coming.
                if (pod != null && pod.Destroyed)
                {
                    break;
                }

                if (pod != null)
                {
                    pod.Telegraph(charge > 0f ? elapsed / charge : 1f);
                }

                yield return null;
            }

            if (pod != null)
            {
                pod.Telegraph(0f);
            }

            if (pod == null || !pod.Destroyed)
            {
                GameObject projectile = attack.ProjectileFor(TravellingLeft);
                float until = Time.time + attack.BurstSeconds;

                do
                {
                    FireMuzzles(attack, projectile, rows[index], -1);
                    yield return new WaitForSeconds(attack.BurstInterval);
                }
                while (Time.time < until);
            }

            running[index] = false;
        }

        /// <summary>
        /// The boss itself, thrown round the ring at the player.
        ///
        /// Its counter is the dash and only the dash. Three times cruise beats the
        /// player's own orbit speed, so running the same way loses; the hull spans
        /// more than the whole playable band, so climbing loses. What is left is
        /// going through it, which the dash was measured against - 74.5 units of
        /// invincible travel against a hull 70.5 units wide along the ring.
        ///
        /// The telegraph is the hull flashing, because by this act there are no
        /// emplacements left to light up.
        /// </summary>
        private IEnumerator RunRam(BossAttack attack, int index)
        {
            running[index] = true;

            if (movement == null)
            {
                running[index] = false;
                yield break;
            }

            float charge = attack.ChargeSeconds;
            float nextBlink = 0f;

            for (float elapsed = 0f; elapsed < charge; elapsed += Time.deltaTime)
            {
                if (elapsed >= nextBlink)
                {
                    if (flash != null)
                    {
                        flash.Strike();
                    }

                    nextBlink = elapsed + BlinkSeconds;
                }

                yield return null;
            }

            movement.OrbitSpeedScale = attack.RamSpeedScale;
            yield return new WaitForSeconds(attack.BurstSeconds);

            ReleaseMovement();
            running[index] = false;
        }

        /// <summary>
        /// Seconds between flashes while the ram winds up. Fast enough to read as
        /// a warning light rather than as damage, which is the other thing a
        /// flashing hull means in this game.
        /// </summary>
        private const float BlinkSeconds = 0.15f;

        /// <summary>
        /// Tears one plate off a wrecked emplacement and leaves it where it was.
        ///
        /// Spawned at the emplacement rather than at the hull's centre, which is
        /// the whole point of it: the pods are mounted proud of the armour on the
        /// face the player sees, at the three heights the first act taught, so the
        /// plate appears where they were aiming a moment ago and stays at that
        /// height for the rest of its life.
        ///
        /// It rides the pool like a projectile because that is what it is to this
        /// class - something the attack releases, warmed at the entrance with
        /// everything else. What it does after that is <see cref="BossWreckage"/>'s
        /// business, and it deliberately has no thread back to here: a plate is
        /// not part of the boss once it is off, and the boss is not told when one
        /// breaks.
        /// </summary>
        private void ShedWreckage(BossAttack attack, int volley)
        {
            GameObject plate = attack.ProjectileFor(TravellingLeft);

            if (plate == null || emplacements == null || emplacements.Length == 0)
            {
                return;
            }

            if (wreckedScratch == null || wreckedScratch.Length != emplacements.Length)
            {
                wreckedScratch = new bool[emplacements.Length];
            }

            for (int i = 0; i < emplacements.Length; i++)
            {
                wreckedScratch[i] = emplacements[i] != null && emplacements[i].Destroyed;
            }

            int source = BossShedding.SourceIndex(wreckedScratch, volley);

            // Nothing wrecked yet. Reachable only if this attack is authored into
            // a phase where the emplacements can still be standing, and shedding
            // hull off a gun that is still firing is not what it means.
            if (source < 0)
            {
                return;
            }

            Transform mount = emplacements[source].transform;
            ObjectPool.Spawn(plate, mount.position, mount.rotation);
        }

        private bool TravellingLeft => movement != null && movement.TravellingLeft;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Shoot"))
            {
                return;
            }

            // Read before despawning, so sparks land where the bullet broke
            // rather than at the middle of a hull 150 units tall.
            Vector3 impact = other.transform.position;

            ObjectPool.Despawn(other.gameObject);

            if (!phase.HullVulnerable)
            {
                // Armour. The shot is spent and the boss does not react at all -
                // which is the point, and is why this is sparks rather than the
                // hull flash. A flash here would say "that landed" 150 times over
                // while the health bar never moved, and the player would rightly
                // conclude the bar was broken rather than that they were shooting
                // the wrong thing.
                if (hullSpark != null)
                {
                    ObjectPool.Spawn(hullSpark, impact, transform.rotation);
                }

                return;
            }

            if (SpendHealth(1))
            {
                return;
            }

            // Worth more here than anywhere else. The boss has three hundred hit
            // points and no spark prefab of its own, so without this the only
            // reading of progress is a health bar at the top of the screen -
            // which is not where the player is looking.
            if (flash != null)
            {
                flash.Strike();
            }
        }

        /// <summary>
        /// Takes damage off the one pool the whole fight shares, and reports
        /// whether that killed the boss.
        ///
        /// Every point of damage in the fight comes through here, from the hull
        /// and from the emplacements alike, so the bar cannot disagree with the
        /// fight and the last act cannot be missed by damage arriving down the
        /// route nobody thought to check.
        /// </summary>
        private bool SpendHealth(int amount)
        {
            bool killed = health.TakeDamage(amount);

            Slider bar = Bar();

            if (bar != null)
            {
                bar.value = health.Current;
            }

            if (killed)
            {
                Death();

                // The boss arrives through a spawn stream like everything else,
                // so it comes from the pool and goes back to it.
                ObjectPool.Despawn(gameObject);
                return true;
            }

            if (phase.ReportHealth(health.Current))
            {
                EnterPhase();
            }

            return false;
        }

        /// <summary>Damage that landed on an emplacement rather than on the hull.</summary>
        public void ReportEmplacementDamage(int amount)
        {
            SpendHealth(amount);
        }

        /// <summary>
        /// One emplacement wrecked. The emplacement guards against reporting
        /// twice, so this does not have to know which one it was.
        /// </summary>
        public void ReportEmplacementDestroyed()
        {
            if (phase != null && phase.ReportEmplacementDestroyed())
            {
                EnterPhase();
            }
        }

        /// <summary>Which act the fight is in. For the HUD and for tests.</summary>
        public BossPhase Phase => phase != null ? phase.Phase : BossPhase.Armoured;

        /// <summary>
        /// The beat where the fight changes shape.
        ///
        /// Everything mid-volley belongs to the act that just ended, so it is
        /// stopped rather than allowed to finish - a lance still streaming out of
        /// an emplacement that was destroyed to end the phase is the exact thing
        /// the phase change is supposed to have stopped. The silence afterwards is
        /// the player's cue that it did.
        /// </summary>
        private void EnterPhase()
        {
            StopAllCoroutines();
            ReleaseMovement();
            ClearTelegraphs();

            for (int i = 0; i < running.Length; i++)
            {
                running[i] = false;
            }

            RestartCadence(Time.time, phaseChangeSilence);
        }

        private void ReleaseMovement()
        {
            if (movement != null)
            {
                movement.OrbitSpeedScale = 1f;
            }
        }

        private void ClearTelegraphs()
        {
            if (emplacements == null)
            {
                return;
            }

            foreach (BossWeakPoint pod in emplacements)
            {
                if (pod != null)
                {
                    pod.Telegraph(0f);
                }
            }
        }

        /// <summary>
        /// One shot noise per volley rather than per muzzle - the crown alone has
        /// sixteen, and the retrigger guard on the sound is a safety net, not a
        /// licence to ask for sixteen copies of the same noise.
        /// </summary>
        private void PlayVolleySound()
        {
            if (GameSounds.Instance != null)
            {
                GameSounds.PlayAt(GameSounds.Instance.BossShot, transform.position);
            }
        }

        private void Death()
        {
            int reward = definition != null ? definition.ExperienceReward : 2;

            if (EXP.Instance != null)
            {
                EXP.Instance.AddEXP(reward);
            }

            PickupLabelBoard.Experience(transform.position, reward);
            RunStats.RecordKill(reward);

            // Not positional, and played before the object goes: this is the run
            // ending, not an event somewhere out on the ring.
            GameSounds sounds = GameSounds.Instance;
            if (sounds != null)
            {
                GameSounds.Play(sounds.BossDeath != null ? sounds.BossDeath : sounds.Victory);
            }

            RunOutcome.ReportBossDefeated();
        }
    }
}
