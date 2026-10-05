using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// What the volcano throws: embers that rise through the plume all run
    /// long, and lava bombs that it lobs onto its own slopes when it surges.
    ///
    /// Environment roadmap, item 38, 5 October 2026. Before it, 65 seconds
    /// into a run the whole scene held two live particles, and nothing left
    /// the crater but fog.
    ///
    /// Both follow <see cref="Eruption"/>, found on a parent. The embers'
    /// rate is its level. The bombs answer its surges: a volley when a new
    /// group of enemies arrives, a larger one for the Leviathan, and small
    /// ones every few seconds while the volcano stands at full, so that the
    /// boss fight does not have a mountain that threw everything it had in
    /// its first three seconds.
    ///
    /// The two particle systems, and the smaller ones that are a bomb's tail
    /// and its landing, are made by EruptionSparksBuilder and live in the
    /// EruptionSparks prefab. A bomb lands on the island's own surface: the
    /// island, the cone and the lava carry mesh colliders for it, the only
    /// 3D colliders in the scene besides the ship's.
    ///
    /// **Nothing here reaches the lane.** The bombs' speed and spread put the
    /// furthest landing about ten units from the axis, against a lane at
    /// 19.72, and that arithmetic is tested (<see cref="Reach"/>). It is also
    /// not trusted: any bomb found past <see cref="keepWithin"/> is put out
    /// where it is, so no retuning of the throw can put one among the ships.
    /// They are lava-coloured, which hostile fire was until the same day; it
    /// is violet now, so a bomb cannot be taken for a round.
    /// </summary>
    public sealed class EruptionSparks : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The sparks rising out of the crater.")]
        private ParticleSystem embers;

        [SerializeField]
        [Tooltip("The lava bombs. Emitted from here, never by its own emission module.")]
        private ParticleSystem bombs;

        [Header("Embers")]
        [SerializeField]
        [Tooltip("Embers a second with the volcano at rest (X) and at full (Y).")]
        private Vector2 embersPerSecond = new Vector2(6f, 50f);

        [SerializeField]
        [Min(0)]
        [Tooltip("Extra embers thrown at once as a surge begins, for a surge at full strength.")]
        private int embersPerSurge = 60;

        [Header("Lava bombs")]
        [SerializeField]
        [Min(0)]
        [Tooltip("Bombs in the volley when a new group of enemies arrives.")]
        private int bombsPerSurge = 12;

        [SerializeField]
        [Min(0)]
        [Tooltip("Bombs in the volley when the Leviathan arrives.")]
        private int bombsForTheLeviathan = 22;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Seconds a volley is spread over, so the bombs leave one after another.")]
        private float volleySeconds = 2.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("From this level up the volcano keeps throwing small volleys of its own.")]
        private float keepsThrowingFrom = 0.9f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("Seconds between those small volleys.")]
        private float throwsEvery = 9f;

        [SerializeField]
        [Min(0)]
        [Tooltip("Bombs in each of those small volleys.")]
        private int bombsPerThrow = 4;

        [SerializeField]
        [Min(0f)]
        [Tooltip("A bomb found further than this from the island's axis is put out where it is. The lane is 19.72 out; 12 leaves 7 between the furthest bomb and the nearest ship.")]
        private float keepWithin = 12f;

        private Eruption eruption;
        private Vector3 axis;

        /// <summary>Bombs still to leave in the volley under way, and how fast they go.</summary>
        private int owed;
        private float owedPerSecond;

        /// <summary>The part of a bomb the volley has earned since the last one left.</summary>
        private float due;

        private float sinceThrow;

        private ParticleSystem.Particle[] live;

        /// <summary>
        /// How far from where it was thrown a bomb comes down: thrown at
        /// <paramref name="speed"/>, <paramref name="degreesFromUp"/> off
        /// vertical, falling at <paramref name="gravity"/> and landing
        /// <paramref name="drop"/> below where it left.
        /// </summary>
        public static float Reach(float speed, float degreesFromUp, float gravity, float drop)
        {
            if (gravity <= 0f)
            {
                return float.PositiveInfinity;
            }

            float radians = degreesFromUp * Mathf.Deg2Rad;
            float up = speed * Mathf.Cos(radians);
            float out_ = speed * Mathf.Sin(radians);
            float seconds = (up + Mathf.Sqrt(up * up + 2f * gravity * Mathf.Max(drop, 0f))) / gravity;
            return out_ * seconds;
        }

        /// <summary>Whether <paramref name="position"/> is further than <paramref name="limit"/> from the upright line through <paramref name="axis"/>.</summary>
        public static bool IsPast(Vector3 position, Vector3 axis, float limit)
        {
            float x = position.x - axis.x;
            float z = position.z - axis.z;
            return x * x + z * z > limit * limit;
        }

        private void Awake()
        {
            eruption = GetComponentInParent<Eruption>();

            GameObject scenario = GameObject.FindWithTag("Scenario");
            axis = scenario != null ? scenario.transform.position : Vector3.zero;

            if (bombs != null)
            {
                live = new ParticleSystem.Particle[bombs.main.maxParticles];
            }
        }

        private void OnEnable()
        {
            if (eruption != null)
            {
                eruption.Surged += OnSurge;
            }
        }

        private void OnDisable()
        {
            if (eruption != null)
            {
                eruption.Surged -= OnSurge;
            }
        }

        private void OnSurge(float strength)
        {
            bool leviathan = strength >= 1f;
            Throw(leviathan ? bombsForTheLeviathan : bombsPerSurge);

            if (embers != null)
            {
                embers.Emit(Mathf.RoundToInt(embersPerSurge * (leviathan ? 1f : 0.5f)));
            }

            sinceThrow = 0f;
        }

        /// <summary>Adds <paramref name="count"/> bombs to the volley, spread over the volley's seconds.</summary>
        private void Throw(int count)
        {
            if (count <= 0)
            {
                return;
            }

            owed += count;
            owedPerSecond = Mathf.Max(owedPerSecond, owed / volleySeconds);
        }

        private void Update()
        {
            float level = eruption != null ? eruption.Level : 0f;

            if (embers != null)
            {
                ParticleSystem.EmissionModule emission = embers.emission;
                emission.rateOverTime = Mathf.Lerp(embersPerSecond.x, embersPerSecond.y, level);
            }

            if (bombs == null)
            {
                return;
            }

            if (level >= keepsThrowingFrom)
            {
                sinceThrow += Time.deltaTime;

                if (sinceThrow >= throwsEvery)
                {
                    sinceThrow = 0f;
                    Throw(bombsPerThrow);
                }
            }

            if (owed > 0)
            {
                // Whole bombs only; the part of one waits for the next frame.
                due += owedPerSecond * Time.deltaTime;
                int now = Mathf.Min(owed, Mathf.FloorToInt(due));

                if (now > 0)
                {
                    bombs.Emit(now);
                    due -= now;
                    owed -= now;
                }

                if (owed == 0)
                {
                    owedPerSecond = 0f;
                    due = 0f;
                }
            }
        }

        private void LateUpdate()
        {
            if (bombs == null || live == null || keepWithin <= 0f)
            {
                return;
            }

            int count = bombs.GetParticles(live);
            bool changed = false;

            for (int i = 0; i < count; i++)
            {
                if (IsPast(live[i].position, axis, keepWithin))
                {
                    live[i].remainingLifetime = 0f;
                    changed = true;
                }
            }

            if (changed)
            {
                bombs.SetParticles(live, count);
            }
        }
    }
}
