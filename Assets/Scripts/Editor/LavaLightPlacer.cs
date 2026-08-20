using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Places rectangle area lights along the lava.
    ///
    /// Emissive materials cannot cast shadows - shadows are rendered from a
    /// light's point of view, and an emissive surface is not a light. So the
    /// lava's glow on the surrounding rock has to come from real lights sitting
    /// on the emitting surface.
    ///
    /// Rather than have them positioned by hand, this reads the vertices of the
    /// submesh actually using the lava material, splits them into bands by
    /// height, and puts one light per band - centred on the band, facing along
    /// its average surface normal, and sized to its footprint. That follows the
    /// river from the crater down to the pool automatically.
    ///
    /// Re-running deletes and rebuilds the lights, so it is safe to iterate.
    /// </summary>
    public class LavaLightPlacer : ScriptableWizard
    {
        private const string ContainerName = "Lava Lights";
        private const string LavaMaterialPath = "Assets/Art/Materials/Scenario/lava_.mat";

        [Tooltip("How many lights to spread along the lava, from the crater downward.")]
        [SerializeField]
        private int lightCount= 4;

        [Tooltip("Lumens per light. HDRP's default area light is 200; lava wants considerably more.")]
        [SerializeField]
        private float intensity= 4000f;

        [Tooltip("Colour of the emitted light. Keep it redder than the lava surface - bounced light reads warmer.")]
        [SerializeField]
        private Color color= new Color(1f, 0.35f, 0.12f, 1f);

        [Tooltip("How far each light reaches. Too large and they overlap expensively.")]
        [SerializeField]
        private float range= 90f;

        [Tooltip("Lift each light slightly off the surface so it does not z-fight or self-shadow.")]
        [SerializeField]
        private float surfaceOffset= 1.5f;

        [Tooltip("Shadows cost a shadow map per light per frame. Turn off if the frame budget suffers.")]
        [SerializeField]
        private bool castShadows= true;

        [Tooltip("Scales the rectangle relative to the band it covers.")]
        [SerializeField]
        private float sizeMultiplier= 0.9f;

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

            Debug.Log($"Placed {placed} rectangle area light(s) along the lava from {points.Count} surface points. " +
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

            // Sit on the surface, emitting outward along it.
            go.transform.position = centre + normal * surfaceOffset;

            // LookRotation needs an up vector that is not parallel to forward.
            Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation(normal, reference);

            // The two widest horizontal dimensions of the band make a rectangle
            // that roughly covers the lava it stands in for.
            float width = Mathf.Max(1f, Mathf.Max(extents.x, extents.z) * sizeMultiplier);
            float height = Mathf.Max(1f, Mathf.Max(extents.y, Mathf.Min(extents.x, extents.z)) * sizeMultiplier);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Rectangle;
            light.areaSize = new Vector2(width, height);
            light.color = color;
            light.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            light.intensity = intensity;
            light.range = range;
            light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

            // HDRP needs its companion data component to render the light.
            HDAdditionalLightData data = go.GetComponent<HDAdditionalLightData>();
            if (data == null)
            {
                data = go.AddComponent<HDAdditionalLightData>();
            }

            data.EnableShadows(castShadows);
            data.affectsVolumetric = true;
        }
    }
}
