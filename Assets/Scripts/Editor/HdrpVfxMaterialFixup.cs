using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Moves the two VFX materials the Built-in to HDRP converter skipped onto
    /// HDRP/Unlit. The converter only maps opaque surface shaders, so particle
    /// and skybox shaders come out the other side still pointing at built-in
    /// shaders, which HDRP renders as magenta.
    ///
    /// This is done in code rather than by editing the .mat files because an
    /// HDRP material's shader keywords and render queue have to agree with its
    /// properties. HDMaterial.SetSurfaceType recomputes both; hand-written YAML
    /// would have to get them right by luck.
    ///
    /// Re-running is safe.
    /// </summary>
    public static class HdrpVfxMaterialFixup
    {
        private const string ExplosionPath = "Assets/Art/Materials/VFX/Explosion.mat";
        private const string SmokePath = "Assets/Art/Materials/VFX/Smoke_Mat.mat";

        // HDRP's BlendMode enum: Alpha = 0, Additive = 1, Premultiply = 4.
        private const float BlendAlpha = 0f;
        private const float BlendAdditive = 1f;

        [MenuItem("Survival Chaos/Fix HDRP VFX Materials")]
        public static void Fix()
        {
            Shader unlit = Shader.Find("HDRP/Unlit");
            if (unlit == null)
            {
                Debug.LogError("HDRP/Unlit not found. Is the HDRP package installed?");
                return;
            }

            int fixedCount = 0;
            fixedCount += FixExplosion(unlit) ? 1 : 0;
            fixedCount += FixSmoke(unlit) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"HDRP VFX fixup: {fixedCount} material(s) moved to HDRP/Unlit.");
        }

        /// <summary>
        /// The explosion had no texture - all of its look came from an HDR
        /// emissive orange, so that is what carries over. Additive blending
        /// keeps it reading as a flash rather than a solid object.
        /// </summary>
        private static bool FixExplosion(Shader unlit)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ExplosionPath);
            if (material == null)
            {
                Debug.LogWarning($"Not found: {ExplosionPath}");
                return false;
            }

            Color emissive = material.HasProperty("_EmissionColor")
                ? material.GetColor("_EmissionColor")
                : new Color(16f, 3.89f, 0f);

            material.shader = unlit;
            material.SetColor("_UnlitColor", Color.white);
            material.SetFloat("_BlendMode", BlendAdditive);

            HDMaterial.SetUseEmissiveIntensity(material, false);
            HDMaterial.SetEmissiveColor(material, emissive);

            // Recomputes keywords and render queue for the properties above.
            HDMaterial.SetSurfaceType(material, transparent: true);

            EditorUtility.SetDirty(material);
            return true;
        }

        /// <summary>
        /// The smoke is a textured particle, so the texture carries over and it
        /// blends normally.
        /// </summary>
        private static bool FixSmoke(Shader unlit)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SmokePath);
            if (material == null)
            {
                Debug.LogWarning($"Not found: {SmokePath}");
                return false;
            }

            Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Color tint = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

            material.shader = unlit;
            material.SetColor("_UnlitColor", tint);
            material.SetFloat("_BlendMode", BlendAlpha);

            if (texture != null)
            {
                material.SetTexture("_UnlitColorMap", texture);
            }
            else
            {
                Debug.LogWarning($"{SmokePath} had no _MainTex to carry over.");
            }

            HDMaterial.SetSurfaceType(material, transparent: true);

            EditorUtility.SetDirty(material);
            return true;
        }
    }
}
