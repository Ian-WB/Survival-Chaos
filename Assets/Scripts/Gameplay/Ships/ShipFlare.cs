using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// One glowing mesh bolted to a ship: an engine plume, a gun's glow.
    ///
    /// The player's flares (<see cref="ShipThrusters"/>) and the Leviathan's
    /// muzzle glows are built this way already, each in its own script. This
    /// is the same recipe for the ships that came to it later, kept in one
    /// place: rigid geometry in Object motion vector mode, because anything
    /// bright that does not write its own motion gets dragged across the
    /// screen under FSR, and brightness through a property block so one
    /// material serves every flare.
    /// </summary>
    internal static class ShipFlare
    {
        private static readonly int FlameIntensityId = Shader.PropertyToID("_FlameIntensity");

        /// <summary>Builds a flare under <paramref name="parent"/>, dark. Null without a mesh or a material.</summary>
        public static Renderer Build(Transform parent, string name, Mesh mesh, Material material,
            Vector3 localPosition, Quaternion localRotation)
        {
            if (parent == null || mesh == null || material == null)
            {
                return null;
            }

            var host = new GameObject(name);
            host.layer = parent.gameObject.layer;
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localPosition;
            host.transform.localRotation = localRotation;

            host.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer flare = host.AddComponent<MeshRenderer>();
            flare.sharedMaterial = material;
            flare.shadowCastingMode = ShadowCastingMode.Off;
            flare.receiveShadows = false;
            flare.lightProbeUsage = LightProbeUsage.Off;
            flare.reflectionProbeUsage = ReflectionProbeUsage.Off;
            flare.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            flare.enabled = false;

            return flare;
        }

        /// <summary>
        /// Sizes a flare and sets how hot it burns. One at nothing is switched
        /// off, not drawn at no size, so a dark flare costs no draw.
        /// </summary>
        public static void Set(Renderer flare, MaterialPropertyBlock block, Vector3 localScale, float brightness)
        {
            if (flare == null)
            {
                return;
            }

            bool lit = brightness > 0.002f && localScale.x > 0.0001f;
            flare.enabled = lit;

            if (!lit)
            {
                return;
            }

            flare.transform.localScale = localScale;

            flare.GetPropertyBlock(block);
            block.SetFloat(FlameIntensityId, brightness);
            flare.SetPropertyBlock(block);
        }
    }
}
