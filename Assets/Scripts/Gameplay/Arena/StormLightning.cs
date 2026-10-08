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
    /// gets brighter, no rule of the fight changes, and there is no bolt, so
    /// it cannot be read as something to dodge. It is silent: thunder is a
    /// sound to choose and a place in the mix, and was left for Ian to ask
    /// for.
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
    /// counted, and neither is a second component.
    ///
    /// With no clouds drawn, on the two lowest quality tiers, nothing shows.
    /// </summary>
    public sealed class StormLightning : MonoBehaviour
    {
        /// <summary>The most strokes in one flash. See the note on flashing light above before raising it.</summary>
        public const int MostStrokes = 3;

        [SerializeField]
        [Tooltip("The project's copy of HDRP's cloud tracer, which draws the glow: Art/Shaders/VolumetricCloudsTrace.")]
        private ComputeShader tracer;

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

        private static readonly int FlashId = Shader.PropertyToID("_StormFlash");
        private static readonly int FlashLightId = Shader.PropertyToID("_StormFlashLight");

        private readonly float[] strokeAt = new float[MostStrokes];
        private readonly float[] strokeHeight = new float[MostStrokes];
        private int strokes;

        private Camera view;
        private float startedAt;
        private float nextAt;
        private float peak;

        /// <summary>True from a flash's first stroke until its last has died away.</summary>
        public bool Striking { get; private set; }

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
                if (now < nextAt)
                {
                    return;
                }

                Strike();
            }

            float since = now - startedAt;
            if (since >= Length(strokeAt, strokes, strokeRise, strokeFade))
            {
                Striking = false;
                nextAt = now + Random.Range(gap.x, gap.y);
                Light(0f);
                return;
            }

            Light(peak * Glow(since, strokeAt, strokeHeight, strokes, strokeRise, strokeFade));
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
            Vector2 place = Place(faceFoot, faceTop, Random.value, Random.Range(depth.x, depth.y));
            Vector3 at = towards * place.x + Vector3.up * place.y;
            tracer.SetVector(FlashId, new Vector4(at.x, at.y, at.z, reach));

            strokes = Random.Range(2, MostStrokes + 1);
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

        private void Light(float bright)
        {
            if (tracer != null)
            {
                tracer.SetVector(FlashLightId, new Vector4(colour.r * bright, colour.g * bright, colour.b * bright, 0f));
            }
        }
    }
}
