using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The smoke rising out of the volcano: its material, and the fog volume
    /// in the Game scene that wears it.
    ///
    /// Asked for on 2 October 2026 as volumetric smoke, so it is HDRP's own
    /// Local Volumetric Fog rather than particles. It is part of the same fog
    /// the arena already has, which means the lava lights and the sun light it,
    /// it sits at its real depth behind the ships, and there is no overdraw,
    /// no sorting and nothing for FSR to drag. The price is that it is only as
    /// sharp as the fog's own grid, and that it is not there on Low, where the
    /// fog is off. It is not baked: a lighting bake neither needs it nor
    /// changes it.
    ///
    /// One box over the crater, and the plume is drawn inside it by
    /// VolcanoSmoke.shadergraph, whose one node is the function in
    /// VolcanoSmoke.hlsl - the shape, the lean and the billows are all there.
    /// The first version, the same day, was three boxes stacked and scrolling
    /// a noise texture. It was rejected in play: the three boxes read as a
    /// pipe with steps in it, and the texture's fine noise, finer than the
    /// fog's grid, flickered as it moved.
    ///
    /// Re-running puts the material and the volume back to what is written
    /// here, keeping the object, so anything else hung on it survives.
    /// </summary>
    public static class VolcanoSmokeBuilder
    {
        public const string ShaderPath = "Assets/Art/Shaders/VolcanoSmoke.shadergraph";
        public const string MaterialPath = "Assets/Art/Materials/VFX/VolcanoSmoke.mat";
        public const string RootName = "Volcano Smoke";

        /// <summary>The mouth of the crater, in the Game scene.</summary>
        public static readonly Vector3 Crater = new Vector3(0.2f, 12.6f, 0.8f);

        /// <summary>
        /// The box the plume is drawn in. The plume takes about four tenths of
        /// its width at the top and bends a fifth of it downwind, along the box's X, so
        /// the box is wider than the smoke; the top quarter is the fade out.
        /// </summary>
        private static readonly Vector3 Box = new Vector3(26f, 28f, 26f);

        /// <summary>How far into the crater the box starts, so the smoke comes from inside it.</summary>
        private const float Sunk = 0.8f;

        /// <summary>
        /// Pale warm ash. Dark smoke, 0.4, could not be seen: fog shows by the
        /// light it scatters, the sky here is night, and dark smoke scatters
        /// none. Pale, the crater's light climbs the column and the rest
        /// stands grey against the clouds.
        /// </summary>
        private static readonly Color Ash = new Color(0.80f, 0.76f, 0.72f);

        /// <summary>How far light gets through the thickest of it, at the mouth.</summary>
        private const float FogDistance = 0.3f;

        [MenuItem("Survival Chaos/Environment/Build Volcano Smoke", priority = 110)]
        public static void Build()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("Volcano smoke: " + ShaderPath + " is missing or did not compile.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Build Volcano Smoke");
            }

            root.transform.SetPositionAndRotation(
                Crater + Vector3.up * (Box.y * 0.5f - Sunk), Quaternion.identity);
            root.transform.localScale = Vector3.one;
            root.isStatic = false;

            // The first version's three layers.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name.StartsWith("Smoke, "))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            if (!root.TryGetComponent(out LocalVolumetricFog fog))
            {
                fog = root.AddComponent<LocalVolumetricFog>();
            }

            LocalVolumetricFogArtistParameters p = fog.parameters;
            p.albedo = Ash;
            p.meanFreePath = FogDistance;
            p.blendingMode = LocalVolumetricFogBlendingMode.Additive;
            p.size = Box;
            p.maskMode = LocalVolumetricFogMaskMode.Material;
            p.materialMask = material;
            p.volumeMask = null;
            // The shader empties the sides and fades both ends itself.
            p.positiveFade = Vector3.zero;
            p.negativeFade = Vector3.zero;
            p.invertFade = false;
            p.distanceFadeStart = 10000f;
            p.distanceFadeEnd = 10000f;
            fog.parameters = p;
            EditorUtility.SetDirty(fog);

            AssetDatabase.SaveAssetIfDirty(material);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Volcano smoke built: one fog volume on " + MaterialPath + ".", root);
        }
    }
}
