using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Writes the element's pixel size into the mesh's second UV channel, which
    /// is where the holographic shaders read it from.
    ///
    /// Without this the shaders can only work in UV space, and every measurement
    /// - corner cut, border width, bracket length - would stretch with the
    /// element. A health bar and a square skill slot would not look like parts of
    /// the same interface.
    ///
    /// The canvas must list TexCoord1 in its additional shader channels or this
    /// data never reaches the GPU; HoloUiBuilder sets that, and this component
    /// says so rather than failing silently.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Holo Rect Data")]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class HoloRectData : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper == null)
            {
                return;
            }

            Vector2 size = ((RectTransform)transform).rect.size;
            UIVertex vertex = default;

            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = size;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }

        /// <summary>
        /// A resize changes the value being written, so the mesh has to be built
        /// again. Unity rebuilds on its own for layout, but not for a plain
        /// anchor change, which would otherwise leave stale sizes on screen.
        /// </summary>
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            if ((root.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
            {
                Debug.LogWarning(
                    "Canvas '" + root.name + "' is not sending TexCoord1, so holographic elements " +
                    "under it will fall back to a fixed size and look wrong. Add TexCoord1 to the " +
                    "canvas's Additional Shader Channels.", root);
            }
        }
#endif
    }
}
