using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Writes into every tree and ruin of the scenery model how thick it is at
    /// each vertex, as the model is imported (environment roadmap, item 47,
    /// 8 October 2026). The scenery's shader (Art/Shaders/SceneryFar) reads it
    /// and leaves out what is under a pixel wide at the distance it is drawn
    /// from. <see cref="ThinParts"/> has the two measures and why there are
    /// two.
    ///
    /// The number rides in the fourth UV set, which nothing else uses: the
    /// first is the model's own and the second the lightmap's. It is in
    /// world units as the model stands in its own file, so a tree scaled in
    /// the scene is that much out, which a threshold of about a pixel does
    /// not notice.
    ///
    /// This runs for the one model and nothing else. It has no version
    /// number on purpose: one would reimport every model in the project when
    /// it changed. After changing this file or ThinParts, reimport the
    /// scenery model by hand; SceneryFarTests fails while the model on disk
    /// has no thickness in it.
    /// </summary>
    public sealed class SceneryThinParts : AssetPostprocessor
    {
        public const string ModelPath = "Assets/Art/Models/Scenario/cenario lava v02_unity.fbx";

        /// <summary>The UV set the thickness is written to, counting from 0.</summary>
        public const int Channel = 3;

        /// <summary>The trees' material, by name. A tree is one skin, measured through.</summary>
        public const string TreeMaterial = "avores";

        /// <summary>The ruins' material, by name. A ruin is a wall and separate bars, measured part by part.</summary>
        public const string RuinMaterial = "predio";

        private void OnPostprocessModel(GameObject root)
        {
            if (assetPath != ModelPath)
            {
                return;
            }

            List<Vector2> into = new List<Vector2>();

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                Renderer renderer = filter.GetComponent<Renderer>();
                if (mesh == null || renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }

                string material = renderer.sharedMaterial.name;
                if (material != TreeMaterial && material != RuinMaterial)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                float[] thickness = new float[vertices.Length];

                if (material == TreeMaterial)
                {
                    ThinParts.Through(vertices, triangles, thickness, mesh.bounds.size.magnitude,
                        filter.transform.InverseTransformDirection(Vector3.up));
                }
                else
                {
                    ThinParts.Measure(vertices, triangles, thickness);
                }

                Vector3 scale = filter.transform.lossyScale;
                float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));

                into.Clear();
                foreach (float thick in thickness)
                {
                    into.Add(new Vector2(thick * largest, 0f));
                }

                mesh.SetUVs(Channel, into);
            }
        }
    }
}
