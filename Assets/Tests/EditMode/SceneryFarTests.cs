using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The far trees and ruins (environment roadmap, item 47, built 8 October
    /// 2026): every vertex of the scenery carries how thick the mesh is
    /// there, and the scenery's shader leaves out what is under a pixel wide
    /// at the distance it is drawn from.
    ///
    /// Three things would fail without a sound. The model reimported with
    /// no thickness in it, which draws everything as before. A material put
    /// back on HDRP's own shader, the same. And a measure that calls a
    /// twig's end thick, which leaves specks hanging in the air where the
    /// twig was: the first two tries did exactly that.
    /// </summary>
    public class SceneryFarTests
    {
        private const string ModelPath = "Assets/Art/Models/Scenario/cenario lava v02_unity.fbx";
        private const string ShaderPath = "Assets/Art/Shaders/SceneryFar.shadergraph";
        private const string FunctionPath = "Assets/Art/Shaders/SceneryFar.hlsl";

        /// <summary>A square post standing on y, its rings at the heights and half-widths given, capped at both ends.</summary>
        private static void Post(float[] heights, float[] halves, out Vector3[] vertices, out int[] triangles)
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();

            for (int ring = 0; ring < heights.Length; ring++)
            {
                float h = halves[ring], y = heights[ring];
                v.Add(new Vector3(-h, y, -h));
                v.Add(new Vector3(h, y, -h));
                v.Add(new Vector3(h, y, h));
                v.Add(new Vector3(-h, y, h));
            }

            for (int ring = 0; ring + 1 < heights.Length; ring++)
            {
                for (int side = 0; side < 4; side++)
                {
                    int a = ring * 4 + side, b = ring * 4 + (side + 1) % 4, c = a + 4, d = b + 4;
                    t.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            int top = (heights.Length - 1) * 4;
            t.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            t.AddRange(new[] { top, top + 2, top + 1, top, top + 3, top + 2 });

            vertices = v.ToArray();
            triangles = t.ToArray();
        }

        [Test]
        public void ATree_IsThickAtItsFoot_AndOnlyGetsThinnerOnTheWayUp()
        {
            Post(new[] { 0f, 1f, 2f, 3f }, new[] { 0.2f, 0.1f, 0.03f, 0.01f }, out Vector3[] vertices, out int[] triangles);
            float[] thickness = new float[vertices.Length];

            ThinParts.Through(vertices, triangles, thickness, 10f, Vector3.up);

            Assert.That(thickness[0], Is.GreaterThan(0.15f), "the foot of the post is 0.4 across");

            for (int ring = 0; ring < 3; ring++)
            {
                Assert.That(thickness[(ring + 1) * 4], Is.LessThanOrEqualTo(thickness[ring * 4] + 1e-5f), "ring " + (ring + 1) + " reads thicker than the ring under it");
            }

            // Straight in from the cap on the tip is three units of post. The
            // tip is still as thin as the twig that carries it.
            Assert.That(thickness[12], Is.LessThan(0.1f), "the tip reads as thick as the post is long, and would be left hanging when the twig under it goes");
        }

        [Test]
        public void ARuin_ComesApartIntoItsParts_AndABarIsThinWhereAWallIsNot()
        {
            // A wall: wide and tall, 0.1 thick. A bar: 1 long, 0.05 square, standing clear of it.
            Vector3[] wall = Box(new Vector3(0f, 0f, 0f), new Vector3(2f, 1.5f, 0.1f));
            Vector3[] bar = Box(new Vector3(5f, 0f, 0f), new Vector3(0.05f, 1f, 0.05f));

            Vector3[] vertices = new Vector3[16];
            wall.CopyTo(vertices, 0);
            bar.CopyTo(vertices, 8);

            List<int> triangles = new List<int>();
            foreach (int offset in new[] { 0, 8 })
            {
                foreach (int i in BoxTriangles)
                {
                    triangles.Add(i + offset);
                }
            }

            float[] thickness = new float[16];
            int parts = ThinParts.Measure(vertices, triangles.ToArray(), thickness);

            Assert.AreEqual(2, parts);
            Assert.AreEqual(1.5f, thickness[0], 1e-3f, "a wall is thin one way only: its middle size is its height");
            Assert.AreEqual(0.05f, thickness[8], 1e-3f, "a bar is thin two ways");
        }

        [Test]
        public void NothingIsThickerThanTheWayToIt()
        {
            // A row of five places: thick, thin, thick, and one off on its own.
            float[] thickness = { 5f, 1f, 9f, 9f, 7f };
            List<int>[] beside = { new List<int> { 1 }, new List<int> { 0, 2 }, new List<int> { 1, 3 }, new List<int> { 2 }, null };

            ThinParts.NoThickerThanTheWayThere(thickness, beside, 0);

            Assert.AreEqual(new[] { 5f, 1f, 1f, 1f, 0f }, thickness);
        }

        private static readonly int[] BoxTriangles =
        {
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7,
        };

        private static Vector3[] Box(Vector3 corner, Vector3 size)
        {
            return new[]
            {
                corner, corner + new Vector3(size.x, 0f, 0f), corner + new Vector3(size.x, size.y, 0f), corner + new Vector3(0f, size.y, 0f),
                corner + new Vector3(0f, 0f, size.z), corner + new Vector3(size.x, 0f, size.z), corner + size, corner + new Vector3(0f, size.y, size.z),
            };
        }

        [Test]
        public void TheSceneryModel_CarriesAThickness_OnEveryTreeAndRuin()
        {
            int measured = 0;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Mesh mesh && mesh.uv4.Length > 0)
                {
                    Assert.AreEqual(mesh.vertexCount, mesh.uv4.Length, mesh.name);
                    measured++;
                }
            }

            Assert.AreEqual(87, measured,
                "the scenery's 87 trees and ruins do not all carry a thickness. Reimport " + ModelPath + " (right-click, Reimport).");
        }

        [TestCase("Assets/Art/Materials/Scenario/avores.mat")]
        [TestCase("Assets/Art/Materials/Scenario/predio.mat")]
        public void TheSceneryMaterials_UseTheShader_AndDropWhatIsUnderAboutAPixel(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, path);
            Assert.AreEqual(ShaderPath, AssetDatabase.GetAssetPath(material.shader),
                path + " is not on the scenery's shader, so its thin parts are drawn at any distance");
            Assert.That(material.GetFloat("_FarThin"), Is.InRange(0.5f, 1.5f),
                "under half a pixel leaves the crawl in; at a pixel and a half the far trees are stumps");
        }

        [Test]
        public void TheShader_LeavesShadowsAndTheBakeWhole()
        {
            string function = File.ReadAllText(FunctionPath);
            StringAssert.Contains("SHADERPASS == SHADERPASS_SHADOWS", function, "a shadow is cast from the light's place, not the camera's");
            StringAssert.Contains("SHADERPASS == SHADERPASS_LIGHT_TRANSPORT", function, "the bake must not depend on where the camera stood");
            StringAssert.Contains("_ScreenSize.y", function, "the pixel has to be the size being rendered at, before an upscaler");

            string guid = AssetDatabase.AssetPathToGUID(FunctionPath);
            StringAssert.Contains(guid, File.ReadAllText(ShaderPath), "the shader graph does not call " + FunctionPath);
        }
    }
}
