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
            AssignTintTarget(pickup, renderer);
        }

        /// <summary>
        /// Points the Pickup component at the renderer it should tint. The field
        /// is private and serialized, so it goes through SerializedObject.
        ///
        /// Pickup falls back to the first renderer in its children if this is
        /// left empty, so this is belt and braces - but the fallback runs a
        /// GetComponentInChildren on the first spawn of every pickup, and being
        /// explicit costs nothing.
        /// </summary>
        private static void AssignTintTarget(Pickup pickup, Renderer renderer)
        {
            SerializedObject serialized = new SerializedObject(pickup);
            SerializedProperty target = serialized.FindProperty("tintTarget");

            if (target == null)
            {
                Debug.LogWarning(
                    "Pickup has no 'tintTarget' field; it will find its renderer at runtime instead.");
                return;
            }

            target.objectReferenceValue = renderer;
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
