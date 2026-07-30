using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Creates enemy definitions matching the stats currently baked into the
    /// prefabs, then assigns each one to its prefab.
    ///
    /// Health came from each prefab's serialized healthPoints; the reward came
    /// from the hardcoded EXPGain on whichever script that prefab used - which
    /// is why two prefabs sharing a script could not differ in reward.
    ///
    /// Re-running updates assets and assignments in place.
    /// </summary>
    public static class DefaultEnemyAssets
    {
        private const string FolderPath = "Assets/Content/Enemies";

        [MenuItem("Survival Chaos/Create Default Enemy Assets")]
        public static void Create()
        {
            Directory.CreateDirectory(FolderPath);
            AssetDatabase.Refresh();

            //                                     display    health  reward
            EnemyDefinition fighter = CreateOrUpdate("Fighter", "Fighter", 2, 15);
            EnemyDefinition heavy = CreateOrUpdate("Heavy", "Heavy", 7, 15);
            EnemyDefinition scout = CreateOrUpdate("Scout", "Scout", 1, 5);
            EnemyDefinition drone = CreateOrUpdate("Drone", "Drone", 1, 5);
            EnemyDefinition boss = CreateOrUpdate("Boss", "Boss", 300, 2);

            AssetDatabase.SaveAssets();

            int wired = 0;
            wired += AssignToPrefab("Assets/Enemy/Enemy.prefab", fighter) ? 1 : 0;
            wired += AssignToPrefab("Assets/Enemy/Enemy 1.prefab", heavy) ? 1 : 0;
            wired += AssignToPrefab("Assets/Enemy/Enemy 2.prefab", scout) ? 1 : 0;
            wired += AssignToPrefab("Assets/Enemy/Enemy 3.prefab", drone) ? 1 : 0;
            wired += AssignToPrefab("Assets/Boss/Boss.prefab", boss) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Enemy definitions written to {FolderPath}; {wired} of 5 prefabs wired.");
        }

        private static EnemyDefinition CreateOrUpdate(string fileName, string displayName, int maxHealth, int reward)
        {
            string path = $"{FolderPath}/{fileName}.asset";

            EnemyDefinition asset = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("maxHealth").intValue = maxHealth;
            serialized.FindProperty("experienceReward").intValue = reward;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Sets the "definition" field on whichever component of the prefab has
        /// one. Uses LoadPrefabContents/SaveAsPrefabAsset because editing a
        /// prefab asset's components in place does not reliably persist.
        /// </summary>
        private static bool AssignToPrefab(string prefabPath, EnemyDefinition definition)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"Enemy prefab not found, skipped: {prefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;

            try
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    SerializedObject serialized = new SerializedObject(behaviour);
                    SerializedProperty property = serialized.FindProperty("definition");

                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    property.objectReferenceValue = definition;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                else
                {
                    Debug.LogWarning($"No component with a definition field on {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }
    }
}
