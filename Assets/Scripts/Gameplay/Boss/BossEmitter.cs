using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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

        /// <summary>
        /// The tag every hostile projectile carries, and so the tag of the light
        /// pool whose look the lance beam borrows.
        /// </summary>
        private const string HostileFireTag = "enemy_Shoot";

        [SerializeField]
        [Tooltip("Light cloned along the lance beam. Leave it empty and the beam borrows the " +
                 "template from the light pool that lights the boss's rounds, which is usually " +
                 "what you want - set this only to make the beam deliberately different.")]
        private Light beamLightTemplate;

        [SerializeField]
        [Tooltip("How many lights are spread along the lance beam. Four sits well under the 24 " +
                 "per cluster cell HDRP will hold before it starts dropping lights silently, " +
                 "which the forty-round stream this replaced did not.")]
        private int beamLights = 4;

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

        [Header("Muzzle tells")]
        [SerializeField]
        [Tooltip("The glow shown on a muzzle before it fires. Built by Survival Chaos/Apply Boss " +
                 "Tells and Routes: the ship's dash-ready glow, with its white-hot centre taken out.")]
        private Mesh tellMesh;

        [SerializeField]
        [Tooltip("Material for the muzzle glows: the thruster shader in hostile orange.")]
        private Material tellMaterial;

        [SerializeField]
        [Tooltip("Brightness of a muzzle glow at the moment its volley fires. Kept low for the " +
                 "reason the boss pods taught: pushed hard, the core blows out to a white ball " +
                 "and stops reading as the colour of the fire it announces.")]
        private float tellBrightness = 3f;

        [SerializeField]
        private List<BossAttack> attacks = new List<BossAttack>();

        private HealthState health;
        private EnemyMovement movement;
        private Slider hpBar;
        private HitFlash flash;
        private BossPhaseState phase;
        private BossWeakPoint[] emplacements;

        public static BossEmitter Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => Active = null;

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

        /// <summary>
        /// One beam, built on the first lance and reused for every one after it.
        /// The attack fires every 2.6 seconds and a pass lasts two, so there is
        /// never a second one to draw - and a mesh rebuilt every frame is not
        /// something to allocate and throw away twenty times a fight.
        /// </summary>
        private BossLanceBeam lanceBeam;

        /// <summary>
        /// One glow per muzzle, for the attacks that announce their volleys;
        /// null for the ones that do not. Built once, on the muzzles themselves,
        /// so they ride the mirroring rig with the guns. Switched off whenever
        /// they are not lit, which is nearly always.
        /// </summary>
        private Renderer[][] tells;
        private MaterialPropertyBlock tellBlock;

        /// <summary>
        /// The property the thruster shader takes its brightness from, the same
        /// one <see cref="ShipThrusters"/> drives.
        /// </summary>
        private static readonly int FlameIntensityId = Shader.PropertyToID("_FlameIntensity");

        private Transform arenaCentre;
        private bool warnedAboutCentre;

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
            Active = this;
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
            if (Active == this) { Active = null; }
            ReleaseMovement();
            ClearTelegraphs();
            ClearTells();
        }

        /// <summary>
        /// The beam is a root object rather than a child of the boss - it has to
        /// be, since it is drawn in world space and the boss moves - so nothing
        /// else would ever collect it. Without this, killing the boss leaves an
        /// arc of hostile geometry on the ring with no one left to switch it off.
        /// </summary>
        private void OnDestroy()
        {
            if (lanceBeam != null)
            {
                Destroy(lanceBeam.gameObject);
            }
        }

        private void Start()
        {
            Bar();
            WarmProjectilePools();
            BuildTells();
        }

        /// <summary>
        /// Puts a glow on every muzzle of every attack that announces itself.
        ///
        /// At runtime rather than in the prefab for the reason the ship's flares
        /// are: the safe way to edit a prefab from a script can add a component
        /// but not a child, and the other way is the one that took the GPU down.
        /// The boss is pooled and its muzzles come back with it, so this runs
        /// once for every life it will have.
        /// </summary>
        private void BuildTells()
        {
            tells = new Renderer[attacks.Count][];
            tellBlock = new MaterialPropertyBlock();

            bool wanted = false;

            for (int i = 0; i < attacks.Count; i++)
            {
                wanted |= attacks[i].TellSeconds > 0f && Tells(attacks[i].Pattern);
            }

            if (!wanted)
            {
                return;
            }

            // Said out loud because the failure is quiet: the volleys still fire
            // on time, just unannounced, which is exactly how they looked before.
            if (tellMesh == null || tellMaterial == null)
            {
                Debug.LogWarning(
                    "BossEmitter has attacks with a tell but no glow to show it, so they will fire " +
                    "unannounced. Run Survival Chaos/Apply Boss Tells and Routes.", this);
                return;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                BossAttack attack = attacks[i];

                if (attack.TellSeconds <= 0f || !Tells(attack.Pattern) || attack.Pivots == null)
                {
                    continue;
                }

                tells[i] = new Renderer[attack.Pivots.Length];

                for (int m = 0; m < attack.Pivots.Length; m++)
                {
                    if (attack.Pivots[m] != null)
                    {
                        tells[i][m] = BuildTell(attack.Pivots[m]);
                    }
                }
            }
        }

        /// <summary>
        /// The patterns this glow is for. The lance and the ram have their own
        /// charge, and a glow on top of it would be a second warning saying the
        /// same thing less well.
        /// </summary>
        private static bool Tells(BossFirePattern pattern)
        {
            return pattern == BossFirePattern.Curtain || pattern == BossFirePattern.Sequence;
        }

        private Renderer BuildTell(Transform muzzle)
        {
            var go = new GameObject("Muzzle Tell");
            go.transform.SetParent(muzzle, false);

            go.AddComponent<MeshFilter>().sharedMesh = tellMesh;

            MeshRenderer glow = go.AddComponent<MeshRenderer>();
            glow.sharedMaterial = tellMaterial;
            glow.shadowCastingMode = ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glow.lightProbeUsage = LightProbeUsage.Off;
            glow.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Object, for the reason the ship's flares give: the glow moves
            // rigidly with the boss, and anything that does not write its own
            // motion vectors gets dragged across the screen under FSR.
            glow.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            glow.enabled = false;

            return glow;
        }

        /// <summary>
        /// Lights one muzzle's glow, 0 dark and 1 at the moment it fires. Size
        /// and brightness both follow, so the warning grows as well as heats.
        /// </summary>
        private void SetTell(int attack, int muzzle, float level)
        {
            Renderer glow = tells != null && tells[attack] != null ? tells[attack][muzzle] : null;

            if (glow == null)
            {
                return;
            }

            bool lit = level > 0.002f;
            glow.enabled = lit;

            if (!lit)
            {
                return;
            }

            // In the muzzle's space, so divide its scale back out: the glow is a
            // world size, and the rig's scale is not a statement about it.
            float scale = Mathf.Abs(glow.transform.parent.lossyScale.x);
            float size = attacks[attack].TellSize;
            glow.transform.localScale = Vector3.one * (size * level / Mathf.Max(scale, 0.0001f));

            glow.GetPropertyBlock(tellBlock);
            tellBlock.SetFloat(FlameIntensityId, tellBrightness * level);
            glow.SetPropertyBlock(tellBlock);
        }

        /// <summary>Puts out every glow of one attack, or of all of them.</summary>
        private void ClearTells(int attack = -1)
        {
            if (tells == null)
            {
                return;
            }

            for (int i = 0; i < tells.Length; i++)
            {
                if (tells[i] == null || (attack >= 0 && i != attack))
                {
                    continue;
                }

                foreach (Renderer glow in tells[i])
                {
                    if (glow != null)
                    {
                        glow.enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Whether the emplacement feeding an attack has been shot off. Asked
        /// through a warning as well as before it, for the reason the lance
        /// gives: a warning for a shot that is never coming is a lie.
        /// </summary>
        private static bool Silenced(BossAttack attack)
        {
            return attack.WeakPoint != null && attack.WeakPoint.Destroyed;
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
                    if (attack.TellSeconds > 0f)
                    {
                        StartCoroutine(RunCurtain(attack, index, volley));
                    }
                    else
                    {
                        FireCurtain(attack, index, volley);
                    }

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

                SendAlongRoute(attack, ObjectPool.Spawn(projectile, pivots[i].position, Quaternion.identity), muzzleRows[i]);
                fired = true;
            }

            if (fired)
            {
                PlayVolleySound();
            }
        }

        /// <summary>
        /// Gives a round its attack's weave, from the row it left.
        ///
        /// Asked per round rather than per volley because crossing rows take
        /// opposite phases, and the row is only known per muzzle.
        /// </summary>
        private static void SendAlongRoute(BossAttack attack, GameObject round, int row)
        {
            if (!attack.HasRoute || round == null || !round.TryGetComponent(out ShootScript shot))
            {
                return;
            }

            shot.SetRoute(attack.RouteAmplitude, attack.RoutePeriod,
                RoundRoute.PhaseForRow(row, attack.RouteCrossing));
        }

        /// <summary>
        /// The same muzzles as <see cref="FireMuzzles"/>, one at a time.
        ///
        /// Four rounds leaving four muzzles at one instant is four rounds in one
        /// place when the muzzles are close together, and the prow's four are
        /// within about two units of each other - measured live at 0.53 to 2.37
        /// apart, against a round two units thick. So the lance was spawning a
        /// bundle four meshes deep rather than a stream. Walking the bank apart in
        /// time strings them along the flight path instead, which is the shape the
        /// attack is named for.
        ///
        /// Only worth it where the muzzles are clustered. The curtain and the rake
        /// leave their stagger at zero, because a wall that arrives in pieces is
        /// not a wall.
        ///
        /// Nothing calls this at the moment. The lance was the one bank clustered
        /// enough to need it, and since 8 September the lance is a beam rather than
        /// a stream - see <see cref="FireLanceBeam"/>. It is kept because the next
        /// clustered bank will want it and because every volley still carries the
        /// field, not because anything is waiting on it.
        ///
        /// The volley sound still fires once, on the first round rather than on
        /// each, for the same reason it always did.
        /// </summary>
        private IEnumerator FireMuzzlesStaggered(
            BossAttack attack, GameObject projectile, int[] muzzleRows, int row)
        {
            Transform[] pivots = attack.Pivots;

            if (projectile == null || pivots == null)
            {
                yield break;
            }

            var gap = new WaitForSeconds(attack.MuzzleStagger);
            bool fired = false;

            for (int i = 0; i < pivots.Length; i++)
            {
                if (pivots[i] == null || (row >= 0 && muzzleRows[i] != row))
                {
                    continue;
                }

                if (fired)
                {
                    yield return gap;
                }

                SendAlongRoute(attack, ObjectPool.Spawn(projectile, pivots[i].position, Quaternion.identity), muzzleRows[i]);

                if (!fired)
                {
                    fired = true;
                    PlayVolleySound();
                }
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
            GameObject projectile = attack.RoundFor(TravellingLeft);

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
        /// A curtain, announced: the rows that are about to fire glow first, and
        /// the one that stays dark is the gap.
        ///
        /// Running for the length of the warning, like the lance's charge, so a
        /// change of act stops it through the same path and a second curtain
        /// cannot start over the top of one still warning.
        /// </summary>
        private IEnumerator RunCurtain(BossAttack attack, int index, int volley)
        {
            running[index] = true;

            int count = rowCounts[index];
            int[] muzzleRows = rows[index];
            float tell = attack.TellSeconds;

            for (float elapsed = 0f; elapsed < tell; elapsed += Time.deltaTime)
            {
                if (Silenced(attack))
                {
                    break;
                }

                float level = VolleyTell.Together(elapsed / tell);

                for (int m = 0; m < muzzleRows.Length; m++)
                {
                    SetTell(index, m, MuzzleRows.IsGap(muzzleRows[m], volley, count, attack.OpenRows) ? 0f : level);
                }

                yield return null;
            }

            ClearTells(index);

            if (!Silenced(attack))
            {
                FireCurtain(attack, index, volley);
            }

            running[index] = false;
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

            GameObject projectile = attack.RoundFor(TravellingLeft);
            int count = rowCounts[index];
            int[] muzzleRows = rows[index];
            bool upward = VolleyTell.Upward(volley);
            float tell = attack.TellSeconds;

            // The warning lights the bank in the order the rake will fire it,
            // so where it starts and which way it climbs are both on the hull
            // before the first row leaves.
            for (float elapsed = 0f; elapsed < tell; elapsed += Time.deltaTime)
            {
                if (Silenced(attack))
                {
                    ClearTells(index);
                    running[index] = false;
                    yield break;
                }

                float progress = elapsed / tell;

                for (int m = 0; m < muzzleRows.Length; m++)
                {
                    int order = VolleyTell.FiringOrder(muzzleRows[m], count, upward);
                    SetTell(index, m, VolleyTell.InOrder(progress, order, count));
                }

                yield return null;
            }

            for (int step = 0; step < count; step++)
            {
                int row = VolleyTell.FiringOrder(step, count, upward);

                // Each row's glow goes out as it fires, so what is still lit is
                // what is still to come.
                for (int m = 0; m < muzzleRows.Length; m++)
                {
                    if (muzzleRows[m] == row)
                    {
                        SetTell(index, m, 0f);
                    }
                }

                FireMuzzles(attack, projectile, muzzleRows, row);

                if (step + 1 < count)
                {
                    yield return new WaitForSeconds(attack.StepSeconds);
                }
            }

            ClearTells(index);
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

            // The wind-up says what is coming, and until now it only said it to
            // the eye - the pod swells and nothing makes a noise. A player
            // watching the lane they are flying in was given no reason to look
            // at the prow.
            SoundDefinition chargeSound = GameSounds.Instance != null
                ? GameSounds.Instance.BossChargeLance
                : null;

            if (chargeSound != null)
            {
                GameSounds.PlayAt(chargeSound, transform.position);
            }

            for (float elapsed = 0f; elapsed < charge; elapsed += Time.deltaTime)
            {
                // Shot off its mounting part way through the wind-up. The warning
                // has to stop with it, or the player is dodging a shot that is
                // never coming.
                if (pod != null && pod.Destroyed)
                {
                    // The sound is half of that warning now, and it outlives the
                    // coroutine that started it, so it has to be cut rather than
                    // simply not continued.
                    if (chargeSound != null)
                    {
                        AudioDirector.Stop(chargeSound);
                    }

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
                FireLanceBeam(attack);
            }

            running[index] = false;
        }

        /// <summary>
        /// One beam from the prow, where this attack used to put forty rounds.
        ///
        /// The muzzles are averaged rather than fired one at a time. They sit
        /// between 0.53 and 2.37 units apart, which is what made four simultaneous
        /// rounds read as one bundle rather than as a line, and a single beam has
        /// no use for the distinction - their average is the middle of the prow,
        /// which is where a lance should look like it comes from.
        ///
        /// The round prefab is still read even though nothing spawns it any more:
        /// it is where the beam takes its material and its direction from, so the
        /// weapon keeps the art it had and left and right stay encoded in exactly
        /// one place instead of two.
        /// </summary>
        private void FireLanceBeam(BossAttack attack)
        {
            Transform[] pivots = attack.Pivots;
            GameObject round = attack.ProjectileFor(TravellingLeft);

            if (pivots == null || round == null)
            {
                return;
            }

            Vector3 sum = Vector3.zero;
            int found = 0;

            for (int i = 0; i < pivots.Length; i++)
            {
                if (pivots[i] == null)
                {
                    continue;
                }

                sum += pivots[i].position;
                found++;
            }

            Transform centre = ArenaCentre();

            if (found == 0 || centre == null)
            {
                return;
            }

            if (lanceBeam == null)
            {
                // Falls back to the light the boss's own rounds are lit by, so the
                // beam matches the fire it replaced without a second copy of the
                // same colour and range to keep in step. Assigning the field above
                // is only for making it deliberately different.
                Light template = beamLightTemplate != null
                    ? beamLightTemplate
                    : BulletLightPool.TemplateFor(HostileFireTag);

                lanceBeam = BossLanceBeam.Create(round, template, beamLights);
            }

            lanceBeam.Fire(sum / found, centre);
            PlayVolleySound();
        }

        /// <summary>
        /// The point the arena turns around, found the same way the projectiles
        /// find it. Cached after the first hit: the tag lookup is once per fight,
        /// not once per lance.
        /// </summary>
        private Transform ArenaCentre()
        {
            if (arenaCentre != null)
            {
                return arenaCentre;
            }

            GameObject scenario = GameObject.FindWithTag("Scenario");

            if (scenario == null)
            {
                if (!warnedAboutCentre)
                {
                    warnedAboutCentre = true;
                    Debug.LogWarning(
                        "BossEmitter found nothing tagged 'Scenario', so the lance has no ring " +
                        "to follow and will not fire.", this);
                }

                return null;
            }

            arenaCentre = scenario.transform;
            return arenaCentre;
        }

        /// <summary>
        /// The boss itself, thrown round the ring at the player.
        ///
        /// Its counter is the dash and only the dash. Three times cruise beats the
        /// player's own orbit speed, so running the same way loses; the hull spans
        /// more than the whole playable band, so climbing loses. What is left is
        /// going through it, which the dash was measured against - 7.45 units of
        /// invincible travel against a hull 7.05 units wide along the ring.
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

            // The ram telegraphs by flashing the hull, which is only visible if
            // the boss is on screen - and the thing it is announcing is the boss
            // arriving, so by definition it often is not. The sound is the half
            // of the warning that reaches a player looking somewhere else.
            if (GameSounds.Instance != null && GameSounds.Instance.BossChargeRam != null)
            {
                GameSounds.PlayAt(GameSounds.Instance.BossChargeRam, transform.position);
            }

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
            // rather than at the middle of a hull 15 units tall.
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

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
        /// <summary>Advances through the real damage and phase transitions, without ending the run.</summary>
        public bool DebugAdvancePhase()
        {
            if (RunOutcome.RunEnded || health == null || health.IsDead)
            {
                return false;
            }

            BossPhase before = Phase;
            if (before == BossPhase.Armoured)
            {
                foreach (BossWeakPoint pod in emplacements)
                {
                    if (pod == null || pod.Destroyed) { continue; }
                    ReportEmplacementDamage(pod.CurrentHealth);
                    pod.Wreck();
                    if (health.IsDead) { break; }
                }
            }
            else if (before == BossPhase.Exposed)
            {
                SpendHealth(Mathf.Max(0, health.Current - Mathf.Max(1, scuttleThreshold)));
            }

            return Phase != before;
        }
#endif

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
            ClearTells();

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
            // Final XP still counts, but cannot create a new upgrade choice behind the ending.
            RunOutcome.ReportRunEnded();
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
