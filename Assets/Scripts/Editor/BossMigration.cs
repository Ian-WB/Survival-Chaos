using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Converts the Boss prefab from BossScript to BossEmitter, reading the 32
    /// pivot references and 4 projectile references off the old component so
    /// nothing is re-wired by hand.
    ///
    /// The three original methods map to three attacks:
    ///   Shoot()   pivots 0-15,          shootPrefab   / shootPrefab_1
    ///   Shoot_2() pivots 16-28,         shootPrefab   / shootPrefab_1
    ///   Shoot_1() pivots 16, 29, 30, 31, shootPrefab_2 / shootPrefab_3, laser-gated
    /// </summary>
    public static class BossMigration
    {
        private const string PrefabPath = "Assets/Boss/Boss.prefab";

        [MenuItem("Survival Chaos/Migrate Boss To Emitter")]
        public static void Migrate()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"Boss prefab not found at {PrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                BossScript old = root.GetComponentInChildren<BossScript>(true);
                if (old == null)
                {
                    Debug.LogWarning("No BossScript on the Boss prefab - already migrated?");
                    return;
                }

                if (old.GetComponent<BossEmitter>() != null)
                {
                    Debug.LogWarning("A BossEmitter is already present; aborting to avoid duplicates.");
                    return;
                }

                SerializedObject src = new SerializedObject(old);

                Transform[] pivots = ReadPivots(src);
                int missing = 0;
                foreach (Transform pivot in pivots)
                {
                    if (pivot == null)
                    {
                        missing++;
                    }
                }

                GameObject projectileA = ObjectField(src, "shootPrefab");
                GameObject projectileB = ObjectField(src, "shootPrefab_1");
                GameObject projectileC = ObjectField(src, "shootPrefab_2");
                GameObject projectileD = ObjectField(src, "shootPrefab_3");

                float initialDelay = src.FindProperty("initialDelay").floatValue;
                float interval = src.FindProperty("spawnDelay").floatValue;

                List<BossAttack> attacks = new List<BossAttack>
                {
                    new BossAttack
                    {
                        label = "Volley (was Shoot)",
                        pivots = Slice(pivots, Indices(0, 15)),
                        projectileWhenLeft = projectileA,
                        projectileWhenRight = projectileB,
                        trigger = AttackTrigger.Repeating,
                        initialDelay = initialDelay,
                        interval = interval
                    },
                    new BossAttack
                    {
                        label = "Spread (was Shoot_2)",
                        pivots = Slice(pivots, Indices(16, 28)),
                        projectileWhenLeft = projectileA,
                        projectileWhenRight = projectileB,
                        trigger = AttackTrigger.Repeating,
                        initialDelay = initialDelay,
                        interval = interval
                    },
                    new BossAttack
                    {
                        label = "Laser (was Shoot_1)",
                        pivots = Slice(pivots, new[] { 16, 29, 30, 31 }),
                        projectileWhenLeft = projectileC,
                        projectileWhenRight = projectileD,
                        trigger = AttackTrigger.WhileLaserActive,
                        initialDelay = 0f,

                        // 0 reproduces the original exactly: Shoot_1 was called
                        // straight from Update, so it fired every frame. That is
                        // roughly 240 projectiles a second at 60fps. Raise this
                        // to throttle it - the field exists so it is one edit.
                        interval = 0f
                    }
                };

                BossEmitter emitter = old.gameObject.AddComponent<BossEmitter>();
                SerializedObject dst = new SerializedObject(emitter);

                // Not ObjectField: definition is a ScriptableObject, so casting
                // it to GameObject would silently write null.
                SerializedProperty definition = src.FindProperty("definition");
                dst.FindProperty("definition").objectReferenceValue =
                    definition != null ? definition.objectReferenceValue : null;
                dst.FindProperty("healthPoints").intValue = src.FindProperty("healthPoints").intValue;
                dst.FindProperty("enemyShip").objectReferenceValue = ObjectField(src, "EnemyShip");

                SerializedProperty list = dst.FindProperty("attacks");
                list.arraySize = attacks.Count;

                for (int i = 0; i < attacks.Count; i++)
                {
                    WriteAttack(list.GetArrayElementAtIndex(i), attacks[i]);
                }

                dst.ApplyModifiedPropertiesWithoutUndo();

                Object.DestroyImmediate(old, true);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                Debug.Log($"Boss migrated to BossEmitter: 3 attacks, " +
                          $"{attacks[0].pivots.Length}/{attacks[1].pivots.Length}/{attacks[2].pivots.Length} pivots. " +
                          $"BossScript component removed." +
                          (missing > 0 ? $" WARNING: {missing} pivot reference(s) were empty." : string.Empty));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WriteAttack(SerializedProperty element, BossAttack attack)
        {
            element.FindPropertyRelative("label").stringValue = attack.label;
            element.FindPropertyRelative("projectileWhenLeft").objectReferenceValue = attack.projectileWhenLeft;
            element.FindPropertyRelative("projectileWhenRight").objectReferenceValue = attack.projectileWhenRight;
            element.FindPropertyRelative("trigger").enumValueIndex = (int)attack.trigger;
            element.FindPropertyRelative("initialDelay").floatValue = attack.initialDelay;
            element.FindPropertyRelative("interval").floatValue = attack.interval;

            SerializedProperty pivots = element.FindPropertyRelative("pivots");
            pivots.arraySize = attack.pivots.Length;

            for (int i = 0; i < attack.pivots.Length; i++)
            {
                pivots.GetArrayElementAtIndex(i).objectReferenceValue = attack.pivots[i];
            }
        }

        /// <summary>Reads shootPivot, then shootPivot_1 .. shootPivot_31, in order.</summary>
        private static Transform[] ReadPivots(SerializedObject src)
        {
            Transform[] pivots = new Transform[32];
            pivots[0] = src.FindProperty("shootPivot")?.objectReferenceValue as Transform;

            for (int i = 1; i < 32; i++)
            {
                pivots[i] = src.FindProperty($"shootPivot_{i}")?.objectReferenceValue as Transform;
            }

            return pivots;
        }

        private static GameObject ObjectField(SerializedObject src, string name)
        {
            SerializedProperty property = src.FindProperty(name);
            return property != null ? property.objectReferenceValue as GameObject : null;
        }

        private static int[] Indices(int first, int last)
        {
            int[] result = new int[last - first + 1];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = first + i;
            }

            return result;
        }

        private static Transform[] Slice(Transform[] source, int[] indices)
        {
            List<Transform> result = new List<Transform>(indices.Length);

            foreach (int index in indices)
            {
                if (index >= 0 && index < source.Length && source[index] != null)
                {
                    result.Add(source[index]);
                }
            }

            return result.ToArray();
        }
    }
}
