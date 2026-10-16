using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Lightning inside the storm's wall: every few seconds a patch of cloud
    /// lights from within, in two or three quick strokes, and goes dark.
    ///
    /// 7 October 2026, the third change to come out of an outside review of
    /// the storm. A flash is the one thing that shows the wall has a depth:
    /// the cloud in front of it stands out black against the cloud it
    /// lights. It is not the environment roadmap's item 39. That is
    /// lightning in the volcano's ash, which would light the whole scene,
    /// and it is still put off.
    ///
    /// **The light is in the clouds' own tracer.** HDRP's clouds take light
    /// from the moon and the sky and nothing else, so a light in the scene
    /// would not touch them. The project's copy of the tracer
    /// (Assets/Art/Shaders/VolumetricCloudsUtilities.hlsl) adds a glow round
    /// one point, scattered by the cloud as the moonlight is, and this sets
    /// where that point is and how bright it is now. Cloud between the
    /// camera and the point hides it, so most flashes are a glow with no
    /// source to see.
    ///
    /// **It lights the clouds only.** Nothing on the island or in the lane
    /// gets brighter and no rule of the fight changes. It is silent: thunder
    /// is a sound to choose and a place in the mix, and was left for Ian to
    /// ask for.
    ///
    /// **Some flashes have a bolt** (15 October 2026, at Ian's ask): an
    /// upright line with one fork, deep inside the wall, with the flash on
    /// it. He found the first bolts small and coming from nothing, and asked
    /// for them to look inside the clouds: softened and partly hidden by the
    /// cloud in front, the cloud glowing all along them, and none of the
    /// bolt out in the open. So a bolt stands well behind the wall's inner
    /// edge, where the wall is solid from below the island's cloud floor to
    /// a few hundred metres over the arena, and the tracer lights the cloud
    /// along it as it lights it round the flash (StormBoltReach in the
    /// include). The lines are opaque, so the cloud in front covers them,
    /// and the light along them is what shows through. They are a
    /// kilometre and a half and more away, where nothing flies, so a bolt
    /// cannot be read as something to dodge.
    ///
    /// Tried and taken out the same day, each placed against the wall's
    /// face (<see cref="faceFoot"/> to <see cref="faceTop"/>), which is only
    /// where the cloud starts, so every one sat at the cloud's edge: bolts
    /// that dropped out of the wall to the horizon, which showed as a dark
    /// line in the cloud low down because nothing lit the cloud in front of
    /// them; a see-through lower part to fix that, which no cloud could
    /// hide; and bolts running sideways along the face, which Ian found
    /// worse. The camera has to draw that far: its far plane was 1,000
    /// metres and the wall starts at 1,550.
    ///
    /// **It strikes towards where the camera is looking,** within
    /// <see cref="spread"/> degrees either side. The frame shows a little
    /// over a quarter of the wall, and a flash behind the camera lights
    /// nothing the player can see. It is a lean and not a promise: the
    /// frame is 49 degrees either side, so some flashes land off its edge,
    /// and nothing checks that cloud stands where one lands.
    ///
    /// **A flash can be sharp, and is kept from being too sharp.** HDRP
    /// blends each frame of cloud with the frames before it, which by its
    /// numbers would smear a flash over a second. Measured in play, frame by
    /// frame, it does not: the clouds show nine tenths of a flash on its
    /// first frame and have lost it one frame after it ends, because the
    /// blend is clamped to what the frame in hand shows. But the clouds are
    /// traced a quarter of their pixels a frame, and a stroke that arrives
    /// whole in one frame shows that for the next three: dark squares where
    /// cloud stands in front of the light. So a stroke takes
    /// <see cref="strokeRise"/> to arrive, which is still a flash to the
    /// eye and gives the tracer its three frames at 60 frames a second. It
    /// is a time and not a count of frames: at 30 it is a frame and a half,
    /// and the squares were only looked for at 60.
    ///
    /// **No more than three strokes to a flash,** and flashes seconds
    /// apart, so this component, left to its own clock, never flashes the
    /// clouds more than three times in a second, which is the usual limit
    /// for flashing light. <see cref="Strike"/> called from outside is not
    /// counted. The volcano's own lightning (<see cref="AshLightning"/>,
    /// 8 October) is a second source of flashes, so the two take turns:
    /// neither starts while the other is lit or within a second of it.
    ///
    /// With no clouds drawn, on the Low quality tier, nothing shows.
    /// </summary>
    public sealed class StormLightning : MonoBehaviour
    {
        /// <summary>The most strokes in one flash. See the note on flashing light above before raising it.</summary>
        public const int MostStrokes = 3;

        /// <summary>The most corners a bolt or its fork has, ends included.</summary>
        public const int MostCorners = 32;

        /// <summary>The most corners of a bolt the tracer lights the cloud along; STORM_BOLT_CORNERS in the include.</summary>
        public const int CloudCorners = 12;

        [SerializeField]
        [Tooltip("The project's copy of HDRP's cloud tracer, which draws the glow: Art/Shaders/VolumetricCloudsTrace.")]
        private ComputeShader tracer;

        [SerializeField]
        [Tooltip("The lightning in the volcano's ash, so that the two take turns. Leave empty and this keeps its own time.")]
        private AshLightning ash;

        [Header("When")]
        [SerializeField]
        [Tooltip("Seconds from the end of one flash to the start of the next, least (X) and most (Y).")]
        private Vector2 gap = new Vector2(6f, 14f);

        [Header("Where")]
        [SerializeField]
        [Tooltip("The wall's inner face where it comes over the horizon: how far from the island's axis (X) and how high over the arena's floor (Y). The face leans outward, so it is a line from here to Face Top.")]
        private Vector2 faceFoot = new Vector2(1550f, 220f);

        [SerializeField]
        [Tooltip("The wall's inner face at its shoulder: how far out (X) and how high (Y).")]
        private Vector2 faceTop = new Vector2(1800f, 420f);

        [SerializeField]
        [Tooltip("How far under the face a flash is, least (X) and most (Y). At the face it is a bright patch of cloud; deeper it is a glow behind the cloud in front.")]
        private Vector2 depth = new Vector2(0f, 120f);

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Degrees either side of where the camera looks that a flash can be. The frame is about 48 either side; 180 is anywhere round the wall.")]
        private float spread = 60f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("How far a flash's light reaches through the cloud: a quarter of it is left this far away.")]
        private float reach = 200f;

        [Header("What")]
        [SerializeField]
        [ColorUsage(false)]
        private Color colour = new Color(0.75f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("How bright a flash's strongest stroke is at its heart, against the moonlight on the same cloud, least (X) and most (Y).")]
        private Vector2 brightness = new Vector2(3f, 6f);

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds a stroke takes to arrive. At 0 its first frames show squares in the cloud in front of it.")]
        private float strokeRise = 0.05f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds a stroke takes to fall to about a third, once it has arrived.")]
        private float strokeFade = 0.07f;

        [SerializeField]
        [Tooltip("Seconds between one stroke and the next, least (X) and most (Y).")]
        private Vector2 strokeGap = new Vector2(0.12f, 0.22f);

        [Header("Bolt")]
        [SerializeField]
        [Tooltip("The line that draws a bolt. It is hidden between flashes. Leave empty for flashes with no bolt.")]
        private LineRenderer bolt;

        [SerializeField]
        [Tooltip("The line that draws the bolt's fork, hidden with the bolt.")]
        private LineRenderer fork;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The share of flashes that have a bolt.")]
        private float boltShare = 0.5f;

        [SerializeField]
        [Tooltip("How far from the island's axis a bolt stands, least (X) and most (Y). The wall starts about 1,050 out and is solid by about 1,800; the deeper, the more cloud in front.")]
        private Vector2 boltOut = new Vector2(1500f, 1900f);

        [SerializeField]
        [Tooltip("How high over the arena's floor a bolt's top is, least (X) and most (Y). The wall's tops are 250 to 600 over it.")]
        private Vector2 boltTop = new Vector2(120f, 240f);

        [SerializeField]
        [Tooltip("How high over the arena's floor a bolt's lower end is, least (X) and most (Y). Under 0 is under the horizon, in the cloud the island floats over.")]
        private Vector2 boltFoot = new Vector2(-200f, 0f);

        [SerializeField]
        [Min(0f)]
        [Tooltip("How far, level, a bolt's lower end can be from under its top, any way.")]
        private float boltLean = 150f;

        [SerializeField]
        [Range(2, MostCorners)]
        [Tooltip("How many corners a bolt has, its two ends included.")]
        private int boltCorners = 18;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How far a corner can stand off the straight line between a bolt's ends, as a share of its length. Much over 0.1 and it is a saw, not a bolt.")]
        private float boltWander = 0.09f;

        [SerializeField]
        [Tooltip("Where down the bolt the fork leaves it, least (X) and most (Y), 0 at the top and 1 at the end.")]
        private Vector2 forkFrom = new Vector2(0.25f, 0.55f);

        [SerializeField]
        [Tooltip("How long the fork is, as a share of the bolt's length, least (X) and most (Y).")]
        private Vector2 forkLength = new Vector2(0.25f, 0.45f);

        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("The lines' own glow at a flash's brightest.")]
        private Color boltGlow = new Color(16f, 20f, 27f);

        [SerializeField]
        [Min(1f)]
        [Tooltip("How far the bolt's light reaches through the cloud round it: a quarter of it is left this far from the line.")]
        private float boltReach = 60f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How bright the cloud is at the bolt, against the cloud at the flash's heart.")]
        private float boltLight = 2.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How bright the flash has to be, against its strongest stroke, for the bolt's lines to show. They are opaque, so under it they would draw darker than the cloud behind them.")]
        private float boltShowsFrom = 0.2f;

        private static readonly int FlashId = Shader.PropertyToID("_StormFlash");
        private static readonly int FlashLightId = Shader.PropertyToID("_StormFlashLight");
        private static readonly int BoltCornersId = Shader.PropertyToID("_StormBoltCorners");
        private static readonly int BoltId = Shader.PropertyToID("_StormBolt");
        private static readonly int BoltBoundsId = Shader.PropertyToID("_StormBoltBounds");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        private readonly float[] strokeAt = new float[MostStrokes];
        private readonly float[] strokeHeight = new float[MostStrokes];
        private readonly Vector3[] path = new Vector3[MostCorners];
        private readonly Vector4[] cloudCorners = new Vector4[CloudCorners];
        private int strokes;

        private Camera view;
        private Material boltMaterial;
        private float startedAt;
        private float nextAt;
        private float peak;
        private bool bolting;
        private int struck;

        /// <summary>True from a flash's first stroke until its last has died away.</summary>
        public bool Striking { get; private set; }

        /// <summary>When the last flash ended, in game time; far in the past until one has.</summary>
        public float EndedAt { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// How bright a flash is, 0 to 1, a time after it began: each stroke
        /// arrives over <paramref name="rise"/> and then dies away, and the
        /// brightest one still burning is what shows.
        /// </summary>
        public static float Glow(float since, float[] at, float[] heights, int count, float rise, float fade)
        {
            float glow = 0f;
            for (int i = 0; i < count; i++)
            {
                float burning = since - at[i];
                if (burning < 0f)
                {
                    continue;
                }

                float stroke = burning < rise ? burning / rise : Mathf.Exp(-(burning - rise) / fade);
                glow = Mathf.Max(glow, heights[i] * stroke);
            }

            return glow;
        }

        /// <summary>
        /// Where a flash is: how far out (X) and how high (Y), for a place
        /// along the wall's face, 0 at its foot to 1 at its top, and a depth
        /// under it. The wall thickens outward, so deeper is further out.
        /// </summary>
        public static Vector2 Place(Vector2 foot, Vector2 top, float along, float under)
        {
            Vector2 onTheFace = Vector2.Lerp(foot, top, along);
            return new Vector2(onTheFace.x + under, onTheFace.y - under * 0.5f);
        }

        /// <summary>How long a flash lasts: until its last stroke is under a hundredth.</summary>
        public static float Length(float[] at, int count, float rise, float fade)
        {
            return at[count - 1] + rise + fade * 4.6f;
        }

        /// <summary>
        /// A bolt's two ends: its top <paramref name="away"/> metres out
        /// along <paramref name="towards"/> and <paramref name="top"/> high,
        /// and its lower end <paramref name="foot"/> high, moved level from
        /// under the top by <paramref name="lean"/> (x along
        /// <paramref name="towards"/>, y across it).
        /// </summary>
        public static void Ends(Vector3 towards, float away, float top, float foot, Vector2 lean, out Vector3 from, out Vector3 to)
        {
            Vector3 across = Vector3.Cross(Vector3.up, towards);
            from = towards * away + Vector3.up * top;
            to = towards * (away + lean.x) + across * lean.y + Vector3.up * foot;
        }

        /// <summary>
        /// Where a fork leaves a bolt and where it ends. It leaves at the
        /// corner <paramref name="from"/> of the way along, and runs
        /// <paramref name="length"/> mostly down and partly along the bolt,
        /// forwards or back as <paramref name="side"/> says. The ends of the
        /// bolt are never where a fork leaves.
        /// </summary>
        public static void Fork(Vector3[] boltPath, int count, float from, float length, float side, out Vector3 start, out Vector3 end)
        {
            int corner = Mathf.Clamp(Mathf.RoundToInt(from * (count - 1)), 1, Mathf.Max(1, count - 2));
            start = boltPath[corner];

            Vector3 along = boltPath[count - 1] - boltPath[0];
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.right;
            end = start + (along * (side < 0f ? -0.5f : 0.5f) + Vector3.down * 0.85f).normalized * length;
        }

        /// <summary>
        /// Whether a bolt's lines show at <paramref name="glow"/>, the
        /// flash's brightness against its strongest stroke. The lines are
        /// opaque and black under their own glow, so a dim line draws darker
        /// than the moonlit cloud behind it: they show only while a stroke is
        /// bright, and blink on with each stroke as a real bolt does. The
        /// cloud along the bolt keeps fading with the flash.
        /// </summary>
        public static bool LineShows(float glow, float from)
        {
            return glow > 0f && glow >= from;
        }

        /// <summary>
        /// Up to <c>into.Length</c> of a path's corners, evenly spaced along
        /// it, both ends kept, as the tracer takes them. It counts the
        /// corners written.
        /// </summary>
        public static int Thin(Vector3[] path, int count, Vector4[] into)
        {
            int kept = Mathf.Min(count, into.Length);
            for (int i = 0; i < kept; i++)
            {
                int corner = kept > 1 ? Mathf.RoundToInt(i * (count - 1) / (float)(kept - 1)) : 0;
                into[i] = path[corner];
            }

            return kept;
        }

        private void Start()
        {
            if (bolt != null)
            {
                // An instance, so that play never writes to the material asset; the fork shares it.
                boltMaterial = bolt.material;
                if (fork != null)
                {
                    fork.sharedMaterial = boltMaterial;
                }
            }

            HideBolt();
        }

        private void OnEnable()
        {
            view = Camera.main;
            nextAt = Time.time + Random.Range(gap.x, gap.y);
        }

        private void OnDisable()
        {
            // The tracer is an asset: what is set on it outlives play in the editor.
            Striking = false;
            Light(0f);
            HideBolt();
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
            if (tracer == null)
            {
                return;
            }

            float now = Time.time;
            if (!Striking)
            {
                if (now < nextAt || (ash != null && (ash.Striking || now - ash.EndedAt < AshLightning.Turn)))
                {
                    return;
                }

                Strike();
            }

            float since = now - startedAt;
            if (since >= Length(strokeAt, strokes, strokeRise, strokeFade))
            {
                Striking = false;
                EndedAt = now;
                nextAt = now + Random.Range(gap.x, gap.y);
                Light(0f);
                HideBolt();
                return;
            }

            float glow = Glow(since, strokeAt, strokeHeight, strokes, strokeRise, strokeFade);
            Light(peak * glow);

            if (bolting)
            {
                bool shows = LineShows(glow, boltShowsFrom);
                ShowLines(shows);
                if (shows)
                {
                    BoltGlow(boltGlow * (glow * peak / Mathf.Max(brightness.y, 1e-3f)));
                }
            }
        }

        /// <summary>Starts a flash now, somewhere in the wall the camera can see.</summary>
        [ContextMenu("Strike now")]
        public void Strike()
        {
            if (tracer == null)
            {
                return;
            }

            if (view == null)
            {
                view = Camera.main;
            }

            Vector3 ahead = view != null ? view.transform.forward : Vector3.forward;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : Vector3.forward;

            Vector3 towards = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * ahead;
            bolting = bolt != null && Random.value < boltShare;
            Vector3 from = Vector3.zero;
            Vector3 to = Vector3.zero;
            Vector3 at;
            if (bolting)
            {
                // Upright, deep in the wall, with the flash a third of the way down it.
                Ends(towards, Random.Range(boltOut.x, boltOut.y), Random.Range(boltTop.x, boltTop.y), Random.Range(boltFoot.x, boltFoot.y),
                    Random.insideUnitCircle * boltLean, out from, out to);
                at = Vector3.Lerp(from, to, 0.35f);
            }
            else
            {
                Vector2 place = Place(faceFoot, faceTop, Random.value, Random.Range(depth.x, depth.y));
                at = towards * place.x + Vector3.up * place.y;
            }

            tracer.SetVector(FlashId, new Vector4(at.x, at.y, at.z, reach));

            if (bolting)
            {
                int count = Mathf.Clamp(boltCorners, 2, MostCorners);
                int seed = ++struck * 7919 + Time.frameCount;
                AshLightning.Bend(from, to, boltWander, seed, path, count);
                bolt.positionCount = count;
                bolt.SetPositions(path);
                LightTheCloudAlong(count);

                if (fork != null)
                {
                    float length = Vector3.Distance(from, to) * Random.Range(forkLength.x, forkLength.y);
                    Fork(path, count, Random.Range(forkFrom.x, forkFrom.y), length, Random.value - 0.5f, out Vector3 forkStart, out Vector3 forkEnd);
                    int forkCount = Mathf.Max(2, count / 2);
                    AshLightning.Bend(forkStart, forkEnd, boltWander * 1.5f, seed + 1, path, forkCount);
                    fork.positionCount = forkCount;
                    fork.SetPositions(path);
                }
            }

            // A bolt's lines wait for its first stroke to be bright (LineShows).
            if (bolting)
            {
                ShowLines(false);
            }
            else
            {
                HideBolt();
            }

            strokes =Random.Range(2, MostStrokes + 1);
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
        }

        private void HideBolt()
        {
            BoltGlow(Color.black);
            if (tracer != null)
            {
                tracer.SetVector(BoltId, Vector4.zero);
            }

            ShowLines(false);
        }

        private void ShowLines(bool show)
        {
            if (bolt != null && bolt.enabled != show)
            {
                bolt.enabled = show;
            }

            if (fork != null && fork.enabled != show)
            {
                fork.enabled = show;
            }
        }

        private void BoltGlow(Color glow)
        {
            if (boltMaterial != null)
            {
                boltMaterial.SetColor(EmissiveColorId, glow);
            }

        }

        /// <summary>Hands the tracer the bolt's corners, so the cloud round the whole bolt lights with the flash.</summary>
        private void LightTheCloudAlong(int count)
        {
            int kept = Thin(path, count, cloudCorners);
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < kept; i++)
            {
                centre += (Vector3)cloudCorners[i];
            }

            centre /= kept;
            float radius = 0f;
            for (int i = 0; i < kept; i++)
            {
                radius = Mathf.Max(radius, Vector3.Distance(centre, cloudCorners[i]));
            }

            // At three reaches the light is a hundredth of what it is at the line.
            radius += boltReach * 3f;
            tracer.SetVectorArray(BoltCornersId, cloudCorners);
            tracer.SetVector(BoltId, new Vector4(kept, boltReach, boltLight, 0f));
            tracer.SetVector(BoltBoundsId, new Vector4(centre.x, centre.y, centre.z, radius));
        }

        private void Light(float bright)
        {
            if (tracer != null)
            {
                tracer.SetVector(FlashLightId, new Vector4(colour.r * bright, colour.g * bright, colour.b * bright, 0f));
            }
        }
    }
}
