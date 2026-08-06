using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Creates the skill assets matching the behaviour the game shipped with.
    /// Re-running updates the existing assets in place rather than duplicating
    /// them, so it is safe to invoke after pulling changes.
    /// </summary>
    public static class DefaultSkillAssets
    {
        private const string FolderPath = "Assets/Data/Skills";

        [MenuItem("Survival Chaos/Create Default Skill Assets")]
        public static void Create()
        {
            Directory.CreateDirectory(FolderPath);
            AssetDatabase.Refresh();

            // maxPicks 0 means unlimited - Heal is the fallback that keeps the
            // pool from ever running dry.
            CreateOrUpdate<ShotUpgradeSkill>("ShotUpgrade", "More Shots!", Player.MaxShotUpgrades, null);
            CreateOrUpdate<MaxHealthSkill>("MaxHealth", "Max HP Increased!", 1, 20);
            CreateOrUpdate<AttackSpeedSkill>("AttackSpeed", "Attack Speed Increased!", 3, null);
            CreateOrUpdate<HealSkill>("Heal", "Heal!", 0, 5);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Default skill assets written to {FolderPath}. " +
                      "Assign them to the SkillSelect component's Skills list.");
        }

        private static void CreateOrUpdate<T>(string fileName, string displayName, int maxPicks, int? amount)
            where T : SkillDefinition
        {
            string path = $"{FolderPath}/{fileName}.asset";

            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("maxPicks").intValue = maxPicks;

            if (amount.HasValue)
            {
                SerializedProperty amountProperty = serialized.FindProperty("amount");
                if (amountProperty != null)
                {
                    amountProperty.intValue = amount.Value;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
