using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The Deflector as it shows on the ship: a thin glowing ring while the
    /// charge is up, which bursts outward the moment it takes a hit and pops
    /// back in when the charge returns.
    ///
    /// The HUD bar already says all of this, but a player dodging is looking at
    /// the ship, not at the corner of the screen. A blocked hit used to show
    /// nothing where it happened - no hull change, no hit effect - so from the
    /// ship's side it looked exactly like a round that had missed. The burst is
    /// what makes it read as the upgrade doing its job.
    ///
    /// Opaque on purpose. Under FSR anything transparent and fast near the
    /// camera smears unless it is rigid geometry writing its own motion vectors,
    /// which is what the player's round streaks had to become. An opaque
    /// MeshRenderer in Object motion vector mode is that without a shader graph,
    /// and the pickups' unlit glow material is already one. The cost is that the
    /// ring cannot fade: it bursts at full brightness and is gone.
    ///
    /// Built in code under the player, so the material is the only thing to wire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeflectorShield : MonoBehaviour
    {
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");

        [SerializeField]
        [Tooltip("Opaque HDRP/Unlit material the ring is drawn with, tinted per instance through a " +
                 "property block the way the pickups are. PickupGlow is what the Game scene uses.")]
        private Material material;

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("The ring's glow. Matches the Deflector pickup, so the thing on the ring and the " +
                 "thing around the ship read as the same upgrade.")]
        private Color color = new Color(2.6f, 2.8f, 3f);

        [SerializeField]
        [Min(0.05f)]
        [Tooltip("Ring radius in world units. The ship is about 0.4 across.")]
        private float radius = 0.425f;

        [SerializeField]
        [Min(0.005f)]
        [Tooltip("Width of the ring's line in world units.")]
        private float width = 0.0375f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("How large the ring grows as it bursts, as a multiple of its resting size.")]
        private float burstScale = 2.5f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Seconds the burst takes, from the blocked hit to the ring disappearing.")]
        private float burstSeconds = 0.2f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Seconds the ring takes to grow back to size when the charge returns.")]
        private float returnSeconds = 0.15f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How much the charged ring's glow breathes, as a share of its colour. 0 holds it " +
                 "steady; a little movement is what keeps a ring that is up for minutes from " +
                 "reading as part of the ship.")]
        private float breathe = 0.25f;

        private const int Segments = 48;

        private Player player;
        private Transform ring;
        private MeshRenderer ringRenderer;
        private MaterialPropertyBlock block;

        /// <summary>
        /// Built here, so destroyed here: the ring's object goes with the ship,
        /// but a mesh made in code belongs to no object and outlives it.
        /// </summary>
        private Mesh ringMesh;

        private bool wasCharged;
        private float burstStarted = float.NegativeInfinity;
        private float returnStarted = float.NegativeInfinity;

        private void Awake()
        {
            player = GetComponent<Player>();

            if (player == null || material == null)
            {
                Debug.LogWarning(
                    "DeflectorShield needs a Player on the same object and a material, so the " +
                    "deflector will show on the HUD only.", this);
                enabled = false;
                return;
            }

            BuildRing();
        }

        private void BuildRing()
        {
            var host = new GameObject("Deflector Ring");
            ring = host.transform;
            ring.SetParent(transform, false);

            ringMesh = RingMesh(radius, width);
            host.AddComponent<MeshFilter>().sharedMesh = ringMesh;

            ringRenderer = host.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = material;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            ringRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            ringRenderer.enabled = false;

            block = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            if (ringMesh != null)
            {
                Destroy(ringMesh);
            }
        }

        /// <summary>
        /// A flat band around the origin in the XY plane, wound both ways so it
        /// draws whichever side the material culls.
        /// </summary>
        private static Mesh RingMesh(float radius, float width)
        {
            var vertices = new Vector3[Segments * 2];
            var triangles = new int[Segments * 12];
            float inner = radius - width * 0.5f;
            float outer = radius + width * 0.5f;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2, b = i * 2 + 1;
                int c = (i + 1) % Segments * 2, d = c + 1;
                int t = i * 12;

                // Facing the camera, which the ring is turned to look along.
                triangles[t] = a; triangles[t + 1] = d; triangles[t + 2] = b;
                triangles[t + 3] = a; triangles[t + 4] = c; triangles[t + 5] = d;

                // And the other way, for a material that culls the other side.
                triangles[t + 6] = a; triangles[t + 7] = b; triangles[t + 8] = d;
                triangles[t + 9] = a; triangles[t + 10] = d; triangles[t + 11] = c;
            }

            var mesh = new Mesh { name = "Deflector Ring", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// In LateUpdate so the ring faces the camera from where the camera ended
        /// up this frame, after it has followed the ship.
        /// </summary>
        private void LateUpdate()
        {
            bool held = player.HasDeflector;
            bool charged = held && player.DeflectorCharged;
            float now = Time.time;

            if (charged && !wasCharged)
            {
                returnStarted = now;
            }
            else if (!charged && wasCharged)
            {
                burstStarted = now;
            }

            wasCharged = charged;

            float bursting = (now - burstStarted) / burstSeconds;
            float scale;
            float glow;

            if (charged)
            {
                float back = Mathf.Clamp01((now - returnStarted) / returnSeconds);
                scale = Mathf.Lerp(0.5f, 1f, EaseOut(back));
                glow = 1f - breathe * 0.5f * (1f + Mathf.Sin(now * Mathf.PI * 2f * 1.2f));
            }
            else if (held && bursting < 1f)
            {
                scale = Mathf.Lerp(1f, burstScale, EaseOut(bursting));
                glow = 1f;
            }
            else
            {
                ringRenderer.enabled = false;
                return;
            }

            ringRenderer.enabled = true;

            Camera view = Camera.main;
            if (view != null)
            {
                ring.rotation = view.transform.rotation;
            }

            ring.localScale = Vector3.one * scale;

            Color tint = color * glow;
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(EmissiveColor, tint);
            block.SetColor(UnlitColor, tint);
            ringRenderer.SetPropertyBlock(block);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
