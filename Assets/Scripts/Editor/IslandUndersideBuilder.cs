using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Lights the island's underside (environment roadmap, item 44). From
    /// the lane you look at it for the whole run and it was a black shape:
    /// nothing lit it.
    ///
    /// **The underglow.** One light under the rim where the lava falls
    /// away, so the fall lights the rock it passes and the haze under it.
    /// It is a copy of a lava light, so it carries the same colour, the
    /// same cached shadow and the same fixed shadow size; it is Realtime,
    /// which the lava lights are not since item 45, because the haze only
    /// takes light from a light that is live.
    ///
    /// It goes under one object, <see cref="RootName"/>, which building
    /// again replaces.
    ///
    /// **No hanging rock.** Item 44 also hung 36 shards from the underside,
    /// point down: the debris ring's meshes, stretched. Ian looked at them
    /// in a build on 12 October, did not like them, and had them taken out.
    /// Do not hang rock here again.
    /// </summary>
    public static class IslandUndersideBuilder
    {
        public const string RootName = "Island Underside";
        public const string GlowName = "Underglow";
        private const string LavaName = "ilha lava";
        private const string LightToCopy = "Lava Light 5";

        /// <summary>How far under the lava's lowest point the underglow sits, how far it reaches and how much of it the haze takes.</summary>
        private const float GlowDrop = 2.5f;
        private const float GlowRange = 16f;
        private const float GlowInHaze = 0.5f;

        [MenuItem("Survival Chaos/Environment/Build Island Underside", priority = 116)]
        public static void Build()
        {
            GameObject lava = GameObject.Find(LavaName);
            GameObject lightToCopy = GameObject.Find(LightToCopy);

            if (lava == null || lightToCopy == null)
            {
                Debug.LogError("Island underside: needs the Game scene open, with " + LavaName + " and " + LightToCopy + ".");
                return;
            }

            GameObject old = GameObject.Find(RootName);

            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Island Underside");

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
            Debug.Log("Island underside built: the underglow at " + glow.transform.position.ToString("0.0") + ". Save the scene.", root);
        }
    }
}
