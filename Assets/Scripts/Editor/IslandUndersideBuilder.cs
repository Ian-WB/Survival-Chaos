using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Gives the island's underside something to show (environment roadmap,
    /// item 44). From the lane you look at it for the whole run and it was
    /// a black shape: nothing lit it and nothing hung from it.
    ///
    /// **Hanging rock.** Shards hang from the underside, point down. Each
    /// is found by looking up at the island's own collider from below, so
    /// it starts in the rock wherever the rock is; the ones that would hang
    /// from a face that does not look down are thrown away. They are the
    /// debris ring's shard meshes, stretched, on the island's dark rock
    /// material. They take part in the bake and are lit by the probes, as
    /// the trees are, because the meshes have no lightmap UVs.
    ///
    /// **The underglow.** One light under the rim where the lava falls
    /// away, so the fall lights the rock it passes and the haze under it.
    /// It is a copy of a lava light, so it carries the same colour, the
    /// same cached shadow and the same fixed shadow size; it is Realtime,
    /// which the lava lights are not since item 45, because the haze only
    /// takes light from a light that is live.
    ///
    /// Both go under one object, <see cref="RootName"/>, which building
    /// again replaces. The dice are seeded.
    /// </summary>
    public static class IslandUndersideBuilder
    {
        public const string RootName = "Island Underside";
        public const string GlowName = "Underglow";
        private const string IslandName = "ilha principal 1";
        private const string LavaName = "ilha lava";
        private const string LightToCopy = "Lava Light 5";
        private const string MaterialPath = "Assets/Art/Materials/Scenario/pedra.mat";

        private const int Seed = 44;
        private const int Tries = 90;
        private const int Most = 36;

        /// <summary>How far from the axis the rock hangs: not under the middle, which nobody sees, and not past the rim.</summary>
        private const float NearestOut = 5f;
        private const float FurthestOut = 17.5f;

        /// <summary>A hanging rock's length and its width.</summary>
        private const float Shortest = 1.6f;
        private const float Longest = 5f;
        private const float Thinnest = 0.9f;
        private const float Widest = 2.2f;

        /// <summary>How far under the lava's lowest point the underglow sits, how far it reaches and how much of it the haze takes.</summary>
        private const float GlowDrop = 2.5f;
        private const float GlowRange = 16f;
        private const float GlowInHaze = 0.5f;

        [MenuItem("Survival Chaos/Environment/Build Island Underside", priority = 116)]
        public static void Build()
        {
            GameObject island = GameObject.Find(IslandName);
            GameObject lava = GameObject.Find(LavaName);
            GameObject lightToCopy = GameObject.Find(LightToCopy);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            List<Mesh> shards = new List<Mesh>();

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(DebrisRingBuilder.MeshPath))
            {
                if (asset is Mesh mesh && mesh.name.StartsWith("Shard"))
                {
                    shards.Add(mesh);
                }
            }

            shards.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (island == null || !island.TryGetComponent(out MeshCollider rock) || lava == null || lightToCopy == null
                || material == null || shards.Count == 0)
            {
                Debug.LogError("Island underside: needs the Game scene open, with " + IslandName + " (and its collider), " +
                               LavaName + " and " + LightToCopy + ", and the debris ring built.");
                return;
            }

            GameObject old = GameObject.Find(RootName);

            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Island Underside");
            System.Random dice = new System.Random(Seed);
            int hung = 0;

            for (int i = 0; i < Tries && hung < Most; i++)
            {
                float bearing = Range(dice, 0f, Mathf.PI * 2f);
                float out_ = Range(dice, NearestOut, FurthestOut);
                float length = Range(dice, Shortest, Longest);
                float width = Range(dice, Thinnest, Widest);
                int kind = dice.Next(shards.Count);
                float spin = Range(dice, 0f, 360f);

                Vector3 below = new Vector3(Mathf.Cos(bearing) * out_, -60f, Mathf.Sin(bearing) * out_);

                // Looking up: the first rock met is the underside. A face that does not look down is a cliff's foot or the rim.
                if (!rock.Raycast(new Ray(below, Vector3.up), out RaycastHit hit, 120f) || hit.normal.y > -0.35f)
                {
                    continue;
                }

                // Mostly straight down, leaning a little the way the rock faces.
                Vector3 along = (Vector3.down * 0.8f + hit.normal * 0.2f).normalized;

                GameObject shard = new GameObject("Hanging Rock " + hung);
                shard.transform.SetParent(root.transform, false);
                // The shards are longest along their own x. A quarter of the length is inside the rock.
                shard.transform.SetPositionAndRotation(
                    hit.point + along * (length * 0.25f),
                    Quaternion.AngleAxis(spin, along) * Quaternion.FromToRotation(Vector3.right, along));
                shard.transform.localScale = new Vector3(length, width, width);

                shard.AddComponent<MeshFilter>().sharedMesh = shards[kind];
                MeshRenderer renderer = shard.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveGI = ReceiveGI.LightProbes;
                renderer.renderingLayerMask = 1;
                GameObjectUtility.SetStaticEditorFlags(shard,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic);
                hung++;
            }

            // Where the lava falls away: under its lowest point.
            Mesh lavaMesh = lava.GetComponent<MeshFilter>().sharedMesh;
            Vector3 lowest = Vector3.up * float.MaxValue;

            foreach (Vector3 corner in lavaMesh.vertices)
            {
                Vector3 world = lava.transform.TransformPoint(corner);

                if (world.y < lowest.y)
                {
                    lowest = world;
                }
            }

            GameObject glow = Object.Instantiate(lightToCopy, root.transform);
            glow.name = GlowName;
            glow.transform.SetPositionAndRotation(lowest + Vector3.down * GlowDrop, Quaternion.identity);

            Light light = glow.GetComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.range = GlowRange;
            light.renderingLayerMask = 1;
            glow.GetComponent<HDAdditionalLightData>().volumetricDimmer = GlowInHaze;

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Island underside built: " + hung + " hanging rocks, and the underglow at " + glow.transform.position.ToString("0.0") +
                      ". Save the scene, and bake the lighting again.", root);
        }

        private static float Range(System.Random dice, float from, float to)
        {
            return Mathf.Lerp(from, to, (float)dice.NextDouble());
        }
    }
}
