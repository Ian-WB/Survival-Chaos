using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the player's round: a double-ended energy needle, its material, and
    /// the swap onto the two player bullet prefabs.
    ///
    /// It replaces Unity's primitive capsule, which read as a cheap solid object.
    /// The needle is 0.48 long and 0.077 across in world units. It began at the
    /// spec's 0.32 by 0.048, which read as too small in play on 14 September
    /// 2026, and was scaled 1.5 along its length and 1.6 across. A round covers
    /// about 0.6 units a frame at 60 fps, so this still does not fill the gap
    /// between frames; what it buys is a sample that reads as a shape pointing
    /// along its flight. Six in a volley 0.2 apart still leave 0.12 between them.
    ///
    /// Both ends are identical on purpose. <c>shoot</c> and <c>shoot 1</c> fly in
    /// opposite directions along the same local X, and a round with a head and a
    /// tail would need a material per direction and the sign exactly right. A
    /// symmetric needle, and a shader that folds its length coordinate, need
    /// neither.
    ///
    /// Only the visual changes. The trigger box, the movement, and the root the
    /// light pool measures from are untouched, and the needle is centred on the
    /// root, so <see cref="ShootScript"/>'s drawn centre stays where it was.
    ///
    /// Prefab edits go through SerializedObject on the asset rather than
    /// PrefabUtility.LoadPrefabContents. Opening a prefab stage from a script
    /// once stalled the editor long enough for the GPU driver to reset it.
    /// Re-running updates the mesh and material in place, so their GUIDs survive.
    /// </summary>
    public static class PlayerRoundBuilder
    {
        private const string ShaderPath = "Assets/Art/Shaders/PlayerRound.shadergraph";
        private const string MaterialPath = "Assets/Art/Materials/VFX/PlayerRound.mat";
        private const string MeshFolder = "Assets/Art/Models/Projectiles";
        private const string MeshPath = MeshFolder + "/PlayerRoundNeedle.asset";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Projectiles/shoot.prefab",
            "Assets/Prefabs/Projectiles/shoot 1.prefab",
        };

        /// <summary>The child that draws the round, under the prefab root.</summary>
        private const string VisualChild = "projectile";

        // The needle's profile along its length, in world units: pointed at both
        // ends, widest at the centre.
        private static readonly float[] ProfileX = { -0.240f, -0.0825f, 0f, 0.0825f, 0.240f };
        private static readonly float[] ProfileRadius = { 0f, 0.0288f, 0.0384f, 0.0288f, 0f };

        /// <summary>Sides around each cross-section. Normals are smoothed around them.</summary>
        private const int Sides = 8;

        private static readonly Vector3 BodyColour = new Vector3(0.12f, 1.00f, 0.25f);
        private const float BodyIntensity = 2f;
        private static readonly Vector3 CoreColour = new Vector3(0.65f, 1.00f, 0.80f);
        private const float CoreIntensity = 6f;
        private const float PulseSpeed = 3f;
        private const float PulseDepth = 0.06f;

        // ---------- the trail ----------
        //
        // Added on 14 September 2026 because the needle alone read as a dot once a
        // volley flew away from the camera. A short streak carries the direction
        // of travel where the needle's own shape is lost to distance.

        private const string TrailMaterialPath = "Assets/Art/Materials/VFX/PlayerRoundTrail.mat";
        private const string TrailShaderPath = "Assets/Art/Shaders/PlayerRoundTrail.shadergraph";

        /// <summary>
        /// Seconds of path the trail keeps. A round covers about 36 units a second,
        /// so this is about 1.1 units, of which the rear 0.24 lies under the
        /// needle. Short on purpose: long enough to point, not a beam.
        /// </summary>
        private const float TrailSeconds = 0.03f;

        /// <summary>World width at the head, a little under the needle's 0.077; tapers to nothing.</summary>
        private const float TrailWidth = 0.06f;

        /// <summary>The needle's body green, a step dimmer so the needle stays the brightest part.</summary>
        private static readonly Vector3 TrailColour = new Vector3(0.12f, 1.00f, 0.25f);
        private const float TrailIntensity = 1.5f;

        [MenuItem("Survival Chaos/Build Player Round", priority = 49)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var log = new StringBuilder();

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                return "No shader at " + ShaderPath + ". Nothing was changed.";
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                return ShaderPath + " has compile errors. Nothing was changed.";
            }

            float rootScale = SharedRootScale(log);
            if (rootScale <= 0f)
            {
                return log.ToString();
            }

            Mesh needle = BuildMesh(1f / rootScale);
            Material material = BuildMaterial(shader);
            Material trailMaterial = BuildTrailMaterial();

            if (trailMaterial == null)
            {
                log.Append("No usable shader at " + TrailShaderPath + "; trails left as they were.\n");
            }

            foreach (string path in PrefabPaths)
            {
                ApplyToPrefab(path, needle, material, log);

                if (trailMaterial != null)
                {
                    ApplyTrail(path, trailMaterial, log);
                }
            }

            AssetDatabase.SaveAssets();

            Vector3 world = needle.bounds.size * rootScale;
            log.AppendFormat("Needle {0:F3} long x {1:F3} across in world units ({2} triangles).\n",
                world.x, world.y, needle.triangles.Length / 3);
            return log.ToString();
        }

        /// <summary>
        /// The mesh is built in the prefab root's local units, since the visual
        /// child sits at scale 1 under it. Both prefabs have to agree on that
        /// scale, or one of them would draw the needle at the wrong size.
        /// </summary>
        private static float SharedRootScale(StringBuilder log)
        {
            float scale = -1f;

            foreach (string path in PrefabPaths)
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    log.Append("Missing prefab " + path + ". Nothing was changed.\n");
                    return -1f;
                }

                float s = root.transform.localScale.x;
                if (scale > 0f && !Mathf.Approximately(scale, s))
                {
                    log.AppendFormat("Root scales disagree ({0} and {1}). Nothing was changed.\n", scale, s);
                    return -1f;
                }

                scale = s;
            }

            return scale;
        }

        private static Mesh BuildMesh(float unitsPerWorld)
        {
            int rings = ProfileX.Length;
            int columns = Sides + 1; // one duplicated column for the UV seam
            float start = ProfileX[0];
            float length = ProfileX[rings - 1] - start;

            var vertices = new Vector3[rings * columns];
            var normals = new Vector3[rings * columns];
            var uvs = new Vector2[rings * columns];

            for (int i = 0; i < rings; i++)
            {
                // Slope of the profile here, so the normals lean towards the tips
                // and the needle shades as a taper rather than a stack of cylinders.
                int before = Mathf.Max(i - 1, 0);
                int after = Mathf.Min(i + 1, rings - 1);
                float slope = (ProfileRadius[after] - ProfileRadius[before]) / (ProfileX[after] - ProfileX[before]);

                for (int j = 0; j < columns; j++)
                {
                    float angle = j / (float)Sides * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    int v = i * columns + j;

                    vertices[v] = new Vector3(ProfileX[i], cos * ProfileRadius[i], sin * ProfileRadius[i]) * unitsPerWorld;
                    normals[v] = new Vector3(-slope, cos, sin).normalized;
                    uvs[v] = new Vector2((ProfileX[i] - start) / length, j / (float)Sides);
                }
            }

            var triangles = new List<int>();

            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * columns + j;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    AddTriangle(triangles, vertices, normals, a, c, b);
                    AddTriangle(triangles, vertices, normals, b, c, d);
                }
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool isNew = mesh == null;

            if (isNew)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            mesh.name = "PlayerRoundNeedle";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            if (isNew)
            {
                EnsureFolder(MeshFolder);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        /// <summary>
        /// Adds a triangle facing outward. At the tips a ring collapses to a point,
        /// so half of each quad there has no area and is skipped rather than kept
        /// as a degenerate triangle.
        /// </summary>
        private static void AddTriangle(List<int> triangles, Vector3[] vertices, Vector3[] normals, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (face.sqrMagnitude < 1e-12f)
            {
                return;
            }

            Vector3 outward = normals[a] + normals[b] + normals[c];

            if (Vector3.Dot(face, outward) < 0f)
            {
                (b, c) = (c, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static Material BuildMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetVector("_BodyColor", BodyColour);
            material.SetFloat("_BodyIntensity", BodyIntensity);
            material.SetVector("_CoreColor", CoreColour);
            material.SetFloat("_CoreIntensity", CoreIntensity);
            material.SetFloat("_PulseSpeed", PulseSpeed);
            material.SetFloat("_PulseDepth", PulseDepth);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyToPrefab(string path, Mesh needle, Material material, StringBuilder log)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform visual = root.transform.Find(VisualChild);

            if (visual == null)
            {
                log.Append(path + ": no '" + VisualChild + "' child, skipped.\n");
                return;
            }

            // The capsule child was turned 90 degrees and scaled to 0.1 to lie
            // along X. The needle is built along X at the root's own scale, so the
            // child goes back to identity.
            var transform = new SerializedObject(visual);
            transform.FindProperty("m_LocalRotation").quaternionValue = Quaternion.identity;
            transform.FindProperty("m_LocalEulerAnglesHint").vector3Value = Vector3.zero;
            transform.FindProperty("m_LocalScale").vector3Value = Vector3.one;
            transform.FindProperty("m_LocalPosition").vector3Value = Vector3.zero;
            transform.ApplyModifiedPropertiesWithoutUndo();

            var filter = new SerializedObject(visual.GetComponent<MeshFilter>());
            filter.FindProperty("m_Mesh").objectReferenceValue = needle;
            filter.ApplyModifiedPropertiesWithoutUndo();

            var renderer = new SerializedObject(visual.GetComponent<MeshRenderer>());
            SerializedProperty materials = renderer.FindProperty("m_Materials");
            materials.arraySize = 1;
            materials.GetArrayElementAtIndex(0).objectReferenceValue = material;
            renderer.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssetIfDirty(root);
            log.Append(path + ": needle and PlayerRound material assigned.\n");
        }

        /// <summary>
        /// The trail's material, on PlayerRoundTrail.shadergraph: HDRP Unlit,
        /// transparent, additive, fading from head to tail and softening across the
        /// ribbon inside the shader.
        ///
        /// A shader graph because the trail has to be excluded from TAA and the
        /// temporal upscalers, and plain HDRP/Unlit cannot be. The first version was
        /// HDRP/Unlit with a fade texture, and it drifted off its needle whenever the
        /// camera moved up or down: TAA reprojected the trail's pixels by the motion
        /// of whatever lay behind them, and the island and the sky move very
        /// differently on screen from a streak a few units from the camera.
        ///
        /// The second version wrote motion vectors instead, with the renderer in
        /// Object mode, and it was worse - the round read as a dragonfly. A
        /// TrailRenderer's mesh is built in world space on a moving object, so its
        /// vectors came out wildly wrong and TAA and motion blur smeared it sideways.
        /// Excluding it from temporal reprojection sidesteps vectors altogether: no
        /// history to drift from, nothing to smear along. The cost is that the ribbon
        /// is not anti-aliased by TAA, which its soft alpha edges mostly hide.
        ///
        /// Returns null, leaving the trails alone, if the graph is missing or broken.
        /// </summary>
        private static Material BuildTrailMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(TrailShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, TrailMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetVector("_TrailColor", TrailColour);
            material.SetFloat("_TrailIntensity", TrailIntensity);

            // Stated here rather than left to the graph's defaults, because a
            // material that already exists keeps its own saved values over the
            // graph's. A view-aligned ribbon can face either way; the other two
            // are the drift fix described above.
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_TransparentWritingMotionVec", 0f);
            material.SetFloat("_ExcludeFromTUAndAA", 1f);

            // Brings the material's keywords and passes in line with the graph's
            // surface options, motion vectors included.
            HDMaterial.ValidateMaterial(material);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Puts the trail on the prefab root, which draws nothing itself: a
        /// GameObject carries one renderer, and the visual child already has the
        /// needle's. Added through the asset rather than a prefab stage, for the
        /// reason in this class's summary. ShootScript clears it on every spawn.
        /// </summary>
        private static void ApplyTrail(string path, Material trailMaterial, StringBuilder log)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            TrailRenderer trail = root.GetComponent<TrailRenderer>();
            bool added = trail == null;

            if (added)
            {
                trail = root.AddComponent<TrailRenderer>();
            }

            trail.time = TrailSeconds;
            trail.widthMultiplier = TrailWidth;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.minVertexDistance = 0.02f;
            trail.numCapVertices = 0;
            trail.numCornerVertices = 0;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.autodestruct = false;
            trail.emitting = true;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;
            trail.sharedMaterial = trailMaterial;

            // Camera, not Object. Object mode made HDRP reproject the trail's
            // world-space mesh through the moving round's matrices, which smeared it
            // into a dragonfly. The material is excluded from TAA and writes no
            // vectors, so nothing reads this beyond keeping it out of the way.
            trail.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;

            PrefabUtility.SavePrefabAsset(root);
            log.Append(path + ": trail " + (added ? "added" : "updated") + ".\n");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
