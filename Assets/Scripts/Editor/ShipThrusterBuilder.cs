using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the ship's thruster flares: two meshes, one material, and the
    /// <see cref="ShipThrusters"/> component on the player's ship model, with its
    /// nozzle positions measured off that model rather than typed in.
    ///
    /// The meshes are unit templates - a plume one long and one wide from its
    /// nozzle, a glow one across - and the component scales them to the lengths
    /// and widths it carries. So tuning how big a flare is happens in the
    /// Inspector and never brings this file back; only the shape of a flare does.
    ///
    /// The material is the round's streak material's twin, for the reason given
    /// at length in <see cref="PlayerRoundBuilder"/>: these are transparent
    /// additive surfaces on a moving object, the game's settings run FSR, and
    /// anything that does not write its own motion vectors gets dragged across
    /// the screen by the island behind it.
    ///
    /// Like PlayerRoundBuilder, the prefab is edited through SerializedObject on
    /// the asset rather than through PrefabUtility.LoadPrefabContents - opening a
    /// prefab stage from a script once stalled the editor long enough for the GPU
    /// driver to reset it. Re-running updates the meshes, material and component
    /// in place, so their GUIDs survive.
    /// </summary>
    public static class ShipThrusterBuilder
    {
        private const string ShaderPath = "Assets/Art/Shaders/ShipThruster.shadergraph";
        private const string MaterialPath = "Assets/Art/Materials/VFX/ShipThruster.mat";
        private const string MeshFolder = "Assets/Art/Models/VFX";
        private const string PlumePath = MeshFolder + "/ShipThrusterPlume.asset";
        private const string GlowPath = MeshFolder + "/ShipThrusterGlow.asset";
        private const string ShipPath = "Assets/Prefabs/Player/PlayerSpaceShip.prefab";

        /// <summary>
        /// The player's own green, the same value the round's needle and streak
        /// carry. Hostile fire is warm on one ladder now, so the ship's exhaust
        /// being green is what keeps it from ever being read as something shot at
        /// you. The shader pushes the core of each flare toward white on its own,
        /// so this is the one hue the whole rig is authored in.
        /// </summary>
        private static readonly Vector3 FlameColour = new Vector3(0.12f, 1.00f, 0.25f);

        /// <summary>The plume's width at its tip, as a fraction of its nozzle.</summary>
        private const float PlumeTipFraction = 0.18f;

        /// <summary>Sides on each of the glow's three fans.</summary>
        private const int GlowSides = 8;

        // ---------- where the nozzles are ----------
        //
        // Named parts of the ship model, measured rather than guessed: the rocket
        // body at the back and the two blocks under the nose. By object name and
        // not by mesh name, because the two blocks are one mesh used twice and a
        // mesh name cannot tell them apart.
        private const string RocketObject = "fogete";
        private static readonly string[] RetroObjects = { "Cubo", "Cubo.001" };

        /// <summary>How far above the hull's top the ready light floats.</summary>
        private const float ReadyClearance = 0.008f;

        [MenuItem("Survival Chaos/Build Ship Thrusters", priority = 50)]
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

            GameObject ship = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath);
            if (ship == null)
            {
                return "No ship model at " + ShipPath + ". Nothing was changed.";
            }

            Mesh plume = BuildPlume();
            Mesh glow = BuildGlow();
            Material material = BuildMaterial(shader);

            if (!Mount(ship, plume, glow, material, log))
            {
                return log.ToString();
            }

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>
        /// A unit plume: two tapered quads crossed along +X, from a nozzle one
        /// unit across at the origin to a tip at x = 1.
        ///
        /// Crossed rather than single for the reason the round's streak is: the
        /// camera looks at the ship roughly down one axis, where the first quad
        /// faces it, and the second is what keeps the flare from vanishing
        /// edge-on as the ship travels round the ring away from that view.
        ///
        /// U runs 0 at the nozzle to 1 at the tip and V across, which is what the
        /// shader fades and softens along.
        /// </summary>
        private static Mesh BuildPlume()
        {
            float tip = PlumeTipFraction * 0.5f;

            var vertices = new[]
            {
                new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(1f, -tip, 0f), new Vector3(1f, tip, 0f),
                new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0f, -tip), new Vector3(1f, 0f, tip),
            };

            var uvs = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f),
            };

            int[] triangles = { 0, 1, 2, 1, 3, 2, 4, 5, 6, 5, 7, 6 };

            return WriteMesh(PlumePath, vertices, uvs, triangles, new Bounds(new Vector3(0.5f, 0f, 0f), Vector3.one));
        }

        /// <summary>
        /// A unit glow: three fans, one in each plane, a unit across.
        ///
        /// U runs 0 at each fan's centre to 1 at its rim, so the same shader that
        /// fades a plume from nozzle to tip fades this from middle to edge.
        ///
        /// V runs the same way, which is what keeps the light a light. Held at
        /// the middle of the ribbon it put the whole glow on the shader's core
        /// colour, and seen in play on 18 September 2026 that was a hard-edged
        /// white ball floating over the hull - a star, or a pickup, rather than a
        /// lamp on the ship. It is the boss pods' problem exactly: brightness
        /// blowing a colour out to white in the tonemapper. Running V outward
        /// instead leaves a hot centre that falls to the player's green at the
        /// rim, and fades twice on the way.
        ///
        /// Three planes rather than a billboard because it has to read from
        /// wherever the camera happens to be, and the ship turns end for end
        /// underneath it.
        /// </summary>
        private static Mesh BuildGlow()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int plane = 0; plane < 3; plane++)
            {
                int centre = vertices.Count;
                vertices.Add(Vector3.zero);
                uvs.Add(new Vector2(0f, 0.5f));

                for (int i = 0; i < GlowSides; i++)
                {
                    float angle = i / (float)GlowSides * Mathf.PI * 2f;
                    float a = Mathf.Cos(angle) * 0.5f;
                    float b = Mathf.Sin(angle) * 0.5f;

                    vertices.Add(plane == 0 ? new Vector3(a, b, 0f)
                        : plane == 1 ? new Vector3(a, 0f, b)
                        : new Vector3(0f, a, b));

                    uvs.Add(new Vector2(1f, 0f));
                }

                for (int i = 0; i < GlowSides; i++)
                {
                    triangles.Add(centre);
                    triangles.Add(centre + 1 + i);
                    triangles.Add(centre + 1 + (i + 1) % GlowSides);
                }
            }

            return WriteMesh(GlowPath, vertices.ToArray(), uvs.ToArray(), triangles.ToArray(),
                new Bounds(Vector3.zero, Vector3.one));
        }

        private static Mesh WriteMesh(string path, Vector3[] vertices, Vector2[] uvs, int[] triangles, Bounds bounds)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;

            if (isNew)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0, calculateBounds: false);
            mesh.RecalculateNormals();
            mesh.bounds = bounds;

            if (isNew)
            {
                EnsureFolder(MeshFolder);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        private static Material BuildMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetVector("_FlameColor", FlameColour);

            // Only what an unwired flare would draw: ShipThrusters writes this
            // per renderer every frame through a property block.
            material.SetFloat("_FlameIntensity", 1f);

            // Stated here rather than left to the graph's defaults, because a
            // material that already exists keeps its own saved values over the
            // graph's. The crossed quads face either way, and the flares write
            // motion vectors and take part in TAA and FSR like the rounds do.
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_TransparentWritingMotionVec", 1f);
            material.SetFloat("_ExcludeFromTUAndAA", 0f);

            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Puts the component on the ship model and writes it the assets and the
        /// three sets of offsets, each measured off a named mesh in the model's
        /// own space.
        /// </summary>
        private static bool Mount(GameObject ship, Mesh plume, Mesh glow, Material material, StringBuilder log)
        {
            if (!TryBounds(ship, RocketObject, out Bounds rocket, log))
            {
                return false;
            }

            var retros = new List<Vector3>();
            foreach (string name in RetroObjects)
            {
                if (!TryBounds(ship, name, out Bounds block, log))
                {
                    return false;
                }

                // The front face of the block, which is the way a retro fires.
                retros.Add(new Vector3(block.max.x, block.center.y, block.center.z));
            }

            // The rocket's back face, on its own centre line.
            var mainOffset = new Vector3(rocket.min.x, rocket.center.y, 0f);

            // Clear of the top of the whole model rather than of a named part of
            // it, so the light cannot end up inside something the ship grows.
            Bounds ship3d = WholeShip(ship);
            var readyOffset = new Vector3(ship3d.center.x, ship3d.max.y + ReadyClearance, 0f);

            ShipThrusters thrusters = ship.GetComponent<ShipThrusters>();
            bool added = thrusters == null;

            if (added)
            {
                thrusters = ship.AddComponent<ShipThrusters>();
            }

            var so = new SerializedObject(thrusters);
            so.FindProperty("plumeMesh").objectReferenceValue = plume;
            so.FindProperty("glowMesh").objectReferenceValue = glow;
            so.FindProperty("flareMaterial").objectReferenceValue = material;
            so.FindProperty("mainOffset").vector3Value = mainOffset;
            so.FindProperty("readyOffset").vector3Value = readyOffset;

            SerializedProperty offsets = so.FindProperty("retroOffsets");
            offsets.arraySize = retros.Count;
            for (int i = 0; i < retros.Count; i++)
            {
                offsets.GetArrayElementAtIndex(i).vector3Value = retros[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(ship);

            log.AppendFormat("{0}: ShipThrusters {1}.\n", ShipPath, added ? "added" : "updated");
            log.AppendFormat("  main   {0} (back of '{1}')\n", Format(mainOffset), RocketObject);
            for (int i = 0; i < retros.Count; i++)
            {
                log.AppendFormat("  retro  {0} (front of '{1}')\n", Format(retros[i]), RetroObjects[i]);
            }

            log.AppendFormat("  ready  {0} ({1} clear of the top)\n", Format(readyOffset), ReadyClearance);
            log.AppendFormat("  the model spans x {0:F4} to {1:F4}, so it is {2:F4} long and {3:F4} tall.\n",
                ship3d.min.x, ship3d.max.x, ship3d.size.x, ship3d.size.y);
            return true;
        }

        /// <summary>
        /// One named part's bounds in the model root's own space, which is the
        /// space the component's offsets are in. The child transforms carry
        /// rotations, so this is the mesh's own bounds moved through them rather
        /// than the child's local position.
        /// </summary>
        private static bool TryBounds(GameObject root, string objectName, out Bounds bounds, StringBuilder log)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && filter.gameObject.name == objectName)
                {
                    bounds = InRootSpace(root, filter);
                    return true;
                }
            }

            bounds = default;
            log.AppendFormat(
                "Nothing named '{0}' under {1}, so the nozzles cannot be placed. Nothing was changed.\n",
                objectName, ShipPath);
            return false;
        }

        /// <summary>Every mesh on the model, as one box in the root's own space.</summary>
        private static Bounds WholeShip(GameObject root)
        {
            Bounds all = default;
            bool any = false;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds part = InRootSpace(root, filter);

                if (any)
                {
                    all.Encapsulate(part);
                }
                else
                {
                    all = part;
                    any = true;
                }
            }

            return all;
        }

        private static Bounds InRootSpace(GameObject root, MeshFilter filter)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds local = filter.sharedMesh.bounds;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);

                Vector3 point = toRoot.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static string Format(Vector3 v)
        {
            return string.Format("({0,8:F4},{1,8:F4},{2,8:F4})", v.x, v.y, v.z);
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
