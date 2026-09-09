using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the pickup prefab and the material it glows with.
    ///
    /// Done in code for the same reason the quality tiers are: the parts that
    /// matter here are the ones that look like nothing in the Inspector. A
    /// hand-built pickup will look right in the scene view and still be wrong in
    /// two ways that only show up as a frame-time cost and a colour that never
    /// changes.
    ///
    /// Re-running replaces the prefab's contents in place, so the GUID survives
    /// and anything already referencing it keeps working.
    /// </summary>
    public static class PickupPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/Pickups";
        private const string PrefabPath = Folder + "/Pickup.prefab";
        private const string MaterialFolder = "Assets/Art/Materials/VFX";
        private const string MaterialPath = MaterialFolder + "/PickupGlow.mat";

        /// <summary>
        /// Radius of the trigger, in world units, against an orbit radius of
        /// 13.72. Deliberately larger than the visible object - the player flies
        /// past at speed, and a collect volume that matches the art exactly reads
        /// as the pickup failing to work rather than as a near miss.
        /// </summary>
        private const float TriggerRadius = 0.9f;

        /// <summary>Edge length of the visible core. A bullet is roughly a third of this.</summary>
        private const float CoreScale = 0.45f;

        /// <summary>
        /// Where the interface typeface lives. Searched rather than referenced by
        /// path so renaming the font asset does not silently produce unstyled
        /// text - the same arrangement HoloUiFactory uses for the menus.
        /// </summary>
        private const string FontFolder = "Assets/UI/Fonts";

        /// <summary>
        /// How far above the core the label sits, in world units. The core is a
        /// 0.45 cube stood on a corner, so its silhouette reaches about 0.39 out
        /// from centre - this clears it without floating free of it.
        /// </summary>
        private const float LabelRise = 0.75f;

        /// <summary>Where the caption board goes: a child of the overlay canvas.</summary>
        private const string BoardName = "Pickup Labels (Holo)";

        [MenuItem("Survival Chaos/Build Pickup Prefab", priority = 48)]
        public static void Build()
        {
            Material glow = BuildMaterial();
            if (glow == null)
            {
                return;
            }

            EnsureFolder("Assets/Prefabs", "Pickups");

            GameObject root = new GameObject("Pickup");

            try
            {
                Compose(root, glow);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"Built {PrefabPath}. Assign it to PickupSpawner's Pickup Prefab field. " +
                    "The visible core is a child, so swapping the mesh for something more " +
                    "interesting only means replacing that one MeshFilter.",
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Compose(GameObject root, Material glow)
        {
            // A trigger, so flying into a pickup collects it rather than shoving
            // the ship off its orbit.
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = TriggerRadius;

            // The part that is invisible in the Inspector and expensive without.
            //
            // A collider with no Rigidbody anywhere in its parent chain is static
            // geometry as far as PhysX is concerned, and static geometry is not
            // expected to move. The pickup spins and bobs every frame, so every
            // frame PhysX would rebuild the part of its static structure that
            // this collider sits in. Kinematic says "this moves, but nothing
            // pushes it" - which is exactly true, and costs nothing.
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "Core";

            // CreatePrimitive ships a collider with the mesh. Left on, it would
            // be a second collider on the same object - a solid one, inside the
            // trigger, that the ship would collide with.
            Object.DestroyImmediate(core.GetComponent<Collider>());

            core.transform.SetParent(root.transform, worldPositionStays: false);
            core.transform.localScale = Vector3.one * CoreScale;

            // Presented corner-on, so the silhouette reads as a faceted crystal
            // and the spin is legible. A sphere would rotate invisibly.
            core.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);

            MeshRenderer renderer = core.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;

            // Nothing else in the arena should be lit by a pickup, and shadow
            // casting on an emissive object of this size buys nothing.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Pickup pickup = root.AddComponent<Pickup>();
            AssignReference(pickup, "tintTarget", renderer);
            AssignReference(pickup, "label", BuildLabel(root));
        }

        /// <summary>
        /// The point the caption is drawn above, and the component that carries
        /// what it says.
        ///
        /// No text of its own. The caption is drawn by PickupLabelBoard on the
        /// screen-space canvas, because a world-space TextMeshPro writes no
        /// motion vectors and temporal antialiasing smeared it; this object only
        /// has to say where above the pickup that caption belongs.
        ///
        /// A child of the root rather than of the core so it does not inherit the
        /// core's 45-degree presentation rotation, and so the spin does not move
        /// it - the root spins about its own axis, which leaves a point on that
        /// axis exactly where it was.
        /// </summary>
        private static PickupLabel BuildLabel(GameObject root)
        {
            GameObject holder = new GameObject("Label");
            holder.transform.SetParent(root.transform, worldPositionStays: false);
            holder.transform.localPosition = new Vector3(0f, LabelRise, 0f);

            return holder.AddComponent<PickupLabel>();
        }

        /// <summary>
        /// Puts the caption board on the scene's overlay canvas, or brings an
        /// existing one back up to date.
        ///
        /// Separate from the prefab build because it writes to the open scene
        /// rather than to an asset, and running it is not something the prefab
        /// build should do behind your back.
        ///
        /// It has to be the Screen Space - Overlay canvas specifically. That is
        /// the whole point of the board: overlay UI is composited after HDRP has
        /// finished, so temporal antialiasing never sees it. A world-space or
        /// Screen Space - Camera canvas is drawn inside the frame and would be
        /// smeared exactly as the world-space labels were.
        /// </summary>
        [MenuItem("Survival Chaos/Build Pickup Label Board", priority = 49)]
        public static void BuildBoard()
        {
            Canvas canvas = null;

            // No sort mode: every overload taking one was deprecated in 6000.5.9
            // for relying on instance-id ordering. Nothing here depended on the
            // order anyway - the loop is looking for one specific canvas.
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (candidate.isRootCanvas && candidate.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = candidate;
                    break;
                }
            }

            if (canvas == null)
            {
                Debug.LogError(
                    "No Screen Space - Overlay canvas in the open scene, so there is nowhere " +
                    "to put the caption board. The captions are drawn there precisely because " +
                    "overlay UI escapes post-processing.");
                return;
            }

            Transform existing = canvas.transform.Find(BoardName);
            GameObject board;

            if (existing != null)
            {
                board = existing.gameObject;
            }
            else
            {
                board = new GameObject(BoardName, typeof(RectTransform));
                board.transform.SetParent(canvas.transform, worldPositionStays: false);
                Undo.RegisterCreatedObjectUndo(board, "Build Pickup Label Board");
            }

            // Stretched to the full canvas. The captions are positioned in screen
            // pixels, so the board only has to be a container that does not clip
            // them - an inset rect would cut off any caption near an edge.
            RectTransform rect = board.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (!board.TryGetComponent(out PickupLabelBoard component))
            {
                component = board.AddComponent<PickupLabelBoard>();
            }

            AssignReference(component, "font", InterfaceFont());

            EditorUtility.SetDirty(board);
            Selection.activeObject = board;

            Debug.Log(
                $"'{BoardName}' is on '{canvas.name}'. Save the scene to keep it. Caption size, " +
                "distance falloff and glow strength are on the component.", board);
        }

        /// <summary>
        /// The interface typeface, or null to leave TextMeshPro's default.
        ///
        /// Found by searching the font folder rather than by a hard path, so
        /// replacing the font is a matter of dropping the new asset in. Null is a
        /// legitimate answer: unstyled text is worse than styled text but far
        /// better than a builder that refuses to run.
        /// </summary>
        private static TMP_FontAsset InterfaceFont()
        {
            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                Debug.LogWarning($"No '{FontFolder}' folder, so pickup labels will use " +
                                 "TextMeshPro's default font.");
                return null;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontFolder }))
            {
                TMP_FontAsset font =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

                if (font != null)
                {
                    return font;
                }
            }

            Debug.LogWarning($"No TMP font asset in '{FontFolder}', so pickup labels will use " +
                             "TextMeshPro's default font.");
            return null;
        }

        /// <summary>
        /// Fills in a private serialized reference, which has to go through
        /// SerializedObject because the field is not public.
        ///
        /// Every field wired this way has a runtime fallback that finds the same
        /// object with a GetComponentInChildren, so none of this is load-bearing
        /// - but the fallback runs on the first spawn of every pickup, and being
        /// explicit costs nothing. A missing field warns and moves on rather than
        /// aborting the build, since a renamed field should cost you a wire-up,
        /// not the whole prefab.
        /// </summary>
        private static void AssignReference(Object owner, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty target = serialized.FindProperty(field);

            if (target == null)
            {
                Debug.LogWarning(
                    $"{owner.GetType().Name} has no '{field}' field; it will look the reference " +
                    "up at runtime instead.");
                return;
            }

            target.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The shared glow material.
        ///
        /// Every pickup uses this one asset and recolours itself through a
        /// MaterialPropertyBlock, so the colour here is only what an unconfigured
        /// pickup would show. Emission has to be switched on and non-black at
        /// authoring time regardless - HDRP compiles the emissive path out of the
        /// shader variant otherwise, and a property block cannot put it back.
        /// </summary>
        private static Material BuildMaterial()
        {
            Shader unlit = Shader.Find("HDRP/Unlit");
            if (unlit == null)
            {
                Debug.LogError("HDRP/Unlit not found. Is the HDRP package installed?");
                return null;
            }

            EnsureFolder("Assets/Art/Materials", "VFX");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool isNew = material == null;

            if (isNew)
            {
                material = new Material(unlit);
            }
            else
            {
                material.shader = unlit;
            }

            Color placeholder = new Color(1.4f, 1.4f, 1.4f);

            material.SetColor("_UnlitColor", placeholder);

            // False means the emissive colour is taken as authored, HDR values
            // and all, instead of being rebuilt from a separate intensity field
            // that the property block does not set.
            HDMaterial.SetUseEmissiveIntensity(material, false);
            HDMaterial.SetEmissiveColor(material, placeholder);

            // Recomputes keywords and render queue for the properties above.
            HDMaterial.SetSurfaceType(material, transparent: false);

            if (isNew)
            {
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                string grandparent = System.IO.Path.GetDirectoryName(parent).Replace('\\', '/');
                string name = System.IO.Path.GetFileName(parent);
                AssetDatabase.CreateFolder(grandparent, name);
            }

            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
