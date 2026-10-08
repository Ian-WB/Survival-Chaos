using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The island's rock, its underside and how its light is stored
    /// (environment roadmap, items 43 to 46). The builders are in the editor
    /// assembly, which this one does not see, so these read what they made:
    /// the materials, the two sheets, and the saved Game scene.
    /// </summary>
    public class IslandRockAndLightTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string ShaderName = "Shader Graphs/IslandRock";
        private const string IncludePath = "Assets/Art/Shaders/IslandRock.hlsl";
        private const string GraphPath = "Assets/Art/Shaders/IslandRock.shadergraph";
        private const string DetailPath = "Assets/Art/Textures/IslandRockDetail.png";
        private const string HeatPath = "Assets/Art/Textures/IslandHeat.png";
        private const string LightingPath = "Assets/Scenes/Lighting/New Lighting Settings.lighting";

        /// <summary>How much ground the heat map covers, as IslandRockBuilder paints it and the materials are told.</summary>
        private const float HeatSpan = 48f;

        // Light.m_Lightmapping in a saved scene.
        private const float Realtime = 4f;
        private const float Baked = 2f;

        private static readonly (string name, Color tint)[] Rock =
        {
            ("ilha", new Color(0.313f, 0.171f, 0.179f)),
            ("pedra", new Color(0.153f, 0.086f, 0.078f)),
            ("METADE CIMA", new Color(0.493f, 0.184f, 0.188f)),
            ("ponta", new Color(0.513f, 0.334f, 0.213f))
        };

        // Item 43: the rock.

        /// <summary>
        /// The island was four flat colours, and each material is that
        /// colour's rock now. A material put back on HDRP/Lit, or with its
        /// tint lost, is a patch of the old island on the new one.
        /// </summary>
        [Test]
        public void TheFourRockMaterials_WearTheRockShader_InTheirOwnColours()
        {
            foreach ((string name, Color tint) in Rock)
            {
                string path = "Assets/Art/Materials/Scenario/" + name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(material, path);
                Assert.AreEqual(ShaderName, material.shader.name, name + ": run Survival Chaos > Build Island Rock");

                Color kept = material.GetColor("_Tint");
                Assert.AreEqual(tint.r, kept.r, 0.002f, name);
                Assert.AreEqual(tint.g, kept.g, 0.002f, name);
                Assert.AreEqual(tint.b, kept.b, 0.002f, name);

                Assert.AreEqual(DetailPath, AssetDatabase.GetAssetPath(material.GetTexture("_Detail")), name);
                Assert.AreEqual(HeatPath, AssetDatabase.GetAssetPath(material.GetTexture("_Heat")), name);
                Assert.AreEqual(HeatSpan, material.GetVector("_HeatBox").z, name + ": the heat map is laid over the wrong ground");
                Assert.That(material.GetFloat("_Tile"), Is.InRange(0.05f, 0.5f), name);
                Assert.AreEqual(MaterialGlobalIlluminationFlags.EmissiveIsBlack, material.globalIlluminationFlags,
                    name + ": the cracks' glow comes and goes with the eruption, and must stay out of the bake");
            }
        }

        /// <summary>
        /// Both sheets are numbers, not colours: a colour curve on the
        /// detail sheet tilts every face one way. The detail sheet repeats
        /// and keeps its alpha; the heat map must not repeat, or the far
        /// rim would glow with the lava on the other side.
        /// </summary>
        [Test]
        public void TheTwoSheets_AreImportedAsNumbers()
        {
            TextureImporter detail = (TextureImporter)AssetImporter.GetAtPath(DetailPath);
            TextureImporter heat = (TextureImporter)AssetImporter.GetAtPath(HeatPath);
            Assert.IsNotNull(detail, DetailPath);
            Assert.IsNotNull(heat, HeatPath);

            Assert.IsFalse(detail.sRGBTexture);
            Assert.IsFalse(heat.sRGBTexture);
            Assert.AreEqual(TextureImporterType.Default, detail.textureType, "imported as a normal map, the sheet loses its cracks and its light-and-dark");
            Assert.AreEqual(TextureWrapMode.Repeat, detail.wrapMode);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, detail.alphaSource);
            Assert.AreEqual(TextureWrapMode.Clamp, heat.wrapMode);
        }

        /// <summary>
        /// The heat map is the island from above. It is hot where the lava
        /// runs, by the lava lights that stand along it, and cold on the far
        /// side of the island and at its own edge.
        /// </summary>
        [Test]
        public void TheHeatMap_IsHotAlongTheLava_AndColdAcrossTheIsland()
        {
            Texture2D heat = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

            try
            {
                heat.LoadImage(File.ReadAllBytes(HeatPath));
                SavedScene scene = SavedScene.Load(ScenePath);

                foreach (int light in new[] { 2, 3, 5 })
                {
                    string name = "Lava Light " + light;
                    Vector3 at = scene.Vector(scene.Component(scene.GameObjectNamed(name), "Transform"), "m_LocalPosition");
                    Assert.That(HeatAt(heat, at.x, at.z), Is.GreaterThan(0.3f), "the map is cold at " + name + ", which stands by the lava");
                }

                Assert.AreEqual(0f, HeatAt(heat, -12f, 8f), 0.01f, "the map is warm on the far side of the island");
                Assert.AreEqual(0f, HeatAt(heat, -23.5f, -23.5f), 0.01f, "the map is warm at its own corner");
            }
            finally
            {
                Object.DestroyImmediate(heat);
            }
        }

        private static float HeatAt(Texture2D heat, float x, float z)
        {
            return heat.GetPixelBilinear(x / HeatSpan + 0.5f, z / HeatSpan + 0.5f).r;
        }

        /// <summary>
        /// The graph is one Custom Function node that calls the include, and
        /// the eruption reaches the rock through one global. Each end names
        /// the other only by a string.
        /// </summary>
        [Test]
        public void TheGraph_CallsTheInclude_AndTheEruptionReachesIt()
        {
            string include = File.ReadAllText(IncludePath);
            string graph = File.ReadAllText(GraphPath);

            StringAssert.Contains("void IslandRock_float(", include);
            StringAssert.Contains("float _IslandEruption;", include);
            StringAssert.Contains("\"m_FunctionName\": \"IslandRock\"", graph);
            StringAssert.Contains("\"m_FunctionSource\": \"" + AssetDatabase.AssetPathToGUID(IncludePath) + "\"", graph,
                "the graph's Custom Function node points at some other file than " + IncludePath);
            StringAssert.Contains("Shader.PropertyToID(\"_IslandEruption\")", File.ReadAllText("Assets/Scripts/Gameplay/Arena/Eruption.cs"));
        }

        // Item 44: the underside.

        [Test]
        public void TheUnderside_HasHangingRock_AndAGlowUnderTheLavasFall()
        {
            string text = File.ReadAllText(ScenePath);
            SavedScene scene = SavedScene.Load(ScenePath);

            Assert.IsNotNull(scene.GameObjectNamed("Island Underside"), "run Survival Chaos > Build Island Underside, and save the scene");
            Assert.That(Regex.Matches(text, @"m_Name: Hanging Rock \d+").Count, Is.GreaterThanOrEqualTo(20), "hardly any rock hangs under the island");

            string glow = scene.GameObjectNamed("Underglow");
            Assert.IsNotNull(glow, "no Underglow");
            string light = scene.Component(glow, "Light");
            Assert.AreEqual(Realtime, scene.Float(light, "m_Lightmapping"), "the haze only takes light from a light that is live");
            Assert.That(scene.Vector(scene.Component(glow, "Transform"), "m_LocalPosition").y, Is.LessThan(-3f), "the underglow is not under the rim");
        }

        // Item 45: the lights.

        /// <summary>
        /// Six overlapping Mixed lights wanted six shadowmask channels and
        /// there are four, so two of them had silently become bake-only.
        /// Now all six are Baked on purpose, and nothing in the scene is
        /// Mixed, so no shadowmask is baked at all.
        /// </summary>
        [Test]
        public void TheLavaLights_AreBaked_AndNothingIsMixed()
        {
            SavedScene scene = SavedScene.Load(ScenePath);

            for (int i = 1; i <= 6; i++)
            {
                string name = "Lava Light " + i;
                Assert.AreEqual(Baked, scene.Float(scene.Component(scene.GameObjectNamed(name), "Light"), "m_Lightmapping"), name);
            }

            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(ScenePath), @"\n  m_Lightmapping: 1\r?\n"),
                "a light in the Game scene is Mixed: the bake makes a shadowmask again");
        }

        /// <summary>
        /// A baked light gives the haze nothing, so three live copies stand
        /// where lava lights stand, for the haze and the plume alone: on a
        /// light layer no renderer is on, with the island still throwing
        /// its shadow into them. Without that shadow the glow wraps round
        /// the cone, which read as the rock being transparent.
        /// </summary>
        [Test]
        public void TheGlowLights_LightTheHazeOnly_AndTheIslandShadowsThem()
        {
            SavedScene scene = SavedScene.Load(ScenePath);

            foreach (string name in new[] { "Crater Glow", "Cone Glow", "River Glow" })
            {
                string glow = scene.GameObjectNamed(name);
                Assert.IsNotNull(glow, "no " + name);
                string light = scene.Component(glow, "Light");
                string data = scene.ScriptWith(glow, "m_LinkShadowLayers");

                Assert.AreEqual(Realtime, scene.Float(light, "m_Lightmapping"), name);
                Assert.AreEqual(0f, scene.Float(data, "m_LinkShadowLayers"), name + ": its shadows are on the layer it lights, where nothing is");
                Assert.AreEqual(128f, scene.Float(data, "m_LightlayersMask"), name + " lights something other than the haze");
                Assert.AreEqual(1f, scene.Float(light, "m_RenderingLayerMask"), name + ": the island does not shadow it");
                Assert.That(scene.Float(light, "m_Shadows", "m_Type"), Is.GreaterThan(0f), name + " casts no shadow");
            }
        }

        /// <summary>
        /// The eruption used to swell Lava Light 1, which is baked now and
        /// cannot swell. It swells the crater's glow, and brings up a live
        /// light on the island that is off in the saved scene.
        /// </summary>
        [Test]
        public void TheEruption_SwellsTheCratersGlow_AndTheSurgeLight()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string eruption = scene.ScriptWith(scene.GameObjectNamed("Eruption"), "surgeLight");
            Assert.IsNotNull(eruption, "the Eruption has no surgeLight field in the saved scene");

            string crater = scene.Component(scene.GameObjectNamed("Crater Glow"), "Light");
            string surge = scene.Component(scene.GameObjectNamed("Surge Light"), "Light");

            Assert.AreEqual(crater, scene.Reference(eruption, "craterLight"), "the eruption swells some other light than the crater's glow");
            Assert.AreEqual(surge, scene.Reference(eruption, "surgeLight"));
            Assert.AreEqual(Realtime, scene.Float(surge, "m_Lightmapping"));
            Assert.AreEqual(0f, scene.Float(surge, "m_Enabled"), "the surge light is on at rest");
            Assert.That(scene.Float(surge, "m_Intensity"), Is.GreaterThan(0f), "the surge light has nothing to come up to");
        }

        // Item 46: the bake.

        /// <summary>
        /// The rock has relief now, and relief only shows in baked light if
        /// the bake keeps which way the light came from.
        /// </summary>
        [Test]
        public void TheBake_KeepsItsDirection()
        {
            StringAssert.Contains("m_LightmapsBakeMode: 1", File.ReadAllText(LightingPath),
                "the lightmaps are not directional, so the rock's relief is flat wherever the light is baked");
        }
    }
}
