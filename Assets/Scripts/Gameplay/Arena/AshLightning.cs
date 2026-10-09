using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// Lightning in the volcano's ash: now and then, once the eruption has
    /// built, a bolt jumps inside the plume and for a moment lights the smoke
    /// round it and puts a cold rim on the island and the ships.
    ///
    /// Environment roadmap, item 39, 8 October 2026. Real ash plumes do this.
    /// It was put off on 5 October for two reasons, and both shape it. It only
    /// reads once the plume is thick, so there is none under
    /// <see cref="fromLevel"/>, and it comes more often as the eruption grows.
    /// And it is a flash in a game where the player is tracking small rounds,
    /// so it is rare and soft: seconds apart at its busiest, one or two
    /// strokes, each a tenth of a second.
    ///
    /// **It is one light and one line.** The light is a point light with no
    /// shadows, off except during a flash; what it reaches at a flash's height
    /// is the brightness it has in the scene, the way the eruption's surge
    /// light is authored. The bolt is a line with a few kinks, bent afresh at
    /// every flash. It is opaque, so it is drawn into depth and motion like
    /// any hull, and the smoke in front of it dims it as smoke should.
    ///
    /// **It takes turns with the storm.** The usual limit for flashing light
    /// is three flashes in a second. The storm's lightning has up to three
    /// strokes and this has up to two, so neither starts while the other is
    /// lit or within a second of it (<see cref="StormLightning"/> does the
    /// same from its side).
    ///
    /// It changes no rule of the fight: the bolt is over the crater, where
    /// nothing flies, and touches nothing.
    /// </summary>
    public sealed class AshLightning : MonoBehaviour
    {
        /// <summary>The most strokes in one flash. See the note on flashing light above before raising it.</summary>
        public const int MostStrokes = 2;

        /// <summary>Seconds kept clear between this and the storm's lightning.</summary>
        public const float Turn = 1f;

        /// <summary>The most corners a bolt has, ends included.</summary>
        public const int MostCorners = 16;

        /// <summary>How much of a bolt's wander is the one bow it all shares.</summary>
        private const float BowShare = 0.6f;

        [SerializeField]
        [Tooltip("The volcano's clock. With none there is no lightning.")]
        private Eruption eruption;

        [SerializeField]
        [Tooltip("The plume, which says where a bolt can be.")]
        private LocalVolumetricFog smoke;

        [SerializeField]
        [Tooltip("The light of a flash. Its brightness here is what the strongest flash reaches; it is off between flashes.")]
        private Light flash;

        [SerializeField]
        [Tooltip("The line that draws the bolt. It is hidden between flashes.")]
        private LineRenderer bolt;

        [SerializeField]
        [Tooltip("The storm's lightning, so that the two take turns.")]
        private StormLightning storm;

        [Header("When")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How far the eruption has to have got before there is any. The plume is too thin to carry a flash before this.")]
        private float fromLevel = 0.35f;

        [SerializeField]
        [Tooltip("Seconds between flashes when they first start, least (X) and most (Y).")]
        private Vector2 gapAtFirst = new Vector2(30f, 50f);

        [SerializeField]
        [Tooltip("Seconds between flashes with the eruption at full, least (X) and most (Y).")]
        private Vector2 gapAtFull = new Vector2(12f, 24f);

        [Header("Where")]
        [SerializeField]
        [Tooltip("How high in the plume a bolt's middle is, as a share of the plume's height from its floor, least (X) and most (Y).")]
        private Vector2 height = new Vector2(0.08f, 0.3f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How far from the plume's axis a bolt's middle can be, as a share of half the plume's width.")]
        private float offAxis = 0.2f;

        [SerializeField]
        [Tooltip("How long a bolt is, least (X) and most (Y).")]
        private Vector2 length = new Vector2(4f, 7f);

        [SerializeField]
        [Range(2, MostCorners)]
        [Tooltip("How many corners a bolt has, its two ends included.")]
        private int corners = 14;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How far a corner can stand off the straight line between the ends, as a share of the bolt's length.")]
        private float wander = 0.16f;

        [Header("What")]
        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("The bolt's own glow at a flash's height.")]
        private Color boltGlow = new Color(2f, 2.5f, 3.4f);

        [SerializeField]
        [Tooltip("How bright a flash's strongest stroke is, as a share of the light's own brightness, least (X) and most (Y).")]
        private Vector2 brightness = new Vector2(0.5f, 1f);

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds a stroke takes to arrive.")]
        private float strokeRise = 0.04f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds a stroke takes to fall to about a third, once it has arrived.")]
        private float strokeFade = 0.08f;

        [SerializeField]
        [Tooltip("Seconds between one stroke and the next, least (X) and most (Y).")]
        private Vector2 strokeGap = new Vector2(0.1f, 0.18f);

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        private readonly float[] strokeAt = new float[MostStrokes];
        private readonly float[] strokeHeight = new float[MostStrokes];
        private readonly Vector3[] path = new Vector3[MostCorners];
        private int strokes;

        private Material boltMaterial;
        private float authoredLight;
        private float startedAt;
        private float nextAt = -1f;
        private float peak;
        private int struck;

        /// <summary>True from a flash's first stroke until its last has died away.</summary>
        public bool Striking { get; private set; }

        /// <summary>When the last flash ended, in game time; far in the past until one has.</summary>
        public float EndedAt { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// Seconds to the next flash: the gap for when flashes first start at
        /// <paramref name="from"/>, the gap for full at level 1, and between
        /// the two in step with the level. <paramref name="pick"/> chooses
        /// within the gap, 0 its least and 1 its most.
        /// </summary>
        public static float Gap(float level, float from, Vector2 atFirst, Vector2 atFull, float pick)
        {
            float grown = from >= 1f ? 1f : Mathf.InverseLerp(from, 1f, level);
            Vector2 gap = Vector2.Lerp(atFirst, atFull, grown);
            return Mathf.Lerp(gap.x, gap.y, Mathf.Clamp01(pick));
        }

        /// <summary>
        /// A bolt's corners from one end to the other. The ends are where
        /// they are given; every corner between is moved off the straight
        /// line, at right angles to it, by no more than <paramref name="wander"/>
        /// of the bolt's length. Most of that is one bow the whole bolt
        /// shares and the rest is each corner's own, which is what makes it
        /// a bolt and not a zigzag. The same <paramref name="seed"/> bends
        /// the same bolt, and this draws on no random stream but its own.
        /// </summary>
        public static void Bend(Vector3 from, Vector3 to, float wander, int seed, Vector3[] into, int count)
        {
            Vector3 along = to - from;
            float reach = along.magnitude * wander;

            // Two directions square to the bolt, to push its corners along.
            Vector3 across = Vector3.Cross(along, Mathf.Abs(along.normalized.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 over = Vector3.Cross(along, across).normalized;

            uint state = (uint)seed * 747796405u + 2891336453u;
            state = state * 1664525u + 1013904223u;
            float bowA = (state >> 8) / 8388608f - 1f;
            state = state * 1664525u + 1013904223u;
            float bowB = (state >> 8) / 8388608f - 1f;
            Vector2 bow = Vector2.ClampMagnitude(new Vector2(bowA, bowB), 1f) * (reach * BowShare);

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                Vector3 corner = Vector3.Lerp(from, to, t);

                if (i > 0 && i < count - 1)
                {
                    state = state * 1664525u + 1013904223u;
                    float a = (state >> 8) / 8388608f - 1f;
                    state = state * 1664525u + 1013904223u;
                    float b = (state >> 8) / 8388608f - 1f;

                    // Never further than the reach, whichever way the two fall.
                    Vector2 off = bow * Mathf.Sin(Mathf.PI * t)
                        + Vector2.ClampMagnitude(new Vector2(a, b), 1f) * (reach * (1f - BowShare));
                    corner += across * off.x + over * off.y;
                }

                into[i] = corner;
            }
        }

        private void Start()
        {
            if (flash != null)
            {
                authoredLight = flash.intensity;
                flash.enabled = false;
            }

            if (bolt != null)
            {
                // An instance, so that play never writes to the material asset.
                boltMaterial = bolt.material;
                bolt.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (Striking)
            {
                End();
            }
        }

        private void OnDestroy()
        {
            if (boltMaterial != null)
            {
                Destroy(boltMaterial);
            }
        }

        private void Update()
        {
            if (eruption == null || smoke == null || flash == null || bolt == null)
            {
                return;
            }

            float now = Time.time;
            if (!Striking)
            {
                if (eruption.Level < fromLevel)
                {
                    // Not yet, or not any more: the wait starts over when the plume is thick again.
                    nextAt = -1f;
                    return;
                }

                if (nextAt < 0f)
                {
                    nextAt = now + Gap(eruption.Level, fromLevel, gapAtFirst, gapAtFull, Random.value);
                }

                if (now < nextAt || StormIsLit(now))
                {
                    return;
                }

                Strike();
            }

            float since = now - startedAt;
            if (since >= StormLightning.Length(strokeAt, strokes, strokeRise, strokeFade))
            {
                End();
                nextAt = now + Gap(eruption.Level, fromLevel, gapAtFirst, gapAtFull, Random.value);
                return;
            }

            Show(peak * StormLightning.Glow(since, strokeAt, strokeHeight, strokes, strokeRise, strokeFade));
        }

        /// <summary>Starts a flash now, somewhere in the plume, whatever the eruption's level.</summary>
        [ContextMenu("Strike now")]
        public void Strike()
        {
            if (smoke == null || flash == null || bolt == null)
            {
                return;
            }

            // The plume as it stands now: the eruption makes it taller.
            Vector3 size = smoke.parameters.size;
            Vector3 centre = smoke.transform.position;
            float floor = centre.y - size.y * 0.5f;

            Vector2 aside = Random.insideUnitCircle * (offAxis * 0.5f * Mathf.Min(size.x, size.z));
            Vector3 middle = new Vector3(centre.x + aside.x, floor + size.y * Random.Range(height.x, height.y), centre.z + aside.y);

            // Mostly upright, leaning any way.
            Vector3 lean = Random.onUnitSphere;
            lean.y = 0f;
            Vector3 along = (Vector3.up + lean * 0.6f).normalized * (Random.Range(length.x, length.y) * 0.5f);

            int count = Mathf.Clamp(corners, 2, MostCorners);
            Bend(middle - along, middle + along, wander, ++struck * 7919 + Time.frameCount, path, count);
            bolt.positionCount = count;
            bolt.SetPositions(path);
            flash.transform.position = middle;

            strokes = Random.Range(1, MostStrokes + 1);
            int strongest = Random.Range(0, strokes);
            float when = 0f;
            for (int i = 0; i < strokes; i++)
            {
                strokeAt[i] = when;
                strokeHeight[i] = i == strongest ? 1f : Random.Range(0.35f, 0.8f);
                when += Random.Range(strokeGap.x, strokeGap.y);
            }

            peak = Random.Range(brightness.x, brightness.y);
            startedAt = Time.time;
            Striking = true;
            flash.enabled = true;
            bolt.enabled = true;
            Show(0f);
        }

        private bool StormIsLit(float now)
        {
            return storm != null && (storm.Striking || now - storm.EndedAt < Turn);
        }

        private void End()
        {
            Striking = false;
            EndedAt = Time.time;

            if (flash != null)
            {
                flash.intensity = authoredLight;
                flash.enabled = false;
            }

            if (bolt != null)
            {
                bolt.enabled = false;
            }
        }

        private void Show(float glow)
        {
            flash.intensity = authoredLight * glow;

            if (boltMaterial != null)
            {
                boltMaterial.SetColor(EmissiveColorId, boltGlow * glow);
            }
        }
    }
}
