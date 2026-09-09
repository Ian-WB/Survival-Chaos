using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Splits the island mesh into its base and its volcano cone, so the two can
    /// carry different baked lighting densities.
    ///
    /// The problem this exists to solve is arithmetic rather than taste. Lightmap
    /// resolution is texels per *world* unit, which makes it a linear rate. All
    /// the figures below are at the world's current 1x scale; they were first
    /// measured while it was ten times this size, so anything quoted elsewhere
    /// from that period is ten times larger per unit and a hundred times larger
    /// in area.
    ///
    /// The island is 2,746 square units of surface, and at a 4096 lightmap it can
    /// be given at most 60.2 texels per unit however much the Lighting Settings
    /// ask for: 4096 x 4096 texels spread over that much area simply is that
    /// number. The setting sat exactly on it, so it was already pinned to the
    /// ceiling. That is why rebaking never helped, and why raising the sample
    /// counts eightfold never helped either - a bake starved of resolution is not
    /// a bake that is noisy, and samples only answer noise.
    ///
    /// What makes a split worth doing is that the area is not spent where the
    /// attention is. Measured per submesh:
    ///
    ///     ilha         12,772 tris   1,706.76 units^2   the base, its sides, its underside
    ///     pedra         6,872 tris     780.16 units^2   the surrounding rock
    ///     METADE CIMA   1,096 tris     209.72 units^2   the cone
    ///     ponta           506 tris      49.36 units^2   the summit
    ///
    /// The cone and the summit are 9.4% of the surface and very nearly all of what
    /// anyone looks at closely - the lava runs down it and the boss fight happens
    /// in front of it. The other 90.6% is largely underside and flank. So the two
    /// go onto separate renderers and each gets its own scaleInLightmap, which
    /// buys the cone about 100 texels per unit against the 60.2 it shared, and
    /// spends the base down to 30 to pay for it. Net atlas use barely moves:
    ///
    ///     cone     259.08 x 145^2 =  5.45M texels
    ///     base   2,486.92 x  46^2 =  5.26M texels
    ///     total                     10.71M against the 9.97M the island held
    ///
    /// Fresh lightmap UVs are generated for each half. That is not optional: the
    /// existing unwrap packs both halves into one [0,1] square, so a half carried
    /// over on its own would sit in a corner of its own atlas tile and waste the
    /// rest of it.
    ///
    /// The mesh is a standalone .asset rather than an FBX, so Unity's "Generate
    /// Lightmap UVs" import step never runs on it, and Unwrapping.GenerateSecondaryUVSet
    /// is the only route to a fresh unwrap. That is the reason this is a tool.
    ///
    /// Re-running is safe. It finds the child it made last time and updates it,
    /// and it never writes over the source mesh.
    ///
    /// **Re-run it after the scenario FBX is reimported.** scaleInLightmap is a
    /// prefab-overridable property and ilha principal 1 belongs to the model
    /// prefab instance, so a reimport reverts both halves to 1.0 - which does not
    /// fail, does not warn, and does not look wrong until you measure it. That
    /// happened during the rescale on 9 September 2026: two bakes ran with the
    /// cone and the base at the same density, 28.1 and 25.7 texels per unit
    /// against the 100 and 30 intended, and the only outward symptom was a
    /// smaller atlas.
    /// </summary>
    public static class SplitIslandForLightmapping
    {
        private const string SourceObject = "ilha principal 1";
        private const string ConeObject = "ilha cone";
        private const string SplitFolder = "Assets/Art/Models/Scenario/Split";

        /// <summary>
        /// The undivided island, read from the asset rather than from the scene.
        /// It has to come from here: after one run the scene object carries the
        /// base half, which has two submeshes rather than four, and a second run
        /// reading the scene would ask it for submesh 2 and throw. Reading the
        /// source every time is what actually makes re-running safe.
        /// </summary>
        private const string SourceMesh = SplitFolder + "/ilha principal 1 (no lava).asset";

        /// <summary>Submeshes that make up the cone. Everything else is the base.</summary>
        private static readonly int[] ConeSubmeshes = { 2, 3 };

        /// <summary>
        /// Requested densities in texels per world unit. These are absolute; the
        /// tool converts them into scaleInLightmap against whatever the Lighting
        /// Settings currently ask for, so changing the global resolution later
        /// cannot silently change the split.
        ///
        /// They are *requests*, and the distinction is the whole reason the old
        /// numbers were misleading. A renderer consumes area x requested^2 texels
        /// of atlas, but only the fraction of its tile that its UV charts actually
        /// cover carries lighting - so what lands on the surface is
        ///
        ///     achieved = requested x sqrt(chart utilisation)
        ///
        /// That model is not a guess. The old bake requested 60, packed at 35.7%,
        /// and measured 36 off its lightmapScaleOffset: 60 x sqrt(0.357) = 35.8.
        ///
        /// The pair below is chosen against the atlas rather than against taste.
        /// A 4096 lightmap holds 16.78M texels and the island held 59.4% of it;
        /// these two tiles come to 10.7M. What that costs the rest of the scene is
        /// less than it sounds - measured, the island is 91.8% of every lightmapped
        /// surface here, the fourteen ruins are 5.7% and all seventy-three trees
        /// together are 2.5%. At the measured utilisations - 47.8% on the cone,
        /// 42.5% on the base - the pair lands near 100 and 30 texels per unit
        /// against the 36 both halves shared before the split.
        ///
        /// Asking for more is not free and not silent-free: request past what the
        /// atlas holds and Unity scales every renderer down to fit, which is
        /// exactly the failure this tool exists to undo.
        /// </summary>
        private const float ConeTexelsPerUnit = 145f;
        private const float BaseTexelsPerUnit = 46f;

        [MenuItem("Survival Chaos/Split Island For Lightmapping")]
        public static void Run()
        {
            GameObject source = GameObject.Find(SourceObject);
            if (source == null)
            {
                Debug.LogError("Split Island: no GameObject named " + SourceObject + " in the open scene.");
                return;
            }

            MeshFilter filter = source.GetComponent<MeshFilter>();
            Renderer renderer = source.GetComponent<Renderer>();
            if (filter == null || renderer == null)
            {
                Debug.LogError("Split Island: " + SourceObject + " has no mesh filter or no renderer.");
                return;
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SourceMesh);
            if (mesh == null)
            {
                Debug.LogError("Split Island: no source mesh at " + SourceMesh);
                return;
            }

            var coneSet = new HashSet<int>(ConeSubmeshes);
            var baseSubmeshes = new List<int>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (!coneSet.Contains(i)) { baseSubmeshes.Add(i); }
            }

            Material[] materials = GatherMaterials(source, renderer, mesh.subMeshCount, baseSubmeshes);
            if (materials == null) { return; }

            // Both halves are built before anything is written, so a failure
            // leaves the scene as it was rather than half converted.
            Mesh coneMesh = BuildSubset(mesh, ConeSubmeshes, mesh.name + " (cone)");
            Mesh baseMesh = BuildSubset(mesh, baseSubmeshes.ToArray(), mesh.name + " (base)");
            if (coneMesh == null || baseMesh == null) { return; }

            Unwrap(coneMesh);
            Unwrap(baseMesh);

            string conePath = SplitFolder + "/" + coneMesh.name + ".asset";
            string basePath = SplitFolder + "/" + baseMesh.name + ".asset";
            WriteMesh(coneMesh, conePath);
            WriteMesh(baseMesh, basePath);

            coneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(conePath);
            baseMesh = AssetDatabase.LoadAssetAtPath<Mesh>(basePath);

            // The base keeps the original object; the cone becomes a child of it,
            // so it inherits the transform rather than repeating it.
            Undo.RecordObject(filter, "Split island for lightmapping");
            Undo.RecordObject(renderer, "Split island for lightmapping");
            filter.sharedMesh = baseMesh;
            renderer.sharedMaterials = Pick(materials, baseSubmeshes.ToArray());

            Transform existing = source.transform.Find(ConeObject);
            GameObject cone = existing != null ? existing.gameObject : null;
            if (cone == null)
            {
                cone = new GameObject(ConeObject);
                Undo.RegisterCreatedObjectUndo(cone, "Split island for lightmapping");
                Undo.SetTransformParent(cone.transform, source.transform, "Split island for lightmapping");
            }
            else
            {
                Undo.RecordObject(cone, "Split island for lightmapping");
            }

            cone.transform.localPosition = Vector3.zero;
            cone.transform.localRotation = Quaternion.identity;
            cone.transform.localScale = Vector3.one;
            cone.layer = source.layer;
            GameObjectUtility.SetStaticEditorFlags(cone, GameObjectUtility.GetStaticEditorFlags(source));

            MeshFilter coneFilter = cone.GetComponent<MeshFilter>();
            if (coneFilter == null) { coneFilter = Undo.AddComponent<MeshFilter>(cone); }
            MeshRenderer coneRenderer = cone.GetComponent<MeshRenderer>();
            if (coneRenderer == null) { coneRenderer = Undo.AddComponent<MeshRenderer>(cone); }

            coneFilter.sharedMesh = coneMesh;
            coneRenderer.sharedMaterials = Pick(materials, ConeSubmeshes);
            coneRenderer.shadowCastingMode = renderer.shadowCastingMode;
            coneRenderer.receiveShadows = renderer.receiveShadows;
            coneRenderer.lightProbeUsage = renderer.lightProbeUsage;
            coneRenderer.reflectionProbeUsage = renderer.reflectionProbeUsage;
            coneRenderer.motionVectorGenerationMode = renderer.motionVectorGenerationMode;

            float resolution = LightmapResolution();
            float coneScale = ConeTexelsPerUnit / resolution;
            float baseScale = BaseTexelsPerUnit / resolution;
            SetScaleInLightmap(coneRenderer, coneScale);
            SetScaleInLightmap(renderer, baseScale);

            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(cone);

            Debug.Log("Split Island, at Lighting Settings resolution " + resolution + ":\n"
                + Describe("cone", coneMesh, ConeTexelsPerUnit, coneScale)
                + Describe("base", baseMesh, BaseTexelsPerUnit, baseScale)
                + "  atlas: " + ((Tile(coneMesh, ConeTexelsPerUnit) + Tile(baseMesh, BaseTexelsPerUnit)) / 1e6)
                    .ToString("0.00") + "M texels of the " + (4096 * 4096 / 1e6).ToString("0.0")
                + "M a 4096 lightmap holds.\n"
                + "  Save the scene, then rebake.");
        }

        /// <summary>
        /// Rebuilds the source mesh's full material list, in submesh order.
        ///
        /// On a first run the scene renderer still carries all four, so it is just
        /// read. After a split it carries two, and the other two live on the cone
        /// child - so they are put back in their original slots. Without this the
        /// second run would assign the base its own materials shifted by two.
        /// </summary>
        private static Material[] GatherMaterials(
            GameObject source, Renderer renderer, int submeshCount, List<int> baseSubmeshes)
        {
            Material[] onSource = renderer.sharedMaterials;
            if (onSource.Length == submeshCount) { return onSource; }

            Transform cone = source.transform.Find(ConeObject);
            Renderer coneRenderer = cone != null ? cone.GetComponent<Renderer>() : null;
            Material[] onCone = coneRenderer != null ? coneRenderer.sharedMaterials : new Material[0];

            if (onSource.Length != baseSubmeshes.Count || onCone.Length != ConeSubmeshes.Length)
            {
                Debug.LogError("Split Island: cannot rebuild the material list - the source has "
                    + onSource.Length + " and the cone has " + onCone.Length + ", against "
                    + submeshCount + " submeshes. Reassign them by hand, or undo the last split.");
                return null;
            }

            var all = new Material[submeshCount];
            for (int i = 0; i < baseSubmeshes.Count; i++) { all[baseSubmeshes[i]] = onSource[i]; }
            for (int i = 0; i < ConeSubmeshes.Length; i++) { all[ConeSubmeshes[i]] = onCone[i]; }
            return all;
        }

        /// <summary>
        /// Copies the named submeshes into a new mesh, keeping only the vertices
        /// they actually reference. Dropping the rest matters: an unwrapper handed
        /// 40,050 vertices to lay out 1,602 triangles packs the result as though
        /// the whole island were still in front of it.
        /// </summary>
        private static Mesh BuildSubset(Mesh src, int[] submeshes, string name)
        {
            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNormals = src.normals;
            Vector4[] srcTangents = src.tangents;
            Vector2[] srcUv = src.uv;
            Color[] srcColors = src.colors;

            var map = new Dictionary<int, int>();
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangleSets = new List<int[]>();

            foreach (int s in submeshes)
            {
                int[] tris = src.GetTriangles(s);
                var remapped = new int[tris.Length];
                for (int i = 0; i < tris.Length; i++)
                {
                    int old = tris[i];
                    int fresh;
                    if (!map.TryGetValue(old, out fresh))
                    {
                        fresh = verts.Count;
                        map[old] = fresh;
                        verts.Add(srcVerts[old]);
                        if (srcNormals.Length > 0) { normals.Add(srcNormals[old]); }
                        if (srcTangents.Length > 0) { tangents.Add(srcTangents[old]); }
                        if (srcUv.Length > 0) { uvs.Add(srcUv[old]); }
                        if (srcColors.Length > 0) { colors.Add(srcColors[old]); }
                    }
                    remapped[i] = fresh;
                }
                triangleSets.Add(remapped);
            }

            if (verts.Count == 0)
            {
                Debug.LogError("Split Island: subset " + name + " came out empty.");
                return null;
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            if (normals.Count == verts.Count) { mesh.SetNormals(normals); }
            if (tangents.Count == verts.Count) { mesh.SetTangents(tangents); }
            if (uvs.Count == verts.Count) { mesh.SetUVs(0, uvs); }
            if (colors.Count == verts.Count) { mesh.SetColors(colors); }

            mesh.subMeshCount = triangleSets.Count;
            for (int i = 0; i < triangleSets.Count; i++)
            {
                mesh.SetTriangles(triangleSets[i], i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A fresh unwrap, because each half now owns a whole atlas tile rather
        /// than a corner of a shared one. Only the pack margin moves off its
        /// default: it is expressed in normalised UV, and the default 4/1024 is a
        /// wide gutter once a chart is rasterised at a couple of thousand texels.
        /// </summary>
        private static void Unwrap(Mesh mesh)
        {
            UnwrapParam param;
            UnwrapParam.SetDefaults(out param);
            param.packMargin = 0.002f;
            Unwrapping.GenerateSecondaryUVSet(mesh, param);
        }

        private static void WriteMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Keep the asset identity, so every reference to it survives a re-run.
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material[] Pick(Material[] all, int[] indices)
        {
            var picked = new Material[indices.Length];
            for (int i = 0; i < indices.Length; i++) { picked[i] = all[indices[i]]; }
            return picked;
        }

        private static void SetScaleInLightmap(Renderer renderer, float scale)
        {
            var so = new SerializedObject(renderer);
            SerializedProperty p = so.FindProperty("m_ScaleInLightmap");
            if (p == null)
            {
                Debug.LogWarning("Split Island: no m_ScaleInLightmap on " + renderer.name);
                return;
            }
            p.floatValue = scale;
            so.ApplyModifiedProperties();
        }

        private static float LightmapResolution()
        {
            LightingSettings settings = Lightmapping.lightingSettings;
            return settings != null ? settings.lightmapResolution : 6f;
        }

        /// <summary>
        /// One line per half, reporting what was asked for beside what the unwrap
        /// will actually deliver. Reporting only the request would repeat the
        /// mistake that hid this problem for so long.
        /// </summary>
        private static string Describe(string label, Mesh mesh, float requested, float scale)
        {
            double area = Area(mesh);
            double utilisation = Utilisation(mesh);
            double achieved = requested * System.Math.Sqrt(utilisation);
            return "  " + label + ": " + area.ToString("0") + " units^2, "
                + mesh.triangles.Length / 3 + " tris, charts cover "
                + (utilisation * 100).ToString("0.0") + "% of the tile -> asked "
                + requested.ToString("0.0") + ", expect ~" + achieved.ToString("0.0")
                + " texels/unit (scaleInLightmap " + scale.ToString("0.000") + ", tile "
                + (Tile(mesh, requested) / 1e6).ToString("0.00") + "M texels)\n";
        }

        /// <summary>Fraction of the [0,1] lightmap tile the UV2 charts actually cover.</summary>
        private static double Utilisation(Mesh mesh)
        {
            Vector2[] uv = mesh.uv2;
            int[] t = mesh.triangles;
            if (uv == null || uv.Length == 0) { return 1.0; }
            double total = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector2 a = uv[t[i + 1]] - uv[t[i]];
                Vector2 b = uv[t[i + 2]] - uv[t[i]];
                total += Mathf.Abs(a.x * b.y - a.y * b.x) * 0.5;
            }
            return total;
        }

        /// <summary>Atlas texels this renderer will claim: area x requested^2.</summary>
        private static double Tile(Mesh mesh, float requested)
        {
            return Area(mesh) * requested * requested;
        }

        private static double Area(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            double total = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                total += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5;
            }
            return total;
        }
    }
}
