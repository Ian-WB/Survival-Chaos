using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Splits an effects pack's container prefab into one standalone prefab per
    /// effect.
    ///
    /// Packs ship as a single prefab with every effect parented under one root,
    /// laid out side by side so they can all be seen at once in a scene view.
    /// That is convenient to browse and useless to spawn: ObjectPool.Spawn takes
    /// a prefab, and a child of a prefab is not one.
    ///
    /// Two things have to be fixed on the way out, and both are easy to miss by
    /// hand across fifteen effects:
    ///
    /// The local transform is zeroed. In the pack each effect sits at its own
    /// offset so they do not overlap on screen. Spawn positions the root, so an
    /// inherited offset would place every explosion a fixed distance from
    /// whatever it was meant to be on - subtly, consistently wrong, and easy to
    /// blame on the spawn code.
    ///
    /// DestroyAfterTime is added if absent. Everything in Prefabs/VFX carries it
    /// already; it is what returns a pooled effect to the pool. Without it a
    /// spawned effect is never despawned, and the pool grows for the whole run
    /// while the screen fills with finished particle systems that have nothing
    /// left to draw.
    /// </summary>
    public static class EffectPrefabExtractor
    {
        private const string PackPath = "Assets/BigRookGames/Subgraphs/Effects Pack 1.prefab";
        private const string OutputFolder = "Assets/Prefabs/VFX/Extracted";

        /// <summary>
        /// Added to the measured particle duration before it becomes the retire
        /// delay, so an effect is never cut off by its own timer while the last
        /// particles are still fading.
        /// </summary>
        private const float RetireMargin = 0.5f;

        [MenuItem("Survival Chaos/Extract Effect Prefabs", priority = 47)]
        public static void Extract()
        {
            GameObject pack = AssetDatabase.LoadAssetAtPath<GameObject>(PackPath);
            if (pack == null)
            {
                Debug.LogError($"No prefab at {PackPath}. Point PackPath at the pack's container " +
                               "prefab and run this again.");
                return;
            }

            EnsureFolder();

            // Unpacked completely, or every child saved below would come out as a
            // variant linked back into the pack rather than a prefab of its own.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(pack);
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Collected first: the loop reparents and destroys as it goes, and
            // iterating a Transform while changing its children skips entries.
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in instance.transform)
            {
                children.Add(child);
            }

            int written = 0;

            foreach (Transform child in children)
            {
                child.SetParent(null, worldPositionStays: false);
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;

                MakeOneShot(child.gameObject);

                float retireAfter = MeasureDuration(child.gameObject) + RetireMargin;
                ApplyRetireTimer(child.gameObject, retireAfter);

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{OutputFolder}/{child.name}.prefab");

                PrefabUtility.SaveAsPrefabAsset(child.gameObject, path);
                Object.DestroyImmediate(child.gameObject);
                written++;

                Debug.Log($"Extracted {path} (retires after {retireAfter:0.##}s).");
            }

            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Extracted {written} effect prefab(s) into {OutputFolder}. Each is spawnable " +
                      "through ObjectPool and retires itself. Delete the ones you do not want - " +
                      "they cost an import each and nothing else.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/VFX", "Extracted");
            }
        }

        /// <summary>
        /// Turns looping off on every particle system in the effect.
        ///
        /// Packs ship their showcase prefab looping so that all the effects play
        /// continuously while you scroll past them in a scene view. That is the
        /// right choice for browsing and the wrong one for a hit spark: spawned
        /// from the pool it restarts forever, and the only thing that ends it is
        /// DestroyAfterTime cutting it off mid-cycle.
        ///
        /// Prewarm goes with it. It means "start mid-cycle as though it had
        /// already been running", which is meaningless without a cycle, and Unity
        /// only honours it on looping systems.
        /// </summary>
        private static void MakeOneShot(GameObject effect)
        {
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = false;
                main.prewarm = false;
            }
        }

        /// <summary>
        /// How long the effect actually plays: the longest duration plus lifetime
        /// of any particle system under it.
        ///
        /// Measured rather than left at DestroyAfterTime's ten second default,
        /// because these fire on every hit. A quarter-second spark held for ten
        /// seconds keeps forty dead objects alive per second of combat - all of
        /// them drawing nothing, all of them still in the pool's active set.
        /// </summary>
        private static float MeasureDuration(GameObject effect)
        {
            float longest = 0f;

            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;

                // constantMax covers constant and random-between-two-constants.
                // For curve modes it is the curve's own maximum, which is the
                // upper bound wanted here either way.
                // Runs after MakeOneShot, so every system here is non-looping and
                // duration + lifetime is when it genuinely finishes.
                float lifetime = main.startLifetime.constantMax;
                float total = main.duration + lifetime;

                if (total > longest)
                {
                    longest = total;
                }
            }

            return longest;
        }

        /// <summary>
        /// Ensures the effect retires itself, and sets the delay to match what was
        /// measured. The field is private and serialized, so it goes through
        /// SerializedObject rather than an assignment.
        /// </summary>
        private static void ApplyRetireTimer(GameObject effect, float seconds)
        {
            if (!effect.TryGetComponent(out DestroyAfterTime timer))
            {
                timer = effect.AddComponent<DestroyAfterTime>();
            }

            SerializedObject serialized = new SerializedObject(timer);
            SerializedProperty delay = serialized.FindProperty("delay");

            if (delay == null)
            {
                Debug.LogWarning($"DestroyAfterTime on {effect.name} has no 'delay' field to set; " +
                                 "it will retire on its own default instead.", effect);
                return;
            }

            delay.floatValue = seconds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
