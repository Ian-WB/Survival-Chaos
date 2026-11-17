using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A part that has just clamped onto the hull switches its lights on: dark
    /// for a beat, two flickers, a flare well past its resting glow, then it
    /// settles (part pickups, 17 November 2026). Only the part's glowing
    /// materials change; its plain plates are left alone. Looks only.
    /// </summary>
    public sealed class PartPowerOn : MonoBehaviour
    {
        private static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");

        private const float Dark = 0.06f;
        private const float Flicker = 0.1f;
        private const float Flare = 0.45f;
        private const float FlareStrength = 5f;

        private Renderer target;
        private Color[] resting;
        private MaterialPropertyBlock block;
        private float age;

        /// <summary>Starts (or restarts) the power-on on every glowing renderer under <paramref name="mount"/>.</summary>
        public static void Play(Transform mount)
        {
            if (mount == null)
            {
                return;
            }

            foreach (Renderer renderer in mount.GetComponentsInChildren<Renderer>())
            {
                PartPowerOn on = renderer.GetComponent<PartPowerOn>();
                if (on == null)
                {
                    on = renderer.gameObject.AddComponent<PartPowerOn>();
                }

                on.Begin(renderer);
            }
        }

        /// <summary>The brightness against the resting glow, <paramref name="age"/> seconds after the lock.</summary>
        public static float Level(float age)
        {
            if (age < Dark)
            {
                return 0f;
            }

            if (age < Dark + Flicker)
            {
                float k = (age - Dark) / Flicker;
                return k < 0.3f || k > 0.55f && k < 0.75f ? 1.5f : 0.1f;
            }

            float settle = (age - Dark - Flicker) / Flare;
            if (settle >= 1f)
            {
                return 1f;
            }

            return Mathf.Lerp(FlareStrength, 1f, 1f - (1f - settle) * (1f - settle));
        }

        private void Begin(Renderer renderer)
        {
            target = renderer;
            block ??= new MaterialPropertyBlock();
            Material[] materials = renderer.sharedMaterials;
            resting = new Color[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                resting[i] = material != null && material.HasProperty(EmissiveId)
                    ? material.GetColor(EmissiveId)
                    : Color.black;
            }

            age = 0f;
            enabled = true;
            Step();
        }

        private void Update()
        {
            age += Time.deltaTime;
            Step();
        }

        private void Step()
        {
            if (target == null)
            {
                enabled = false;
                return;
            }

            bool done = age >= Dark + Flicker + Flare;
            float level = Level(age);

            for (int i = 0; i < resting.Length; i++)
            {
                if (resting[i].maxColorComponent <= 0.001f)
                {
                    continue;
                }

                if (done)
                {
                    target.SetPropertyBlock(null, i);
                    continue;
                }

                target.GetPropertyBlock(block, i);
                block.SetColor(EmissiveId, resting[i] * level);
                target.SetPropertyBlock(block, i);
            }

            if (done)
            {
                enabled = false;
            }
        }
    }
}
