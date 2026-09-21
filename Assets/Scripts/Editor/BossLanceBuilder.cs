using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The prow lance's two materials, and the round prefabs that carry them.
    ///
    /// Until 21 September 2026 the lance wore BossLance.mat on HDRP Lit, the
    /// flat emission a single round had, over a square tube that was thrown
    /// along the ring. In play it read as a bent log. <see cref="BossLanceBeam"/>
    /// now fires it as a laser from the prow and draws it as two ribbons turned
    /// to face the camera, and this builds what they are drawn with:
    ///
    /// The core, BossLance.mat on BossLance.shadergraph - the player's needle
    /// graph with its function rewritten. Opaque HDRP Unlit, white-hot on its
    /// centre line, with energy pulsing out from the prow along it. Opaque so the
    /// line always reads as solid, whatever it is drawn over, and writes depth
    /// and motion vectors like any opaque surface.
    ///
    /// The glow, BossLanceGlow.mat on BossLanceGlow.shadergraph - the round
    /// streak's graph, renamed. Transparent and additive, fading to nothing at
    /// its edges, so the beam has no outline anywhere. It writes its own motion
    /// vectors for the reason given in <see cref="PlayerRoundBuilder"/>: the
    /// settings run FSR, and a transparent surface that does not gets dragged
    /// across the screen by whatever is behind it.
    ///
    /// Both go on the lance's round prefabs, core first, because the beam takes
    /// its materials from the round in submesh order. Re-running updates all of
    /// it in place, so every GUID survives. None of this touches where the lance
    /// hurts: the hit test lives in BossLanceBeam and reads none of it.
    ///
    /// Like PlayerRoundBuilder, the prefabs are edited through SerializedObject
    /// on the asset rather than through PrefabUtility.LoadPrefabContents - opening
    /// a prefab stage from a script once stalled the editor long enough for the
    /// GPU driver to reset it.
    /// </summary>
    public static class BossLanceBuilder
    {
        private const string CoreShaderPath = "Assets/Art/Shaders/BossLance.shadergraph";
        private const string GlowShaderPath = "Assets/Art/Shaders/BossLanceGlow.shadergraph";
        private const string CoreMaterialPath = "Assets/Art/Materials/VFX/BossLance.mat";
        private const string GlowMaterialPath = "Assets/Art/Materials/VFX/BossLanceGlow.mat";

        /// <summary>The lance's rounds, one for each direction it fires.</summary>
        private static readonly string[] RoundPaths =
        {
            "Assets/Prefabs/Boss/boss_shoot 5.prefab",
            "Assets/Prefabs/Boss/boss_shoot 6.prefab",
        };

        /// <summary>
        /// The core's centre, pale and hot. A blob this bright reads as a star -
        /// the boss pods' problem - but a line this thin reads as a laser, and the
        /// glow around it carries the colour.
        /// </summary>
        private static readonly Vector3 CoreColour = new Vector3(1.00f, 0.85f, 0.60f);
        private const float CoreIntensity = 4f;

        /// <summary>The core's edges, where it hands over to the glow.</summary>
        private static readonly Vector3 BodyColour = new Vector3(1.00f, 0.45f, 0.10f);
        private const float BodyIntensity = 3f;

        /// <summary>
        /// Cycles a second. The shader puts a crest every 3 units, so the energy
        /// runs out from the prow at 12 units a second: fast enough to read as
        /// flowing in the half second the beam is on.
        /// </summary>
        private const float PulseSpeed = 4f;

        /// <summary>How far a trough dims below a crest. The pulse only ever dims.</summary>
        private const float PulseDepth = 0.25f;

        /// <summary>
        /// The halo's colour: the lance's old red-orange, the one it had in the
        /// boss's warm family, so the weapon is still the same colour from a
        /// distance.
        /// </summary>
        private static readonly Vector3 GlowColour = new Vector3(1.00f, 0.30f, 0.05f);
        private const float GlowIntensity = 2f;

        [MenuItem("Survival Chaos/Build Boss Lance", priority = 54)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var log = new StringBuilder();

            Shader coreShader = LoadShader(CoreShaderPath, log);
            Shader glowShader = LoadShader(GlowShaderPath, log);

            if (coreShader == null || glowShader == null)
            {
                return log.Append("Nothing was changed.").ToString();
            }

            Material core = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
            if (core == null)
            {
                return "No material at " + CoreMaterialPath + ". The lance's round prefabs point at that asset, " +
                       "so it is updated rather than created. Nothing was changed.";
            }

            Reset(core, coreShader);
            core.SetVector("_BodyColor", BodyColour);
            core.SetFloat("_BodyIntensity", BodyIntensity);
            core.SetVector("_CoreColor", CoreColour);
            core.SetFloat("_CoreIntensity", CoreIntensity);
            core.SetFloat("_PulseSpeed", PulseSpeed);
            core.SetFloat("_PulseDepth", PulseDepth);
            HDMaterial.ValidateMaterial(core);
            EditorUtility.SetDirty(core);

            Material glow = BuildGlow(glowShader);

            foreach (string path in RoundPaths)
            {
                AssignToRound(path, core, glow, log);
            }

            AssetDatabase.SaveAssets();
            return log.Append(CoreMaterialPath + " and " + GlowMaterialPath + " built.").ToString();
        }

        private static Shader LoadShader(string path, StringBuilder log)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

            if (shader == null)
            {
                log.Append("No shader at " + path + ".\n");
                return null;
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                log.Append(path + " has compile errors.\n");
                return null;
            }

            return shader;
        }

        /// <summary>
        /// Puts a material on a shader wholesale rather than by switching it. A
        /// switch keeps every property the old shader saved - a hundred and more
        /// from Lit, on the core - and they sit in the file doing nothing.
        /// </summary>
        private static void Reset(Material material, Shader shader)
        {
            string name = material.name;
            var fresh = new Material(shader);
            EditorUtility.CopySerialized(fresh, material);
            Object.DestroyImmediate(fresh);
            material.name = name;
        }

        private static Material BuildGlow(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);

            if (material == null)
            {
                material = new Material(shader) { name = "BossLanceGlow" };
                AssetDatabase.CreateAsset(material, GlowMaterialPath);
            }
            else
            {
                Reset(material, shader);
            }

            material.SetVector("_GlowColor", GlowColour);
            material.SetFloat("_GlowIntensity", GlowIntensity);

            // Stated here rather than left to the graph's defaults, because a
            // material that already exists keeps its own saved values over the
            // graph's. The ribbon faces the camera, so either side may be the
            // one showing, and it writes motion vectors and takes part in TAA and
            // FSR like the rounds' streaks do.
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_TransparentWritingMotionVec", 1f);
            material.SetFloat("_ExcludeFromTUAndAA", 0f);

            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignToRound(string path, Material core, Material glow, StringBuilder log)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Renderer art = root != null ? root.GetComponentInChildren<Renderer>(includeInactive: true) : null;

            if (art == null)
            {
                log.Append(path + ": no renderer, skipped.\n");
                return;
            }

            var renderer = new SerializedObject(art);
            SerializedProperty materials = renderer.FindProperty("m_Materials");
            materials.arraySize = 2;
            materials.GetArrayElementAtIndex(0).objectReferenceValue = core;
            materials.GetArrayElementAtIndex(1).objectReferenceValue = glow;
            renderer.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssetIfDirty(root);
            log.Append(path + ": core and glow assigned.\n");
        }
    }
}
