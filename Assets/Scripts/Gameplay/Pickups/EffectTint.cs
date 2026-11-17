using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Recolours and shrinks a pooled one-shot particle effect for one use, and
    /// puts it back the next time it is spawned plain (part pickups, 17 November
    /// 2026: the clamp burst takes the upgrade's colour and is drawn smaller).
    /// Effects come from <see cref="ObjectPool"/>, so whatever one use changes
    /// would otherwise stay on the instance for the next.
    /// </summary>
    public sealed class EffectTint : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private ParticleSystem.MinMaxGradient[] startColors;
        private ParticleSystem.MinMaxGradient[] overLife;
        private bool[] wasOn;
        private Vector3 scale;
        private bool changed;

        /// <summary>
        /// Every particle in <paramref name="effect"/> in <paramref name="color"/>
        /// (alpha curves kept), the whole effect at <paramref name="size"/> of its
        /// own, and the systems named in <paramref name="drop"/> left out.
        /// </summary>
        public static void Apply(GameObject effect, Color color, float size, params string[] drop)
        {
            if (effect == null)
            {
                return;
            }

            EffectTint tint = effect.GetComponent<EffectTint>();
            if (tint == null)
            {
                tint = effect.AddComponent<EffectTint>();
            }

            tint.Recolour(color, size, drop);
        }

        /// <summary>Puts a pooled effect back the way its prefab made it, if a use changed it.</summary>
        public static void Restore(GameObject effect)
        {
            if (effect != null && effect.TryGetComponent(out EffectTint tint))
            {
                tint.Put();
            }
        }

        /// <summary>A colour gradient in one colour, keeping the alpha keys of the one it replaces.</summary>
        public static Gradient OneColour(Gradient alphaFrom, Color color)
        {
            var gradient = new Gradient();
            GradientAlphaKey[] alpha = alphaFrom != null
                ? alphaFrom.alphaKeys
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };

            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                alpha);
            return gradient;
        }

        /// <summary>
        /// The pool switches an effect off when it goes back, and the same
        /// effect serves enemies and the boss: it goes back plain.
        /// </summary>
        private void OnDisable()
        {
            Put();
        }

        private void Remember()
        {
            if (systems != null)
            {
                return;
            }

            systems = GetComponentsInChildren<ParticleSystem>(true);
            startColors = new ParticleSystem.MinMaxGradient[systems.Length];
            overLife = new ParticleSystem.MinMaxGradient[systems.Length];
            wasOn = new bool[systems.Length];
            scale = transform.localScale;

            for (int i = 0; i < systems.Length; i++)
            {
                startColors[i] = systems[i].main.startColor;
                overLife[i] = systems[i].colorOverLifetime.color;
                wasOn[i] = systems[i].gameObject.activeSelf;
            }
        }

        private void Recolour(Color color, float size, string[] drop)
        {
            Remember();
            changed = true;
            transform.localScale = scale * size;
            Color opaque = new Color(color.r, color.g, color.b, 1f);

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                bool dropped = drop != null && System.Array.IndexOf(drop, system.name) >= 0;

                if (system.gameObject != gameObject)
                {
                    system.gameObject.SetActive(wasOn[i] && !dropped);
                }

                ParticleSystem.MainModule main = system.main;
                main.startColor = opaque;

                ParticleSystem.ColorOverLifetimeModule life = system.colorOverLifetime;
                if (life.enabled)
                {
                    life.color = OneColour(overLife[i].gradient, Color.white);
                }
            }
        }

        private void Put()
        {
            if (!changed || systems == null)
            {
                return;
            }

            changed = false;
            transform.localScale = scale;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];

                if (system.gameObject != gameObject)
                {
                    system.gameObject.SetActive(wasOn[i]);
                }

                ParticleSystem.MainModule main = system.main;
                main.startColor = startColors[i];

                ParticleSystem.ColorOverLifetimeModule life = system.colorOverLifetime;
                life.color = overLife[i];
            }
        }
    }
}
