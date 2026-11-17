using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Sets up the part pickups (17 November 2026): the glow behind an offer's
    /// part, a copy of the boss lance's additive glow so neither moves when the
    /// other is tuned, and the pickup prefab pointed at it. The pickup prefab
    /// is edited through SerializedObject on the asset, not a prefab stage.
    /// Running it again changes nothing that is already right.
    /// </summary>
    public static class PartPickups1117
    {
        public const string GlowPath = "Assets/Art/Materials/VFX/PartPickupGlow.mat";
        public const string PickupPath = "Assets/Prefabs/Pickups/Pickup.prefab";
        private const string SourcePath = "Assets/Art/Materials/VFX/BossLanceGlow.mat";

        /// <summary>The least a part is drawn on the ring, against its size on the hull.</summary>
        public const float PartSize = 3f;

        /// <summary>The most a part is drawn on the ring, against its size on the hull.</summary>
        public const float PartSizeMost = 5.5f;

        /// <summary>Half the length, in world units, the parts are drawn toward on the ring.</summary>
        public const float PartRadius = 0.9f;

        [MenuItem("Survival Chaos/Gameplay/Set Up Part Pickups (17 Nov)")]
        private static void FromMenu()
        {
            Debug.Log(Apply());
        }

        public static string Apply()
        {
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);

            if (glow == null)
            {
                if (!AssetDatabase.CopyAsset(SourcePath, GlowPath))
                {
                    return "Could not copy " + SourcePath + ". Nothing was changed.";
                }

                glow = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            }

            // Tinted per pickup through a property block; white here so an
            // untinted one is not the boss's red.
            glow.SetColor("_GlowColor", Color.white);
            glow.SetFloat("_GlowIntensity", 1f);
            EditorUtility.SetDirty(glow);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
            Pickup pickup = prefab != null ? prefab.GetComponent<Pickup>() : null;

            if (pickup == null)
            {
                return "No Pickup on " + PickupPath + ".";
            }

            var so = new SerializedObject(pickup);
            so.FindProperty("partGlow").objectReferenceValue = glow;
            // Drawn big enough to read as a shape in a fight under the 85 degree
            // camera (Ian, 17 November 2026): toward a half-length of 0.9, 3 to
            // 5.5 times the part's size on the hull.
            so.FindProperty("partSize").floatValue = PartSize;
            so.FindProperty("partSizeMost").floatValue = PartSizeMost;
            so.FindProperty("partRadius").floatValue = PartRadius;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();

            return "Part pickups set up: " + GlowPath + " on " + PickupPath + ".";
        }
    }
}
