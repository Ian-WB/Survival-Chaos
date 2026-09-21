using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// A torpedo's motor, drawn: a plume out of its tail that burns as hard as
    /// <see cref="TorpedoSteer.Thrust"/> says. Low out of the muzzle, full once it
    /// is up to speed, and out the moment its fuel is - so a torpedo that has
    /// stopped hunting looks it, and the player can tell a dead one from a live
    /// one without waiting to see whether it turns.
    ///
    /// The ship's own flares, <see cref="ShipThrusters"/>, done the same way and
    /// for the same reasons: the same plume mesh, the same shader, a rigid mesh
    /// that writes its own motion vectors rather than particles FSR would
    /// smear, and the same fast light and slow die-back that reads as
    /// combustion rather than a lamp on a dimmer.
    ///
    /// Added to a round the first time it is fired as a torpedo, and it stays on
    /// the pooled object from then on. The crown shares its rounds with the ram,
    /// so a round fired as a plain bullet switches this off on its first frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TorpedoExhaust : MonoBehaviour
    {
        /// <summary>World length of the plume at full burn. Half the torpedo again.</summary>
        private const float Length = 0.28f;

        /// <summary>World width at the nozzle: about the tail's own girth on a half-size torpedo.</summary>
        private const float Width = 0.07f;

        /// <summary>
        /// Emission at full burn. The ship's main engine runs at 6; this is a
        /// little under, so the hostile orange stays orange rather than blowing
        /// out to white, which is what the boss's pods taught.
        /// </summary>
        private const float Brightness = 5f;

        /// <summary>The ship's own response rates: lights fast, dies back slowly.</summary>
        private const float RiseResponse = 26f;
        private const float FallResponse = 9f;

        private static readonly int FlameIntensityId = Shader.PropertyToID("_FlameIntensity");

        /// <summary>Shared: a block is written and applied before the next one is touched.</summary>
        private static MaterialPropertyBlock block;

        private ShootScript shot;
        private Renderer flare;
        private float level;

        /// <summary>
        /// Lights the motor for this flight, building the plume the first time.
        /// </summary>
        public void Ignite(Mesh plume, Material material)
        {
            if (plume == null || material == null)
            {
                return;
            }

            if (shot == null)
            {
                shot = GetComponent<ShootScript>();
            }

            if (flare == null)
            {
                flare = Build(plume, material);
            }

            level = 0f;
            enabled = true;
            Apply();
        }

        private void Update()
        {
            if (shot == null || flare == null || !shot.IsTorpedo)
            {
                // A plain round from the same pool: nothing to burn, and no need
                // to be asked again until it is fired as a torpedo.
                if (flare != null)
                {
                    flare.enabled = false;
                }

                enabled = false;
                return;
            }

            float target = shot.Thrust;
            level = ShipMotion.Approach(level, target, target > level ? RiseResponse : FallResponse, Time.deltaTime);
            Apply();
        }

        private void OnDisable()
        {
            level = 0f;

            if (flare != null)
            {
                flare.enabled = false;
            }
        }

        /// <summary>
        /// Sizes the plume and sets its brightness, in world units whatever the
        /// round is scaled to. Switched off rather than drawn at nothing, so a
        /// coasting torpedo costs no draw.
        /// </summary>
        private void Apply()
        {
            bool lit = level > 0.002f;
            flare.enabled = lit;

            if (!lit)
            {
                return;
            }

            float scale = Mathf.Max(1e-4f, transform.lossyScale.x);
            flare.transform.localScale = new Vector3(Length * level, Width, Width) / scale;

            block ??= new MaterialPropertyBlock();
            flare.GetPropertyBlock(block);
            block.SetFloat(FlameIntensityId, Brightness * level);
            flare.SetPropertyBlock(block);
        }

        /// <summary>
        /// The plume, at the tail and pointing back along the torpedo's path.
        ///
        /// The tail is found from the visible art rather than written down,
        /// because the crown's two rounds are modelled facing opposite ways -
        /// one with its nose on -X and its flat end at +X, the other the reverse
        /// - and <see cref="ShootScript.NoseOnNegativeX"/> says which this is.
        /// </summary>
        private Renderer Build(Mesh plume, Material material)
        {
            bool noseOnNegativeX = shot == null || shot.NoseOnNegativeX;
            Vector3 tail = TailPoint(noseOnNegativeX);

            var go = new GameObject("Torpedo Exhaust");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = tail;

            // The plume mesh runs along +X from its nozzle.
            go.transform.localRotation = noseOnNegativeX ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

            go.AddComponent<MeshFilter>().sharedMesh = plume;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Rigid with the torpedo, so its own matrix gives FSR the true motion.
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            renderer.enabled = false;

            return renderer;
        }

        /// <summary>
        /// The middle of the art's tail end, in this round's own space: the end
        /// of the drawn meshes away from the nose, at their centre height and
        /// depth. Only renderers that draw, because the round also carries a
        /// switched-off capsule the size of its hit box.
        /// </summary>
        private Vector3 TailPoint(bool noseOnNegativeX)
        {
            bool any = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            foreach (Renderer part in GetComponentsInChildren<Renderer>(includeInactive: false))
            {
                if (!part.enabled || part == flare || !(part is MeshRenderer) || !part.TryGetComponent(out MeshFilter filter)
                    || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toRound = transform.worldToLocalMatrix * part.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toRound.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));

                    min = any ? Vector3.Min(min, point) : point;
                    max = any ? Vector3.Max(max, point) : point;
                    any = true;
                }
            }

            if (!any)
            {
                return Vector3.zero;
            }

            Vector3 middle = (min + max) * 0.5f;
            return new Vector3(noseOnNegativeX ? max.x : min.x, middle.y, middle.z);
        }
    }
}
