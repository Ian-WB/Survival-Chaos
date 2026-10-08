using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the ring of rock that drifts round the island (environment
    /// roadmap, item 42): eight small meshes, one material and the prefab,
    /// and puts the prefab in the open scene if it is not there. What the
    /// ring is for and why it cannot cover a ship is on
    /// <see cref="DebrisRing"/>.
    ///
    /// **The rock is made here, not modelled.** A shard is a twenty-sided
    /// ball with its corners pulled in and out and the whole of it squashed;
    /// an islet is a flat top over a point, like the island it broke from.
    /// Every face is flat, as the island's are. Five shards and three
    /// islets, placed 150 times at different sizes and turns.
    ///
    /// **Where a rock stands** is drawn by band. Round the ring the rocks
    /// are spaced evenly and then nudged, so there is no bare stretch and no
    /// pile. Up, each is put where the camera across the island would see it
    /// between <see cref="LowestSeen"/> and <see cref="HighestSeen"/>
    /// degrees over level, mostly low: over the far side of the lane, which
    /// is seen within a few degrees of level, and in front of the storm's
    /// wall and the moon. That also puts every rock above the fog.
    ///
    /// The dice are seeded, so building again gives the same ring. The
    /// meshes are rewritten in place and the prefab saved over itself, so
    /// the scene's instance keeps pointing at them.
    /// </summary>
    public static class DebrisRingBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Scenario/DebrisRing.prefab";
        public const string MeshPath = "Assets/Art/Models/Scenario/DebrisRocks.asset";
        public const string MaterialPath = "Assets/Art/Materials/Scenario/DebrisRock.mat";

        /// <summary>SaveAsPrefabAsset names the root after the file, so this is the file's name.</summary>
        private const string RootName = "DebrisRing";

        private const int Seed = 42;
        private const int ShardKinds = 5;
        private const int IsletKinds = 3;

        /// <summary>The island's own rock, from its material "ilha".</summary>
        private static readonly Color Rock = new Color(0.313f, 0.171f, 0.179f, 1f);

        /// <summary>
        /// A band of the ring: how far from the axis it runs, how many rocks,
        /// how fast it turns in degrees a second, and how big its rocks are
        /// across. The far ones are bigger so they still read as rock, and
        /// slower, as things further out in a ring are.
        /// </summary>
        private readonly struct Band
        {
            public readonly float From, To, Turn, Smallest, Largest;
            public readonly int Count;

            public Band(float from, float to, int count, float turn, float smallest, float largest)
            {
                From = from;
                To = to;
                Count = count;
                Turn = turn;
                Smallest = smallest;
                Largest = largest;
            }
        }

        private static readonly Band[] Bands =
        {
            new Band(DebrisRing.Nearest, 85f, 40, 1.2f, 2f, 5f),
            new Band(85f, 115f, 50, 0.8f, 3f, 7f),
            new Band(115f, DebrisRing.Furthest, 60, 0.5f, 4f, 10f)
        };

        /// <summary>One rock in this many is an islet, and an islet is this much bigger than a shard would be.</summary>
        private const float IsletShare = 0.15f;
        private const float IsletGrowth = 1.9f;

        /// <summary>
        /// How high over level the camera across the island sees a rock, in
        /// degrees. The far side of the lane is within six of level and the
        /// frame's top is thirty-two up.
        /// </summary>
        private const float LowestSeen = 8f;
        private const float HighestSeen = 30f;

        /// <summary>The camera's height at the middle of the band.</summary>
        private const float EyeHeight = 8f;

        /// <summary>How fast a shard three across tumbles, in degrees a second; bigger ones are slower.</summary>
        private const float SlowestTumble = 2f;
        private const float FastestTumble = 7f;

        [MenuItem("Survival Chaos/Build Debris Ring", priority = 60)]
        public static void Build()
        {
            System.Random dice = new System.Random(Seed);

            EnsureFolder("Assets/Prefabs", "Scenario");
            EnsureFolder("Assets/Art/Models", "Scenario");
            EnsureFolder("Assets/Art/Materials", "Scenario");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetColor("_BaseColor", Rock);
            material.SetFloat("_Smoothness", 0.3f);
            material.SetFloat("_Metallic", 0f);
            // Not instanced. The SRP Batcher draws these whether it is on or off, and on, the build
            // compiles HDRP/Lit a second time for it: ten megabytes and four minutes, measured.
            material.enableInstancing = false;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            List<Mesh> shards = new List<Mesh>();
            List<Mesh> islets = new List<Mesh>();
            Dictionary<string, Mesh> stored = new Dictionary<string, Mesh>();

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
            {
                if (asset is Mesh mesh)
                {
                    stored[mesh.name] = mesh;
                }
            }

            for (int i = 0; i < ShardKinds; i++)
            {
                shards.Add(Store(stored, "Shard " + i, Shard(dice)));
            }

            for (int i = 0; i < IsletKinds; i++)
            {
                islets.Add(Store(stored, "Islet " + i, Islet(dice)));
            }

            AssetDatabase.SaveAssets();

            // Seen from across the island: the lane's far side is this far from the default camera.
            float eyeFromAxis = ArenaGeometry.LaneRadius + CameraFraming.Default.Distance;

            GameObject root = new GameObject(RootName);
            int triangles = 0;

            try
            {
                List<Transform> bandRoots = new List<Transform>();
                List<float> turns = new List<float>();
                List<Transform> tumblers = new List<Transform>();
                List<Vector3> tumbles = new List<Vector3>();

                for (int b = 0; b < Bands.Length; b++)
                {
                    Band band = Bands[b];
                    Transform bandRoot = new GameObject("Band " + b).transform;
                    bandRoot.SetParent(root.transform, false);
                    bandRoots.Add(bandRoot);
                    turns.Add(band.Turn);

                    for (int i = 0; i < band.Count; i++)
                    {
                        bool islet = dice.NextDouble() < IsletShare;
                        float bearing = (i + Range(dice, 0.1f, 0.9f)) / band.Count * Mathf.PI * 2f;
                        float out_ = Range(dice, band.From, band.To);
                        // Mostly low: squaring the draw puts half the rocks in the bottom quarter of the range.
                        float draw = Range(dice, 0f, 1f);
                        float seen = Mathf.Lerp(LowestSeen, HighestSeen, draw * draw);
                        float height = EyeHeight + (out_ + eyeFromAxis) * Mathf.Tan(seen * Mathf.Deg2Rad);
                        float size = Range(dice, band.Smallest, band.Largest) * (islet ? IsletGrowth : 1f);

                        Mesh mesh = islet ? islets[dice.Next(islets.Count)] : shards[dice.Next(shards.Count)];
                        GameObject rock = new GameObject((islet ? "Islet " : "Shard ") + b + "." + i);
                        rock.transform.SetParent(bandRoot, false);
                        rock.transform.localPosition = new Vector3(Mathf.Cos(bearing) * out_, height, Mathf.Sin(bearing) * out_);
                        rock.transform.localScale = Vector3.one * size;

                        if (islet)
                        {
                            // Top up, leaning a little, facing anywhere.
                            rock.transform.localRotation =
                                Quaternion.Euler(Range(dice, -8f, 8f), Range(dice, 0f, 360f), Range(dice, -8f, 8f));
                        }
                        else
                        {
                            rock.transform.localRotation =
                                Quaternion.Euler(Range(dice, 0f, 360f), Range(dice, 0f, 360f), Range(dice, 0f, 360f));
                            Vector3 about = new Vector3(Range(dice, -1f, 1f), Range(dice, -1f, 1f), Range(dice, -1f, 1f)).normalized;
                            tumblers.Add(rock.transform);
                            tumbles.Add(about * (Range(dice, SlowestTumble, FastestTumble) * Mathf.Min(1f, 3f / size)));
                        }

                        rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                        MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        // The moon's layer and no other: the ships' lights are on 2 and 4.
                        renderer.renderingLayerMask = 1;
                        triangles += mesh.triangles.Length / 3;
                    }
                }

                DebrisRing ring = root.AddComponent<DebrisRing>();
                SerializedObject wiring = new SerializedObject(ring);
                Fill(wiring.FindProperty("bands"), bandRoots);
                Fill(wiring.FindProperty("tumblers"), tumblers);

                SerializedProperty bandTurn = wiring.FindProperty("bandTurn");
                bandTurn.arraySize = turns.Count;

                for (int i = 0; i < turns.Count; i++)
                {
                    bandTurn.GetArrayElementAtIndex(i).floatValue = turns[i];
                }

                SerializedProperty tumble = wiring.FindProperty("tumble");
                tumble.arraySize = tumbles.Count;

                for (int i = 0; i < tumbles.Count; i++)
                {
                    tumble.GetArrayElementAtIndex(i).vector3Value = tumbles[i];
                }

                wiring.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject placed = GameObject.Find(RootName);

            if (placed == null)
            {
                placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(placed, "Build Debris Ring");
            }

            // The axis the ships go round is the world's.
            placed.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placed.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(placed.scene);

            Debug.Log("Debris ring built: " + PrefabPath + ", " + triangles + " triangles. Save the scene to keep it placed.", prefab);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void Fill(SerializedProperty list, List<Transform> items)
        {
            list.arraySize = items.Count;

            for (int i = 0; i < items.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static float Range(System.Random dice, float from, float to)
        {
            return Mathf.Lerp(from, to, (float)dice.NextDouble());
        }

        /// <summary>Writes a mesh into the asset: over the one of that name if it is there, so nothing that uses it loses it.</summary>
        private static Mesh Store(Dictionary<string, Mesh> stored, string name, Mesh fresh)
        {
            fresh.name = name;

            if (stored.TryGetValue(name, out Mesh kept))
            {
                kept.Clear();
                kept.vertices = fresh.vertices;
                kept.normals = fresh.normals;
                kept.triangles = fresh.triangles;
                kept.RecalculateBounds();
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(kept);
                return kept;
            }

            if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null)
            {
                AssetDatabase.CreateAsset(fresh, MeshPath);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(fresh, MeshPath);
            }

            stored[name] = fresh;
            return fresh;
        }

        /// <summary>A twenty-sided ball with its corners pulled in and out, squashed along two of its axes.</summary>
        private static Mesh Shard(System.Random dice)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            Vector3 squash = new Vector3(1f, Range(dice, 0.45f, 0.8f), Range(dice, 0.55f, 0.95f));

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = Vector3.Scale(corners[i].normalized * Range(dice, 0.62f, 1.25f), squash);
            }

            return Faceted(corners, faces);
        }

        /// <summary>A flat top with a ragged rim, a waist under it and a point at the bottom.</summary>
        private static Mesh Islet(System.Random dice)
        {
            const int sides = 7;
            float depth = Range(dice, 0.9f, 1.5f);
            List<Vector3> corners = new List<Vector3> { new Vector3(0f, 0.05f, 0f) };

            for (int i = 0; i < sides; i++)
            {
                float bearing = (i + Range(dice, -0.3f, 0.3f)) / sides * Mathf.PI * 2f;
                float reach = Range(dice, 0.7f, 1f);
                corners.Add(new Vector3(Mathf.Cos(bearing) * reach, Range(dice, -0.04f, 0.04f), Mathf.Sin(bearing) * reach));
            }

            for (int i = 0; i < sides; i++)
            {
                Vector3 rim = corners[1 + i];
                float pinch = Range(dice, 0.45f, 0.7f);
                corners.Add(new Vector3(rim.x * pinch, -depth * Range(dice, 0.3f, 0.5f), rim.z * pinch));
            }

            corners.Add(new Vector3(Range(dice, -0.15f, 0.15f), -depth, Range(dice, -0.15f, 0.15f)));

            int tip = corners.Count - 1;
            List<int> faces = new List<int>();

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int rim = 1 + i, rimNext = 1 + next;
                int waist = 1 + sides + i, waistNext = 1 + sides + next;

                faces.AddRange(new[] { 0, rim, rimNext });
                faces.AddRange(new[] { rim, waist, waistNext });
                faces.AddRange(new[] { rim, waistNext, rimNext });
                faces.AddRange(new[] { waist, tip, waistNext });
            }

            return Faceted(corners.ToArray(), faces.ToArray());
        }

        /// <summary>
        /// A mesh of flat faces from corners and the triangles between them:
        /// each face gets corners of its own, so its light does not blend
        /// into the next. Every face is turned to look outward, and the
        /// whole is centred and scaled to be one across at its widest.
        /// </summary>
        private static Mesh Faceted(Vector3[] corners, int[] faces)
        {
            Vector3 middle = Vector3.zero;

            foreach (Vector3 corner in corners)
            {
                middle += corner;
            }

            middle /= corners.Length;
            float widest = 0f;

            foreach (Vector3 corner in corners)
            {
                widest = Mathf.Max(widest, (corner - middle).magnitude);
            }

            float scale = 0.5f / widest;
            Vector3[] vertices = new Vector3[faces.Length];
            Vector3[] normals = new Vector3[faces.Length];
            int[] triangles = new int[faces.Length];

            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = (corners[faces[f]] - middle) * scale;
                Vector3 b = (corners[faces[f + 1]] - middle) * scale;
                Vector3 c = (corners[faces[f + 2]] - middle) * scale;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

                if (Vector3.Dot(normal, a + b + c) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }

                vertices[f] = a;
                vertices[f + 1] = b;
                vertices[f + 2] = c;
                normals[f] = normals[f + 1] = normals[f + 2] = normal;
                triangles[f] = f;
                triangles[f + 1] = f + 1;
                triangles[f + 2] = f + 2;
            }

            Mesh mesh = new Mesh { vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
