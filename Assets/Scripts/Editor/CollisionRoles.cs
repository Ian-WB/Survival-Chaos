using System;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The physics layers the prefab builders put their colliders on.
    ///
    /// Since 25 September 2026 each gameplay role collides on its own layer, and
    /// only six pairs of them meet (see CollisionLayersTests). The prefabs were
    /// moved onto those layers by hand, and a builder that made its object fresh
    /// left it on Default, so rebuilding a prefab quietly undid the move for it:
    /// Default still meets every gameplay layer. The builders ask for the layer
    /// by name through here instead, and refuse to build when a name no longer
    /// resolves, rather than save a prefab on the wrong layer.
    /// </summary>
    internal static class CollisionRoles
    {
        public const string Boss = "Boss";
        public const string Pickups = "Pickups";

        /// <summary>The layer called <paramref name="name"/>.</summary>
        /// <exception cref="InvalidOperationException">No layer has that name.</exception>
        public static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);

            if (layer < 0)
            {
                throw new InvalidOperationException(
                    "No physics layer called \"" + name + "\" in Project Settings > Tags and Layers. " +
                    "Nothing was saved: a prefab built without it would collide on Default.");
            }

            return layer;
        }

        /// <summary>
        /// Puts <paramref name="root"/> and every child that carries a collider on
        /// the layer called <paramref name="name"/>. Children with no collider are
        /// left alone: their layer decides nothing in physics, and some are on
        /// another layer on purpose for rendering.
        /// </summary>
        public static void Assign(GameObject root, string name)
        {
            int layer = Layer(name);
            root.layer = layer;

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.gameObject.layer = layer;
            }
        }
    }
}
