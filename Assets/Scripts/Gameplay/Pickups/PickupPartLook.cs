using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// What an upgrade offer looks like since 17 November 2026: the ship part
    /// the pick adds, a few times its size on the hull, turning slowly over a
    /// soft glow in the upgrade's colour, with gold dots under it for the
    /// pick it is. Built once per pooled pickup, under it, and switched on
    /// for each life that carries a part.
    ///
    /// It only draws. <see cref="Pickup"/> decides when it shows, flies and
    /// breaks up.
    /// </summary>
    internal sealed class PickupPartLook
    {
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");

        /// <summary>The tier dots' gold, the HUD's #FFD140, bright enough to bloom a little.</summary>
        private static readonly Color DotGold = new Color(1f, 0.82f, 0.25f) * 2.2f;

        private static Mesh disc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            disc = null;
        }

        private readonly Transform holder;
        private readonly Transform body;
        private readonly MeshFilter filter;
        private readonly MeshRenderer part;
        private readonly Transform glowHost;
        private readonly MeshRenderer glow;
        private readonly Transform dotHost;
        private readonly MeshRenderer[] dots;
        private readonly MaterialPropertyBlock glowBlock = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock dotBlock = new MaterialPropertyBlock();

        private Color color;
        private float radius;
        private int dotCount;

        /// <summary>The part's own size on the hull, the model's scale.</summary>
        public Vector3 HullScale { get; private set; } = Vector3.one;

        /// <summary>Where the part's middle is, which is where its mount is once it is on.</summary>
        public Transform Holder => holder;

        /// <summary>The part, for its flicker.</summary>
        public Renderer Part => part;

        /// <summary>Half the part's length as shown, in world units.</summary>
        public float Radius => radius;

        public PickupPartLook(Transform root, Material glowMaterial, Mesh dotMesh, Material dotMaterial)
        {
            holder = new GameObject("Part").transform;
            holder.SetParent(root, false);

            body = new GameObject("Mesh").transform;
            body.SetParent(holder, false);
            filter = body.gameObject.AddComponent<MeshFilter>();
            part = body.gameObject.AddComponent<MeshRenderer>();
            part.motionVectorGenerationMode = MotionVectorGenerationMode.Object;

            glowHost = new GameObject("Glow").transform;
            glowHost.SetParent(root, false);
            glowHost.gameObject.AddComponent<MeshFilter>().sharedMesh = Disc();
            glow = glowHost.gameObject.AddComponent<MeshRenderer>();
            glow.sharedMaterial = glowMaterial;
            Quiet(glow);

            dotHost = new GameObject("Tier").transform;
            dotHost.SetParent(root, false);
            dots = new MeshRenderer[PartOffer.MostDots];

            for (int i = 0; i < dots.Length; i++)
            {
                var dot = new GameObject("Dot " + (i + 1));
                dot.transform.SetParent(dotHost, false);
                dot.AddComponent<MeshFilter>().sharedMesh = dotMesh;
                dots[i] = dot.AddComponent<MeshRenderer>();
                dots[i].sharedMaterial = dotMaterial;
                Quiet(dots[i]);
            }

            Hide();
        }

        /// <summary>
        /// Dresses the pickup as a stage of the ship, sized so its half-length
        /// is about <paramref name="target"/> world units, between
        /// <paramref name="least"/> and <paramref name="most"/> times its size
        /// on the hull. A pair (two guns, two fins) shows one of the two.
        /// Lit the way the hull is, so it is the same part in the same colours.
        /// </summary>
        public void Show(ShipPartStage stage, Transform mount, int pick, Color tint, float target, float least, float most)
        {
            full = stage.mesh;
            Mesh shown = PartOffer.OneOfPair(stage.mesh);
            filter.sharedMesh = shown;
            part.sharedMaterials = stage.materials;

            MeshRenderer fitted = mount != null ? mount.GetComponentInChildren<MeshRenderer>(true) : null;
            if (fitted != null)
            {
                part.renderingLayerMask = fitted.renderingLayerMask;
                part.lightProbeUsage = fitted.lightProbeUsage;
                part.shadowCastingMode = fitted.shadowCastingMode;
                part.receiveShadows = fitted.receiveShadows;
            }

            HullScale = mount != null && mount.parent != null ? mount.parent.lossyScale : Vector3.one;
            float own = Mathf.Max(0.001f, shown.bounds.extents.magnitude * HullScale.x);
            Size = Mathf.Clamp(target / own, least, most);

            body.localPosition = -shown.bounds.center;
            holder.localPosition = Vector3.zero;
            holder.localScale = HullScale * Size;
            radius = own * Size;

            color = tint;
            dotCount = PartOffer.DotsFor(pick);

            holder.gameObject.SetActive(true);
            part.enabled = true;
            glow.enabled = glow.sharedMaterial != null;

            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].enabled = i < dotCount;
                dots[i].GetPropertyBlock(dotBlock);
                dotBlock.SetColor(EmissiveColorId, DotGold);
                dotBlock.SetColor(UnlitColorId, DotGold);
                dots[i].SetPropertyBlock(dotBlock);
            }

            Glow(1f, 1f);
        }

        /// <summary>
        /// For the flight: the half of a pair is drawn from the whole stage's
        /// middle from here on, so it lands on its own side of the mount. The
        /// root moves the other way, so nothing jumps on screen. Returns the
        /// distance the root moved.
        /// </summary>
        public Vector3 CentreOnMount()
        {
            Mesh shown = filter.sharedMesh;
            if (full == null || shown == null || shown == full)
            {
                return Vector3.zero;
            }

            Vector3 before = body.position;
            body.localPosition = -full.bounds.center;
            Vector3 moved = body.position - before;
            holder.parent.position -= moved;
            return -moved;
        }

        /// <summary>The part's size on the ring against its size on the hull.</summary>
        public float Size { get; private set; } = 1f;

        private Mesh full;

        public void Hide()
        {
            // Torn down with its scene: OnDisable can come after the children went.
            if (holder == null || glow == null)
            {
                return;
            }

            holder.gameObject.SetActive(false);
            glow.enabled = false;
            ShowDots(false);
            filter.sharedMesh = null;
        }

        public void ShowDots(bool on)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].enabled = on && i < dotCount;
            }
        }

        /// <summary>
        /// The glow's strength and size against its resting ones. White is
        /// mixed in above 1 strength, for the clamp's flash.
        /// </summary>
        public void Glow(float strength, float size, float white = 0f)
        {
            if (glow.sharedMaterial == null)
            {
                return;
            }

            glow.enabled = strength > 0.001f && size > 0.001f;
            if (!glow.enabled)
            {
                return;
            }

            glowHost.localScale = Vector3.one * (radius * 2.4f * size);
            glow.GetPropertyBlock(glowBlock);
            glowBlock.SetColor(GlowColorId, Color.Lerp(color, Color.white * 1.5f, Mathf.Clamp01(white)));
            glowBlock.SetFloat(GlowIntensityId, 0.7f * strength);
            glow.SetPropertyBlock(glowBlock);
        }

        /// <summary>
        /// Turns the glow and the dots to the camera and lays the dots out in
        /// a row under the part.
        /// </summary>
        public void Face(Camera camera, Vector3 middle)
        {
            if (camera == null)
            {
                return;
            }

            Quaternion facing = Quaternion.LookRotation(middle - camera.transform.position, camera.transform.up);
            glowHost.SetPositionAndRotation(middle + facing * Vector3.forward * radius, facing);

            float dot = Mathf.Max(0.05f, radius * 0.13f);
            dotHost.SetPositionAndRotation(middle - camera.transform.up * (radius + dot * 1.6f), facing);

            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].transform.localPosition = new Vector3((i - (dotCount - 1) * 0.5f) * dot * 2.4f, 0f, 0f);
                dots[i].transform.localScale = Vector3.one * dot;
                dots[i].transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }

        private static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
        }

        /// <summary>
        /// A flat disc whose UV.y runs from the middle (0.5) to the rim (0):
        /// the glow shader fades across UV.y, so on this it fades out from the
        /// middle evenly all round. Diameter 1, facing -Z.
        /// </summary>
        private static Mesh Disc()
        {
            if (disc != null)
            {
                return disc;
            }

            const int sides = 32;
            var vertices = new Vector3[sides + 1];
            var uv = new Vector2[sides + 1];
            var triangles = new int[sides * 3];

            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0f, 0.5f);

            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.5f;
                uv[i + 1] = new Vector2(0f, 0f);

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }

            disc = new Mesh { name = "Part Glow Disc", vertices = vertices, uv = uv, triangles = triangles };
            disc.RecalculateBounds();
            return disc;
        }
    }
}
