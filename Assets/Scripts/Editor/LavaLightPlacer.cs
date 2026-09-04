using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Places point lights along the lava.
    ///
    /// Emissive materials cannot cast shadows - shadows are rendered from a
    /// light's point of view, and an emissive surface is not a light. So the
    /// lava's glow on the surrounding rock has to come from real lights sitting
    /// on the emitting surface.
    ///
    /// Rather than have them positioned by hand, this reads the vertices of the
    /// submesh actually using the lava material, splits them into bands by
    /// height, and puts one light per band - centred on the band and sized to
    /// its footprint. That follows the river from the crater down to the pool
    /// automatically.
    ///
    /// This placed rectangle area lights until 2026-09-01. Do not go back to
    /// them. Two things kill it:
    ///
    /// - HDRP only feeds area lights into volumetric fog under path tracing. In
    ///   rasterisation they light surfaces and contribute nothing to the fog, so
    ///   the lava glows on the rock but leaves no glow in the air above it -
    ///   which is most of what the arena's fog is there for.
    /// - A rectangle has edges, and the lighting stops at them. Unless the
    ///   rectangle is placed to match the lava exactly you can see the cut line
    ///   across the rock.
    ///
    /// Point lights have neither problem, and the reason the old hand-placed set
    /// read as fake was never the light type - it was that five of them with a
    /// 100 unit range each are five visible spheres. Many small ones spaced
    /// closer together than they reach read as a continuous river instead.
    ///
    /// Re-running deletes and rebuilds the lights, so it is safe to iterate.
    /// </summary>
    public class LavaLightPlacer : ScriptableWizard
    {
        private const string ContainerName = "Lava Lights";
        private const string LavaMaterialPath = "Assets/Art/Materials/Scenario/lava_.mat";

        [Tooltip("How many lights to spread along the lava, from the crater downward. Keep this " +
                 "high enough that neighbours overlap - lights spaced further apart than their " +
                 "range read as separate bulbs, which is the whole thing this is avoiding.")]
        [SerializeField]
        private int lightCount= 14;

        [Tooltip("Lumens per light. Set through HDRP's own converter, so this really is lumens - " +
                 "assigning Light.intensity directly writes a raw internal value instead and comes " +
                 "out roughly a hundred times too bright.")]
        [SerializeField]
        private float intensity= 250000f;

        [Tooltip("Colour of the emitted light. Keep it redder than the lava surface - bounced light reads warmer.")]
        [SerializeField]
        private Color color= new Color(1f, 0.35f, 0.12f, 1f);

        [Tooltip("How far each light reaches. Short enough not to cross the island, long enough to " +
                 "overlap its neighbours.")]
        [SerializeField]
        private float range= 100f;

        [Tooltip("Lift each light slightly off the surface so it does not z-fight or self-shadow.")]
        [SerializeField]
        private float surfaceOffset= 1.5f;

        [Tooltip("Shadows cost a shadow map per light per frame, and a point light costs six faces. " +
                 "Off is the right default at this light count.")]
        [SerializeField]
        private bool castShadows= false;

        [Tooltip("Emitter radius as a fraction of the band's footprint. Larger softens the falloff " +
                 "and stops each light reading as a point.")]
        [SerializeField]
        private float sizeMultiplier= 0.35f;

        [MenuItem("Survival Chaos/Place Lava Lights")]
        private static void Open()
        {
            DisplayWizard<LavaLightPlacer>("Place Lava Lights", "Place");
        }

        private void OnWizardCreate()
        {
            Material lava = AssetDatabase.LoadAssetAtPath<Material>(LavaMaterialPath);
            if (lava == null)
            {
                Debug.LogError($"Lava material not found at {LavaMaterialPath}");
                return;
            }

            List<Vector3> points = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            CollectLavaSurface(lava, points, normals);

            if (points.Count == 0)
            {
                Debug.LogError("No geometry found using the lava material. Is the scene with the island open?");
                return;
            }

            Transform container = PrepareContainer();
            int placed = PlaceLights(points, normals, container);

            Debug.Log($"Placed {placed} point light(s) along the lava from {points.Count} surface points. " +
                      (castShadows ? "Shadows are on - watch the frame time with the boss active." : "Shadows are off."));
        }

        /// <summary>
        /// Gathers world-space vertices and normals belonging only to the
        /// submeshes that use the lava material. A renderer's materials line up
        /// with submesh indices, so the material's slot index is the submesh.
        /// </summary>
        private static void CollectLavaSurface(Material lava, List<Vector3> points, List<Vector3> normals)
        {
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(
                         FindObjectsInactive.Exclude))
            {
                Material[] materials = renderer.sharedMaterials;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector3[] meshNormals = mesh.normals;

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    if (materials[slot] != lava || slot >= mesh.subMeshCount)
                    {
                        continue;
                    }

                    foreach (int index in mesh.GetTriangles(slot))
                    {
                        points.Add(renderer.transform.TransformPoint(vertices[index]));
                        normals.Add(meshNormals.Length > index
                            ? renderer.transform.TransformDirection(meshNormals[index]).normalized
                            : Vector3.up);
                    }
                }
            }
        }

        private static Transform PrepareContainer()
        {
            GameObject existing = GameObject.Find(ContainerName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Place Lava Lights");
            return container.transform;
        }

        private int PlaceLights(List<Vector3> points, List<Vector3> normals, Transform container)
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (Vector3 p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            int bands = Mathf.Max(1, lightCount);
            float bandHeight = Mathf.Max(0.001f, (maxY - minY) / bands);
            int placed = 0;

            for (int band = 0; band < bands; band++)
            {
                float lower = minY + band * bandHeight;
                float upper = lower + bandHeight;

                Vector3 centre = Vector3.zero;
                Vector3 normal = Vector3.zero;
                Vector3 min = Vector3.one * float.MaxValue;
                Vector3 max = Vector3.one * float.MinValue;
                int count = 0;

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 p = points[i];
                    bool inBand = band == bands - 1 ? p.y >= lower : p.y >= lower && p.y < upper;
                    if (!inBand)
                    {
                        continue;
                    }

                    centre += p;
                    normal += normals[i];
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    count++;
                }

                if (count == 0)
                {
                    continue;
                }

                centre /= count;
                normal = normal.sqrMagnitude < 0.0001f ? Vector3.up : normal.normalized;

                CreateLight(container, centre, normal, max - min, band);
                placed++;
            }

            return placed;
        }

        private void CreateLight(Transform container, Vector3 centre, Vector3 normal, Vector3 extents, int index)
        {
            GameObject go = new GameObject($"Lava Light {index + 1}");
            Undo.RegisterCreatedObjectUndo(go, "Place Lava Lights");
            go.transform.SetParent(container, false);

            // Sit just off the surface. A point light has no orientation, so
            // unlike the area lights this replaced there is nothing to aim.
            go.transform.position = centre + normal * surfaceOffset;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

            // HDRP needs its companion data component to render the light.
            HDAdditionalLightData data = go.GetComponent<HDAdditionalLightData>();
            if (data == null)
            {
                data = go.AddComponent<HDAdditionalLightData>();
            }

            // Go through HDRP's converter. Writing Light.intensity directly
            // skips the unit conversion and lands a raw value in the pipeline,
            // which is why this tool used to blow the scene out at its own
            // default.
            data.SetIntensity(intensity, UnityEngine.Rendering.LightUnit.Lumen);

            // A point light with a real emitter radius falls off over a shell
            // rather than from a singularity, so the near rock stops having a
            // hotspot burnt into it. Sized off the band so the wide pool gets a
            // wide emitter and the crater a tight one.
            // Clamped, because the band footprint is a bounding box and the
            // pool's is enormous - unclamped this hands the lowest lights an
            // emitter half as wide as their own range, which washes the near
            // rock out instead of softening it.
            float footprint = Mathf.Max(extents.x, extents.z);
            light.shapeRadius = Mathf.Clamp(footprint * sizeMultiplier, 2f, 15f);

            data.EnableShadows(castShadows);
            data.affectsVolumetric = true;

            // Physical. The old hand-placed set ran this at 7 to force a glow
            // out of fog that was too thin to show one, which is exactly what
            // made the volumetrics read as fake.
            data.volumetricDimmer = 1f;
        }
    }
}
