using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos.EditorTools
{
    /// <summary>Authors the combat palette and debris edges without touching attack balance.</summary>
    public static class CombatReadabilityBuilder
    {
        private const string Vfx = "Assets/Art/Materials/VFX/";
        private const string BossPath = "Assets/Prefabs/Boss/Boss.prefab";
        private const string WreckagePath = "Assets/Prefabs/Boss/BossWreckage.prefab";
        private const string PodPath = "Assets/Prefabs/Boss/BossEmplacement.mat";
        private const string EdgePath = Vfx + "WreckageEdge.mat";

        [MenuItem("Survival Chaos/Apply Combat Readability", priority = 56)]
        public static void Apply()
        {
            // Pink-red remains hostile, but carries blue that the orange lava
            // does not. Pale cores and darker shells provide a second cue.
            Tint(Vfx + "EnemyShot.mat", new Color(1.15f, 0.035f, 0.30f));
            Tint(Vfx + "EnemyShotTip.mat", new Color(3f, 1.35f, 1.8f));
            Tint(Vfx + "BossRound.mat", new Color(3f, 1.05f, 1.65f));
            Tint(Vfx + "BossRoundShell.mat", new Color(1.3f, 0.035f, 0.32f));
            Tint(Vfx + "BossLance.mat", new Color(3.3f, 0.30f, 0.95f));
            Tint(Vfx + "BossDisc.mat", new Color(2.4f, 0.12f, 0.70f));
            Tint(PodPath, new Color(0.30f, 2.2f, 0.10f));

            GameObject boss = PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                foreach (BossWeakPoint pod in boss.GetComponentsInChildren<BossWeakPoint>(true))
                {
                    foreach (Light lamp in pod.GetComponentsInChildren<Light>(true))
                    {
                        lamp.color = new Color(0.25f, 1f, 0.08f);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(boss, BossPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(boss); }

            GameObject wreckage = PrefabUtility.LoadPrefabContents(WreckagePath);
            try
            {
                EnsureWreckageEdges(wreckage);
                PrefabUtility.SaveAsPrefabAsset(wreckage, WreckagePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(wreckage); }

            AssetDatabase.SaveAssets();
            Debug.Log("Combat readability applied: hostile cores, green weak points and wreckage edges.");
        }

        private static void Tint(string path, Color emission)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogWarning("Missing combat material: " + path);
                return;
            }
            material.SetFloat("_UseEmissiveIntensity", 0f);
            material.SetColor("_EmissiveColor", emission);
            Color surface = emission / Mathf.Max(1f, emission.maxColorComponent);
            surface.a = 1f;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", surface);
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", emission);
            if (material.HasProperty("_Color")) material.SetColor("_Color", surface);
            EditorUtility.SetDirty(material);
        }

        public static void EnsureWreckageEdges(GameObject root)
        {
            Material skin = AssetDatabase.LoadAssetAtPath<Material>(EdgePath);
            if (skin == null)
            {
                Material source = AssetDatabase.LoadAssetAtPath<Material>(PodPath);
                if (source == null) return;
                skin = new Material(source) { name = "WreckageEdge" };
                AssetDatabase.CreateAsset(skin, EdgePath);
            }
            Tint(EdgePath, new Color(0.75f, 0.20f, 0.35f));

            Transform host = root.transform.Find("Hazard Edges");
            if (host == null)
            {
                host = new GameObject("Hazard Edges").transform;
                host.SetParent(root.transform, false);
            }
            LineRenderer line = host.GetComponent<LineRenderer>();
            if (line == null) line = host.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = skin;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.28f;
            line.startColor = line.endColor = Color.white;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // One renderer outlines the twelve edges of the authored 20x12x9
            // plate. It inherits tumble and lifetime shrink, and has no collider.
            Vector3 a = new Vector3(-10f, -6f, -4.5f);
            Vector3 b = new Vector3(10f, -6f, -4.5f);
            Vector3 c = new Vector3(10f, 6f, -4.5f);
            Vector3 d = new Vector3(-10f, 6f, -4.5f);
            Vector3 e = new Vector3(-10f, -6f, 4.5f);
            Vector3 f = new Vector3(10f, -6f, 4.5f);
            Vector3 g = new Vector3(10f, 6f, 4.5f);
            Vector3 h = new Vector3(-10f, 6f, 4.5f);
            Vector3[] corners = { a, b, c, d, a, e, f, b, f, g, c, g, h, d, h, e };
            line.positionCount = corners.Length;
            line.SetPositions(corners);
        }
    }
}
