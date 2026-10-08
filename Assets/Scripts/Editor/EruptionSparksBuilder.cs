using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The volcano's embers and lava bombs: their material, the prefab that
    /// holds the particle systems, its instance under the Game scene's
    /// Eruption, and the mesh colliders the bombs land on.
    ///
    /// Environment roadmap, item 38, 5 October 2026. Particles, where the
    /// smoke is fog: sparks and thrown rock are small bright points, which is
    /// what particles are for and what the fog's coarse grid cannot draw.
    ///
    /// Everything about the look is a number in this file. Re-running puts the
    /// prefab back to what is written here and leaves the scene's instance in
    /// place; what EruptionSparks itself is told - how many, how often - is on
    /// the prefab's root and is kept across a rebuild.
    ///
    /// **The brightness is the material's, and the hue is the particle's.**
    /// A particle's colour is stored in eight bits a channel, so a start
    /// colour of (3, 1, 0.2) arrives as (1, 1, 0.2): the first build of this
    /// threw yellow sparks. So the start colours are plain oranges, and the
    /// material's tint multiplies all of them by <see cref="Brightness"/>.
    /// That is kept low on purpose: an additive particle much past 3 in its
    /// brightest channel comes out of the tonemapper white.
    ///
    /// The material ignores fog. The sparks fly inside the plume, which is
    /// fog at its thickest, and with fog on only their tails could be seen.
    /// </summary>
    public static class EruptionSparksBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/VFX/EruptionSparks.prefab";
        public const string MaterialPath = "Assets/Art/Materials/VFX/Embers.mat";
        /// <summary>
        /// The instance's name in the scene. Saving a prefab names its root
        /// after the file, so this is the file's name: the first build looked
        /// for "Eruption Sparks", did not find the one it had placed, and
        /// placed a second.
        /// </summary>
        public const string RootName = "EruptionSparks";

        /// <summary>The fireflies' material is the pattern: the HDRP sample's unlit particle graph, additive, on a soft dot.</summary>
        private const string PatternPath = "Assets/Art/Materials/VFX/Fireflies.mat";

        /// <summary>How far over the crater's mouth they start, which is just clear of its rim.</summary>
        private const float OverTheMouth = 0.7f;

        // The throw. EruptionSparksTests works the furthest landing out of
        // these as they stand on the prefab, and holds it inside the limit.
        public const float BombSpeedLow = 5f;
        public const float BombSpeedHigh = 8f;
        public const float BombSpread = 17f;
        public const float BombGravity = 0.6f;

        /// <summary>What the material multiplies every spark by.</summary>
        public const float Brightness = 2.6f;

        private static readonly Color EmberHot = new Color(1f, 0.42f, 0.10f, 1f);
        private static readonly Color EmberDull = new Color(1f, 0.24f, 0.05f, 1f);
        private static readonly Color BombHot = new Color(1f, 0.50f, 0.14f, 1f);
        private static readonly Color BombDull = new Color(1f, 0.32f, 0.07f, 1f);

        /// <summary>The scenery a bomb can land on.</summary>
        private static readonly string[] Ground = { "ilha principal 1", "ilha cone", "ilha lava" };

        [MenuItem("Survival Chaos/Environment/Build Eruption Sparks", priority = 111)]
        public static void Build()
        {
            Material pattern = AssetDatabase.LoadAssetAtPath<Material>(PatternPath);
            if (pattern == null)
            {
                Debug.LogError("Eruption sparks: " + PatternPath + " is missing.");
                return;
            }

            Eruption eruption = Object.FindAnyObjectByType<Eruption>();
            if (eruption == null)
            {
                Debug.LogError("Eruption sparks: there is no Eruption in the open scene. Open the Game scene.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(pattern);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetColor("_AlbedoColor", new Color(Brightness, Brightness, Brightness, 1f));
            material.SetFloat("_EnableFogOnTransparent", 0f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            // Built loose, saved as the prefab, then thrown away: the scene
            // gets an instance of the asset, so the two cannot differ.
            GameObject root = new GameObject(RootName);
            EruptionSparks kept = null;
            GameObject old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            try
            {
                ParticleSystem embers = Embers(root.transform, material);
                ParticleSystem bombs = Bombs(root.transform, material);

                EruptionSparks sparks = root.AddComponent<EruptionSparks>();

                // What was dialled in on the old prefab outlives the rebuild.
                if (old != null && old.TryGetComponent(out kept))
                {
                    EditorUtility.CopySerialized(kept, sparks);
                }

                SerializedObject wiring = new SerializedObject(sparks);
                wiring.FindProperty("embers").objectReferenceValue = embers;
                wiring.FindProperty("bombs").objectReferenceValue = bombs;
                wiring.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform placed = eruption.transform.Find(RootName);

            if (placed == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, eruption.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Build Eruption Sparks");
                placed = instance.transform;
            }

            placed.SetPositionAndRotation(VolcanoSmokeBuilder.Crater + Vector3.up * OverTheMouth, Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placed);

            int added = 0;

            foreach (string name in Ground)
            {
                GameObject ground = GameObject.Find(name);

                if (ground == null || !ground.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    Debug.LogWarning("Eruption sparks: found no mesh called " + name + ", so bombs fall through where it was.");
                    continue;
                }

                if (!ground.TryGetComponent(out MeshCollider _))
                {
                    MeshCollider collider = Undo.AddComponent<MeshCollider>(ground);
                    collider.sharedMesh = filter.sharedMesh;
                    added++;
                }

                PreBakeCollision(filter.sharedMesh);
            }

            EditorSceneManager.MarkSceneDirty(eruption.gameObject.scene);
            Debug.Log("Eruption sparks built: " + PrefabPath + ", placed under " + eruption.name + ", "
                      + added + " mesh collider(s) added.", placed);
        }

        /// <summary>
        /// Has the mesh carry its collision data ready-made. A build still
        /// makes it for a mesh that does not, and warns that a later Unity
        /// will stop: the first build with these colliders had that warning,
        /// the only one in it. The three meshes are assets of their own, made
        /// by a one-off split of the island (the tool that did it,
        /// SplitIslandForLightmapping, was removed once its work was done and
        /// is in the history), so the setting is on the mesh and not on a
        /// model's import settings.
        /// </summary>
        private static void PreBakeCollision(Mesh mesh)
        {
            SerializedObject asset = new SerializedObject(mesh);
            SerializedProperty triangles = asset.FindProperty("m_PreBakeTriangleCollisionMesh");

            if (triangles == null || triangles.boolValue)
            {
                return;
            }

            triangles.boolValue = true;
            asset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
        }

        private static ParticleSystem New(Transform parent, string name, Material material)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(parent, false);

            // A cone throws along its own forward; this turns forward up.
            holder.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem system = holder.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startRotation = 0f;

            // Nothing leaves a system that has not been told to.
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            return system;
        }

        /// <summary>Bright, then gone: full by a twentieth of its life, fading over the last <paramref name="fadeFrom"/> of it.</summary>
        private static void Fade(ParticleSystem system, float fadeFrom, Color cooled)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, fadeFrom), new GradientColorKey(cooled, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(1f, fadeFrom), new GradientAlphaKey(0f, 1f) });

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Shrink(ParticleSystem system, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, to));
        }

        private static ParticleSystem Embers(Transform parent, Material material)
        {
            ParticleSystem system = New(parent, "Embers", material);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(EmberDull, EmberHot);
            main.gravityModifier = 0f;
            main.maxParticles = 600;

            // EruptionSparks sets the rate each frame; this is what shows in
            // the editor's preview.
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 12f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.8f;

            // Downwind, the way the plume leans: along world X.
            ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;

            Fade(system, 0.55f, new Color(1f, 0.45f, 0.25f));
            Shrink(system, 0.35f);
            return system;
        }

        private static ParticleSystem Bombs(Transform parent, Material material)
        {
            ParticleSystem system = New(parent, "Lava Bombs", material);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 5f;

            // Longer than any flight: a bomb ends by landing, or by the limit.
            main.startLifetime = 8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(BombSpeedLow, BombSpeedHigh);
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.44f);
            main.startColor = new ParticleSystem.MinMaxGradient(BombDull, BombHot);
            main.gravityModifier = BombGravity;
            main.maxParticles = 96;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = BombSpread;
            shape.radius = 0.5f;

            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = LayerMask.GetMask("Default");
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.5f;
            collision.enableDynamicColliders = false;
            collision.sendCollisionMessages = false;

            ParticleSystem tail = Tail(system.transform, material);
            ParticleSystem splash = Splash(system.transform, material);
            ParticleSystem glow = Glow(system.transform, material);

            ParticleSystem.SubEmittersModule children = system.subEmitters;
            children.enabled = true;
            children.AddSubEmitter(tail, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
            children.AddSubEmitter(splash, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
            children.AddSubEmitter(glow, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
            return system;
        }

        /// <summary>Sparks shed along a bomb's flight, which stay where they were shed: its tail.</summary>
        private static ParticleSystem Tail(Transform parent, Material material)
        {
            ParticleSystem system = New(parent, "Bomb Tail", material);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(EmberDull, EmberHot);
            main.maxParticles = 1600;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 70f;

            Fade(system, 0.2f, new Color(1f, 0.4f, 0.2f));
            Shrink(system, 0.2f);
            return system;
        }

        /// <summary>The sparks a bomb scatters where it lands.</summary>
        private static ParticleSystem Splash(Transform parent, Material material)
        {
            ParticleSystem system = New(parent, "Bomb Splash", material);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(EmberDull, EmberHot);
            main.gravityModifier = 0.5f;
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.15f;

            Fade(system, 0.3f, new Color(1f, 0.4f, 0.2f));
            Shrink(system, 0.3f);
            return system;
        }

        /// <summary>The spot a landed bomb leaves glowing while it cools.</summary>
        private static ParticleSystem Glow(Transform parent, Material material)
        {
            ParticleSystem system = New(parent, "Bomb Glow", material);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(BombDull, BombHot);
            main.maxParticles = 96;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            Fade(system, 0.1f, new Color(0.8f, 0.2f, 0.1f));
            return system;
        }
    }
}
