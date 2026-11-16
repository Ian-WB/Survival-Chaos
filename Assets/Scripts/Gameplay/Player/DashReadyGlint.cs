using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A ring that closes in on the ship the moment the dash comes back.
    ///
    /// The dash is the only answer to the boss's ram, and its cooldown is ten
    /// seconds; the bar that shows it is a short line in the corner, and a
    /// player dodging is looking at the ship. This puts "ready" where they are
    /// looking, the way <see cref="DeflectorShield"/> puts a blocked hit there.
    ///
    /// Closing in rather than bursting out, so it cannot be mistaken for the
    /// deflector's ring taking a hit, and in the dash bar's cyan. Opaque and
    /// built the deflector's way for the same reason: anything transparent and
    /// fast near the camera smears under FSR. So it cannot fade; it shrinks to
    /// nothing and is gone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashReadyGlint : MonoBehaviour
    {
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");

        [SerializeField]
        [Tooltip("Opaque HDRP/Unlit material the ring is drawn with, tinted per instance. The " +
                 "deflector ring's PickupGlow.")]
        private Material material;

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("The ring's glow: the dash bar's cyan, bright enough to read against the lava.")]
        private Color color = new Color(0.6f, 2.2f, 3f);

        [SerializeField]
        [Min(0.05f)]
        [Tooltip("Ring radius in world units at scale 1. The ship is about 0.4 across.")]
        private float radius = 0.425f;

        [SerializeField]
        [Min(0.005f)]
        [Tooltip("Width of the ring's line in world units.")]
        private float width = 0.045f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("How large the ring starts, as a multiple of its radius.")]
        private float startScale = 2.4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How small it gets before it goes, as a multiple of its radius.")]
        private float endScale = 0.6f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Seconds the ring takes to close in.")]
        private float seconds = 0.28f;

        private PlayerDash dash;
        private Transform ring;
        private MeshRenderer ringRenderer;
        private MaterialPropertyBlock block;
        private Mesh ringMesh;

        private bool wasReady = true;
        private float started = float.NegativeInfinity;

        private void Awake()
        {
            dash = GetComponent<PlayerDash>();
            if (dash == null)
            {
                dash = GetComponentInChildren<PlayerDash>();
            }

            if (dash == null || material == null)
            {
                Debug.LogWarning(
                    "DashReadyGlint needs a PlayerDash on the ship and a material, so the dash " +
                    "coming back will show on the HUD only.", this);
                enabled = false;
                return;
            }

            var host = new GameObject("Dash Ready Ring");
            ring = host.transform;
            ring.SetParent(transform, false);

            ringMesh = DeflectorShield.RingMesh(radius, width);
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
        /// In LateUpdate so the ring faces the camera from where the camera
        /// ended up this frame, after it has followed the ship.
        /// </summary>
        private void LateUpdate()
        {
            bool ready = dash.ReadyFraction >= 1f;
            float now = Time.time;

            if (ready && !wasReady)
            {
                started = now;
            }

            wasReady = ready;

            float t = (now - started) / seconds;
            if (t < 0f || t >= 1f || RunOutcome.Decided)
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

            float eased = 1f - (1f - t) * (1f - t);
            ring.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);

            ringRenderer.GetPropertyBlock(block);
            block.SetColor(EmissiveColor, color);
            block.SetColor(UnlitColor, color);
            ringRenderer.SetPropertyBlock(block);
        }
    }
}
