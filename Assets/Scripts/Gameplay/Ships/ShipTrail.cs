using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A streak behind a ship that grows with its speed and lies along the
    /// way it came.
    ///
    /// Not a trail renderer, for the reason the player's rounds and every
    /// flare in the game give: under FSR a transparent ribbon left in the
    /// world has no motion of its own to report and is dragged by whatever
    /// is behind it. This is one rigid mesh that rides with the ship and
    /// writes its own motion vectors. It cannot curve, and over its length
    /// the ring barely does.
    ///
    /// The engine flare says the engine is on; this says how fast and which
    /// way, which is why a dash draws it out to several ship lengths and a
    /// climb tilts it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipTrail : MonoBehaviour
    {
        /// <summary>How quickly the measured velocity follows, so one odd frame does not kink the streak.</summary>
        private const float Response = 12f;

        /// <summary>Half the hull's length and height in the model's space, guns and rocket included.</summary>
        public const float HullHalfLength = 0.25f;
        public const float HullHalfHeight = 0.12f;

        /// <summary>
        /// How much of its brightness the streak keeps flying tail-first.
        /// The retro flares on the nose already say the ship is backing up,
        /// and a full streak off the nose read as a beam being fired.
        /// </summary>
        public const float TailFirstBrightness = 0.45f;

        private Vector3 anchor;

        private Transform body;
        private Renderer streak;
        private MaterialPropertyBlock block;

        private float lengthPerSpeed;
        private float maxLength;
        private float width;
        private float brightness;
        private float minSpeed;
        private float restScale = 1f;

        private Vector3 lastPosition;
        private bool hasLast;
        private Vector3 velocity;

        /// <summary>
        /// Builds a streak under <paramref name="host"/>, starting at
        /// <paramref name="anchor"/> in its space, that reads its speed off
        /// <paramref name="body"/>. Lengths and the width are world units at
        /// the body's authored size. Null without a mesh or a material.
        /// </summary>
        public static ShipTrail Build(Transform host, Transform body, Mesh mesh, Material material,
            Vector3 anchor, float lengthPerSpeed, float maxLength, float width, float brightness, float minSpeed)
        {
            Renderer streak = ShipFlare.Build(host, "Speed Trail", mesh, material, anchor, Quaternion.identity);

            if (streak == null || body == null)
            {
                return null;
            }

            ShipTrail trail = streak.gameObject.AddComponent<ShipTrail>();
            trail.body = body;
            trail.anchor = anchor;
            trail.streak = streak;
            trail.block = new MaterialPropertyBlock();
            trail.lengthPerSpeed = lengthPerSpeed;
            trail.maxLength = maxLength;
            trail.width = width;
            trail.brightness = brightness;
            trail.minSpeed = minSpeed;
            trail.restScale = Mathf.Max(0.0001f, body.lossyScale.x);

            return trail;
        }

        /// <summary>World length of the streak at a speed, in world units a second.</summary>
        public static float Length(float speed, float minSpeed, float lengthPerSpeed, float maxLength)
        {
            return Mathf.Clamp((speed - minSpeed) * lengthPerSpeed, 0f, maxLength);
        }

        /// <summary>A reused ship does not draw a streak back to where it last died.</summary>
        /// <summary>
        /// Where the streak starts, from the hull's middle: the edge of the
        /// hull on the side the ship is coming from. It started at the middle
        /// until 16 November 2026, so a ship flying tail-first wore it
        /// through its canopy and out between its guns.
        /// </summary>
        public static Vector2 Edge(Vector2 behind, float halfLength, float halfHeight)
        {
            float x = behind.x / Mathf.Max(0.0001f, halfLength);
            float y = behind.y / Mathf.Max(0.0001f, halfHeight);
            float reach = Mathf.Sqrt(x * x + y * y);

            return reach < 0.0001f ? Vector2.zero : behind / reach;
        }

        /// <summary>The share of its brightness a streak keeps, by how far it points out of the nose (0 to 1).</summary>
        public static float Dim(float towardNose)
        {
            return Mathf.Lerp(1f, TailFirstBrightness, Mathf.Clamp01(towardNose));
        }

        private void OnEnable()
        {
            hasLast = false;
            velocity = Vector3.zero;

            if (streak != null)
            {
                streak.enabled = false;
            }
        }

        /// <summary>After the ship has moved and its model has been posed.</summary>
        private void LateUpdate()
        {
            float delta = Time.deltaTime;

            if (delta <= 0f || body == null)
            {
                return;
            }

            Vector3 now = body.position;

            if (hasLast)
            {
                Vector3 measured = (now - lastPosition) / delta;
                velocity = Vector3.Lerp(velocity, measured, 1f - Mathf.Exp(-Response * delta));
            }

            lastPosition = now;
            hasLast = true;

            float speed = velocity.magnitude;
            float length = Length(speed, minSpeed, lengthPerSpeed, maxLength);

            if (length <= 0.001f)
            {
                streak.enabled = false;
                return;
            }

            // The plume mesh runs along its own +X, laid here along the way
            // the ship came from.
            transform.rotation = Quaternion.FromToRotation(Vector3.right, -velocity / speed);

            Transform host = transform.parent;

            // From the hull's trailing edge, in the model's own space, where
            // the nose is +X.
            float towardNose = 0f;

            if (host != null)
            {
                Vector3 behind = host.InverseTransformDirection(-velocity / speed);
                Vector2 edge = Edge(new Vector2(behind.x, behind.y), HullHalfLength, HullHalfHeight);

                transform.localPosition = anchor + new Vector3(edge.x, edge.y, 0f);
                towardNose = behind.x;
            }

            // A ship still growing in draws a streak its own size.
            float hostScale = host != null ? Mathf.Max(0.0001f, Mathf.Abs(host.lossyScale.x)) : 1f;
            float grown = Mathf.Abs(body.lossyScale.x) / restScale;
            float toLocal = grown / hostScale;

            ShipFlare.Set(
                streak, block,
                new Vector3(length * toLocal, width * toLocal, width * toLocal),
                brightness * Dim(towardNose) * Mathf.Clamp01(length / Mathf.Max(0.0001f, maxLength) + 0.35f));
        }
    }
}
