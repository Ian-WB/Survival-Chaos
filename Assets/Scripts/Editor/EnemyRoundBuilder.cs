using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Makes the Fighter's and the Heavy's rounds something you can see and
    /// have to respect.
    ///
    /// Until 10 November 2026 an enemy round was 5 cm thick and 10 to 21 cm
    /// long, carried no streak, lived 0.4 seconds and cost 1 hit point. Ian
    /// could not always see one. This tool doubles each round, hit box and
    /// all, gives it the player's streak in the hostile violet, lets it fly
    /// for 0.7 seconds (56 degrees of the ring where it was 32) and makes a
    /// hit cost 2. Speed and fire rate are left alone. The Fighter's brighter
    /// glow is a line in HostileFireColour.
    ///
    /// The Fighter's dart is not modelled on its pivot: it sat 0.17 above it
    /// and a little to one side. Doubling the root doubled that offset, and the
    /// first build left the streak on the pivot, a third of a unit from the dart
    /// it belonged to. So the dart and its hit box are moved back to where they
    /// were in the world, and the streak is built on the dart.
    ///
    /// Every value is written as an absolute, so re-running changes nothing.
    /// The streak is rigid geometry on the prefab root for the reason given in
    /// PlayerRoundBuilder, whose mesh and shader it reuses; prefab edits go
    /// through the asset and never through a prefab stage.
    /// </summary>
    public static class EnemyRoundBuilder
    {
        private const string TrailShaderPath = "Assets/Art/Shaders/PlayerRoundTrail.shadergraph";
        public const string TrailMaterialPath = "Assets/Art/Materials/VFX/EnemyRoundTrail.mat";
        private const string MeshFolder = "Assets/Art/Models/Projectiles";

        /// <summary>Hit points a hit costs the player.</summary>
        public const int Damage = 2;

        /// <summary>Seconds a round flies before it retires.</summary>
        public const float Life = 0.7f;

        /// <summary>World length of the streak behind the round's centre.</summary>
        private const float StreakLength = 1.0f;

        /// <summary>World width at the head, the round's own thickness.</summary>
        private const float StreakHeadWidth = 0.10f;

        private const float StreakTailWidth = 0.015f;

        /// <summary>Under HostileFireColour.PeakCeiling, so the violet survives the tonemapper.</summary>
        private const float TrailIntensity = 1.6f;

        public readonly struct Round
        {
            public Round(string path, float rootScale, Vector3 centre)
            {
                Path = path;
                RootScale = rootScale;
                Centre = centre;
            }

            /// <summary>
            /// Where the middle of the round sits, in the root's own units: half
            /// the hit box centre it was authored with, since the root is twice
            /// what it was.
            /// </summary>
            public Vector3 Centre { get; }

            public string Path { get; }

            /// <summary>Twice what the prefab was authored at.</summary>
            public float RootScale { get; }
        }

        public static readonly Round[] Rounds =
        {
            new Round("Assets/Prefabs/Enemies/enemy_shoot 1.prefab", 1.0f, new Vector3(0.018f, 0.1675f, -0.016f)),
            new Round("Assets/Prefabs/Enemies/enemy_shoot 2.prefab", 1.0f, new Vector3(-0.0185f, 0.1675f, 0.077f)),
            new Round("Assets/Prefabs/Enemies/temp_shoot.prefab", 0.1f, Vector3.zero),
            new Round("Assets/Prefabs/Enemies/temp_shoot1.prefab", 0.1f, Vector3.zero),
        };

        [MenuItem("Survival Chaos/Gameplay/Build Enemy Rounds", priority = 123)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var log = new StringBuilder();

            Material trail = BuildTrailMaterial();
            if (trail == null)
            {
                return "No usable shader at " + TrailShaderPath + ". Nothing was changed.";
            }

            foreach (Round round in Rounds)
            {
                Apply(round, trail, log);
            }

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static Material BuildTrailMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(TrailShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, TrailMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetVector("_TrailColor", HostileFireColour.Bright);
            material.SetFloat("_TrailIntensity", TrailIntensity);

            // As PlayerRoundBuilder states them: the quads face either way, and the
            // streak writes motion vectors so FSR and TAA carry it with its round.
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_TransparentWritingMotionVec", 1f);
            material.SetFloat("_ExcludeFromTUAndAA", 0f);
            HDMaterial.ValidateMaterial(material);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Apply(Round round, Material trail, StringBuilder log)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(round.Path);
            if (root == null)
            {
                log.Append("Missing prefab " + round.Path + ", skipped.\n");
                return;
            }

            ShootScript script = root.GetComponent<ShootScript>();
            DestroyAfterTime life = root.GetComponent<DestroyAfterTime>();
            if (script == null || life == null)
            {
                log.Append(round.Path + ": no ShootScript or DestroyAfterTime, skipped.\n");
                return;
            }

            var transform = new SerializedObject(root.transform);
            transform.FindProperty("m_LocalScale").vector3Value = Vector3.one * round.RootScale;
            transform.ApplyModifiedPropertiesWithoutUndo();

            var shoot = new SerializedObject(script);
            shoot.FindProperty("damage").intValue = Damage;
            shoot.ApplyModifiedPropertiesWithoutUndo();
            float speed = shoot.FindProperty("speed").floatValue;

            var lifetime = new SerializedObject(life);
            lifetime.FindProperty("delay").floatValue = Life;
            lifetime.ApplyModifiedPropertiesWithoutUndo();

            // Only the Fighter's rounds, whose model hangs off the pivot. The dart
            // and its box move together by whatever is left to move, which is
            // nothing on a second run.
            Vector3 origin = Vector3.zero;
            BoxCollider box = root.GetComponent<BoxCollider>();

            if (round.Centre != Vector3.zero && box != null && root.transform.childCount > 0)
            {
                Vector3 shift = round.Centre - box.center;

                var model = new SerializedObject(root.transform.GetChild(0));
                SerializedProperty at = model.FindProperty("m_LocalPosition");
                at.vector3Value += shift;
                model.ApplyModifiedPropertiesWithoutUndo();

                var hit = new SerializedObject(box);
                hit.FindProperty("m_Center").vector3Value = round.Centre;
                hit.ApplyModifiedPropertiesWithoutUndo();

                origin = round.Centre;
            }

            float behind = -PlayerRoundBuilder.TravelSignAlongLocalX(speed);
            string meshPath = MeshFolder + "/EnemyRoundStreak " + root.name + ".asset";
            Mesh streak = PlayerRoundBuilder.BuildStreakMesh(
                1f / round.RootScale, behind, meshPath, StreakLength, StreakHeadWidth, StreakTailWidth, origin);

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = root.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = streak;

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<MeshRenderer>();
            }

            renderer.sharedMaterial = trail;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;

            // Small movers stay out of the ray tracing structure (16 October 2026).
            renderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;

            PrefabUtility.SavePrefabAsset(root);
            log.AppendFormat("{0}: scale {1}, damage {2}, life {3} s, streak behind {4}X.\n",
                round.Path, round.RootScale, Damage, Life, behind > 0f ? "+" : "-");
        }
    }
}
