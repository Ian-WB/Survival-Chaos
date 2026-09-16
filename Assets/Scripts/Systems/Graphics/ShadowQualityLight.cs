using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// Marks a scene light whose shadows the Shadows setting decides.
    ///
    /// Opt-in rather than every shadowed light in the scene, because the one set
    /// of casters the row must not reach is the bullet lights. BulletLightPool
    /// clones its templates onto the rounds and budgets their shadows itself, one
    /// per volley, and QualitySettings switches that off on the bottom two tiers.
    /// A row raising every point light's resolution would spend six faces a light
    /// on bullets the pool had deliberately kept cheap.
    ///
    /// So this sits on the Directional Light and on Lava Light 2, the one lava
    /// light that casts. A light that starts casting later needs it added to
    /// follow the row.
    ///
    /// Each rung picks one of the tier's own shadow resolution levels - Low,
    /// Medium or High out of the pipeline asset's table - so the tiers still
    /// differ underneath the row, the same way the fog row picks a budget. Ultra
    /// is never used: a point light draws six faces, and six at the top level
    /// outgrow the punctual atlas on every tier. Off stops the light casting at
    /// all. Whether it cast soft or hard is captured once, so leaving Off restores
    /// what the scene authored rather than a guess.
    ///
    /// **Off moves a line in the scene file, and that line is not a setting.**
    /// Lava Light 2 caches its shadows - shadowUpdateMode OnEnable - and HDRP
    /// only keeps a light in the cached atlas while it still casts:
    ///
    ///     wantsShadowCache = wantsShadowCache &amp;&amp; (legacyLight.shadows != LightShadows.None);
    ///
    /// so Off evicts it, and eviction writes m_UseViewFrustumForShadowCasterCull
    /// back to true on the legacy Light. Exercising this row in the Editor will
    /// keep showing that line as a scene diff on the lava light; it is HDRP's
    /// bookkeeping and can be discarded. The same line on the Directional Light
    /// is a real authored value, because an EveryFrame light never enters the
    /// cached atlas and nothing overwrites it there.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Shadow Quality Light")]
    [RequireComponent(typeof(Light))]
    public sealed class ShadowQualityLight : MonoBehaviour
    {
        private static readonly List<ShadowQualityLight> Active = new List<ShadowQualityLight>();

        private Light source;
        private HDAdditionalLightData hdData;
        private LightShadows authoredShadows;
        private bool captured;

        private void Awake()
        {
            Capture();
        }

        private void OnEnable()
        {
            Capture();
            Active.Add(this);

            // The director is created before the first scene loads, so it is
            // there for the lights in it. Asked here as well as told later,
            // because a light enabled after the last settings change would
            // otherwise keep its authored level until the next one.
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director != null)
            {
                Apply(director.Shadows);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Called by the director whenever the settings are applied.</summary>
        public static void ApplyAll(EffectQuality quality)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                Active[i].Apply(quality);
            }
        }

        private void Capture()
        {
            if (captured)
            {
                return;
            }

            source = GetComponent<Light>();
            hdData = GetComponent<HDAdditionalLightData>();
            authoredShadows = source != null ? source.shadows : LightShadows.None;
            captured = true;
        }

        private void Apply(EffectQuality quality)
        {
            if (source == null)
            {
                return;
            }

            if (!QualityLadder.IsOn(quality))
            {
                source.shadows = LightShadows.None;
                return;
            }

            source.shadows = authoredShadows;

            if (hdData != null)
            {
                // Off the override so the level is what counts. The setters only
                // refresh HDRP's cached shadow when the value actually moves.
                hdData.SetShadowResolutionOverride(false);
                hdData.SetShadowResolutionLevel(ShadowLadder.LightLevel(quality));
            }
        }

        /// <summary>Static state outlives play mode when domain reload is off.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Active.Clear();
        }
    }
}
