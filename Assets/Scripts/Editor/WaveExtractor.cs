using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds a WaveDefinition from the Spawner components in the currently open
    /// scene, so the 21 hand-tuned spawners transfer exactly rather than being
    /// retyped.
    ///
    /// Spawners that are inactive in the hierarchy are skipped and reported.
    /// Two of them sit under the DeathMenu and EscMenu, where they never run
    /// today - carrying them over would spawn enemies the game does not
    /// currently spawn.
    /// </summary>
    public static class WaveExtractor
    {
        private const string FolderPath = "Assets/Content/Waves";
        private const string AssetPath = FolderPath + "/MainRun.asset";

        /// <summary>Matches the hardcoded floor in the original Spawner.</summary>
        private const float MinInterval = 0.1f;

        /// <summary>Matches the hardcoded cutoff in the original Spawner.</summary>
        private const float StopSpawningAt = 301f;

        [MenuItem("Survival Chaos/Extract Wave From Open Scene")]
        public static void Extract()
        {
            Spawner[] spawners = Object.FindObjectsByType<Spawner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (spawners.Length == 0)
            {
                Debug.LogWarning("No Spawner components in the open scene. Open Game.unity first.");
                return;
            }

            List<SpawnStream> streams = new List<SpawnStream>();
            List<string> skipped = new List<string>();

            foreach (Spawner spawner in spawners)
            {
                if (!spawner.gameObject.activeInHierarchy)
                {
                    skipped.Add(HierarchyPath(spawner.transform));
                    continue;
                }

                SpawnStream stream = ToStream(spawner);
                if (stream != null)
                {
                    streams.Add(stream);
                }
            }

            // Ordering by first spawn makes the run's escalation readable in the
            // inspector; it has no effect on behaviour.
            streams = streams.OrderBy(s => s.startDelay).ThenBy(s => s.label).ToList();

            Directory.CreateDirectory(FolderPath);
            AssetDatabase.Refresh();

            WaveDefinition wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(AssetPath);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveDefinition>();
                AssetDatabase.CreateAsset(wave, AssetPath);
            }

            wave.SetStreams(streams);

            SerializedObject serialized = new SerializedObject(wave);
            serialized.FindProperty("stopSpawningAt").floatValue = StopSpawningAt;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(wave);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Wrote {streams.Count} spawn streams to {AssetPath}.");

            if (skipped.Count > 0)
            {
                Debug.LogWarning(
                    $"Skipped {skipped.Count} inactive spawner(s), which do not spawn in the current build:\n  " +
                    string.Join("\n  ", skipped));
            }
        }

        private static SpawnStream ToStream(Spawner spawner)
        {
            SerializedObject serialized = new SerializedObject(spawner);

            GameObject prefab = serialized.FindProperty("spawnPrefab").objectReferenceValue as GameObject;
            if (prefab == null)
            {
                Debug.LogWarning($"Spawner on {spawner.name} has no prefab; skipped.", spawner);
                return null;
            }

            Transform t = spawner.transform;

            return new SpawnStream
            {
                label = $"{spawner.name} -> {prefab.name}",
                prefab = prefab,
                position = t.position,
                rotation = t.rotation,
                xOffsetRange = ReadRange(serialized, "rangeX"),
                yOffsetRange = ReadRange(serialized, "rangeY"),
                startDelay = serialized.FindProperty("initialDelay").floatValue,
                interval = serialized.FindProperty("spawnDelay").floatValue,
                intervalScale = serialized.FindProperty("spawnRateIncrease").floatValue,
                rampEvery = serialized.FindProperty("spawnIncreaseDelay").floatValue,
                minInterval = MinInterval
            };
        }

        private static Vector2 ReadRange(SerializedObject serialized, string propertyName)
        {
            SerializedProperty range = serialized.FindProperty(propertyName);
            if (range == null)
            {
                return Vector2.zero;
            }

            SerializedProperty min = range.FindPropertyRelative("min");
            SerializedProperty max = range.FindPropertyRelative("max");

            return new Vector2(
                min != null ? min.floatValue : 0f,
                max != null ? max.floatValue : 0f);
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
