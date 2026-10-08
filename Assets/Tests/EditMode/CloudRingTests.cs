using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The clouds as an eye of the storm (environment roadmap, item 40).
    /// Made by CloudRingBuilder, in the editor assembly, which this one does
    /// not see - so these read the two textures it painted and the scene's
    /// volume profile.
    ///
    /// The one that matters is the eye: no cloud where the ships fly. The map
    /// and the lookup only mean anything together, and with the layer they
    /// are stretched over, so the tests go from metres to texels the way
    /// HDRP does.
    ///
    /// The second is the sky over the eye, which is open. A deck of broken
    /// cloud rode there until 7 October to cross the moon and darken the
    /// scene; it hid too much of the sky and was taken out.
    /// </summary>
    public class CloudRingTests
    {
        private const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";
        private const string MapPath = "Assets/Art/Textures/CloudRingMap.png";
        private const string LookupPath = "Assets/Art/Textures/CloudRingLookup.png";

        /// <summary>HDRP's own, in VolumetricClouds.</summary>
        private const float PlanetRadius = 6378100f;

        /// <summary>The lane with the player's offset of 6, and how far out the camera can be pulled.</summary>
        private const float Lane = 19.72f;
        private const float FurthestCamera = 49.72f;

        private static VolumeProfile Profile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsNotNull(profile, ProfilePath);
            return profile;
        }

        private static VolumetricClouds Clouds()
        {
            Assert.IsTrue(Profile().TryGet(out VolumetricClouds clouds), "the scene has no clouds override");
            return clouds;
        }

        /// <summary>How high the arena's floor is over the planet's surface, which is what the clouds count from.</summary>
        private static float ArenaAltitude()
        {
            Assert.IsTrue(Profile().TryGet(out VisualEnvironment environment), "the scene has no Visual Environment");
            return -environment.planetCenter.value.y * 1000f - PlanetRadius;
        }

        /// <summary>The file's own pixels, whatever the import made of them.</summary>
        private static Texture2D Read(string path)
        {
            Assert.IsTrue(File.Exists(path), path + " is missing: run Survival Chaos > Environment > Build Cloud Ring");
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.LoadImage(File.ReadAllBytes(path));
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        /// <summary>The map's texel over a point, in metres from the island, placed as HDRP places it.</summary>
        private static Color MapOver(Texture2D map, float x, float z)
        {
            VolumetricClouds clouds = Clouds();
            float middle = clouds.bottomAltitude.value + clouds.altitudeRange.value * 0.5f;
            float stretched = Mathf.Sqrt((PlanetRadius + middle) * (PlanetRadius + middle) - PlanetRadius * PlanetRadius);
            Vector2 tiling = clouds.cloudTiling.value;
            Vector2 offset = clouds.cloudOffset.value;
            return map.GetPixelBilinear(x / stretched * tiling.x + offset.x + 0.5f, z / stretched * tiling.y + offset.y + 0.5f);
        }

        /// <summary>How much of the noise is cloud, for what the map says over a point, at a height over the arena's floor.</summary>
        private static float CloudAt(Texture2D map, Texture2D lookup, float x, float z, float overTheArena)
        {
            VolumetricClouds clouds = Clouds();
            Color over = MapOver(map, x, z);
            float height = (ArenaAltitude() + overTheArena - clouds.bottomAltitude.value) / clouds.altitudeRange.value;
            if (height < 0f || height > 1f || height > over.a)
            {
                return 0f;
            }

            return over.r * lookup.GetPixelBilinear(over.b, height).r;
        }

        [Test]
        public void TheScene_UsesThePaintedRing()
        {
            VolumetricClouds clouds = Clouds();

            Assert.IsTrue(clouds.cloudControl.overrideState);
            Assert.AreEqual(VolumetricClouds.CloudControl.Manual, clouds.cloudControl.value);
            Assert.IsTrue(clouds.cloudMap.overrideState && clouds.cloudLut.overrideState);
            Assert.AreEqual(MapPath, AssetDatabase.GetAssetPath(clouds.cloudMap.value));
            Assert.AreEqual(LookupPath, AssetDatabase.GetAssetPath(clouds.cloudLut.value));
        }

        [Test]
        public void TheMap_HoldsStill()
        {
            VolumetricClouds clouds = Clouds();

            Assert.IsTrue(clouds.cloudMapSpeedMultiplier.overrideState, "left alone, HDRP blows the map away at half the wind");
            Assert.AreEqual(0f, clouds.cloudMapSpeedMultiplier.value);
            Assert.AreEqual(Vector2.zero, clouds.cloudOffset.value, "the eye is over the island only with no offset");
        }

        /// <summary>
        /// A storm that only turns keeps every shape it has: Ian's word on
        /// it was that it did not deform or reshape like cloud. So the
        /// shapes climb through the wall and the fine detail climbs faster,
        /// while the map and the lookup stay put and cut new heads out of
        /// them. Well under the speed they go round at, or the wall would
        /// read as cloud pouring upward.
        /// </summary>
        [Test]
        public void TheCloudsShapes_ClimbThroughTheWall_SlowerThanTheyGoRound()
        {
            VolumetricClouds clouds = Clouds();

            Assert.IsTrue(clouds.verticalShapeWindSpeed.overrideState && clouds.verticalErosionWindSpeed.overrideState,
                "left alone, HDRP's clouds do not climb at all");
            Assert.That(clouds.verticalShapeWindSpeed.value, Is.GreaterThanOrEqualTo(40f), "km/h; slower than this a head keeps its shape right across the frame");
            Assert.That(clouds.verticalErosionWindSpeed.value, Is.GreaterThan(clouds.verticalShapeWindSpeed.value));
            Assert.That(clouds.verticalShapeWindSpeed.value, Is.LessThan(clouds.globalWindSpeed.value.customValue / 3f));
        }

        /// <summary>
        /// The eye is kept clear by the map, so nothing may fade the clouds
        /// in by distance: Automatic would start them a fifth of the layer
        /// out, which is the floor under the island gone.
        /// </summary>
        [Test]
        public void TheClouds_AreNotFadedInByDistance()
        {
            VolumetricClouds clouds = Clouds();

            Assert.IsTrue(clouds.fadeInMode.overrideState);
            Assert.AreEqual(VolumetricClouds.CloudFadeInMode.Manual, clouds.fadeInMode.value);
            Assert.That(clouds.fadeInStart.value + clouds.fadeInDistance.value, Is.LessThan(200f));
        }

        [Test]
        public void TheArena_IsInsideTheLayer()
        {
            VolumetricClouds clouds = Clouds();
            float arena = ArenaAltitude();

            Assert.AreEqual(1000f, arena, 1f, "the arena is a kilometre up, by the planet's centre");
            Assert.That(clouds.bottomAltitude.value, Is.LessThan(arena - 100f), "room for a floor under the island");
            Assert.That(clouds.bottomAltitude.value + clouds.altitudeRange.value, Is.GreaterThan(arena + 100f), "room for a wall over the horizon");
        }

        /// <summary>
        /// Everywhere a ship or the camera can be, and well past it: from
        /// 100 under the arena's floor to 200 over it, between the cloud
        /// floor and the deck.
        /// </summary>
        [Test]
        public void TheEye_IsClear_WhereverTheShipsAndTheCameraGo()
        {
            Texture2D map = Read(MapPath);
            Texture2D lookup = Read(LookupPath);

            for (float reach = 0f; reach <= FurthestCamera * 4f; reach += Lane * 0.5f)
            {
                for (int bearing = 0; bearing < 360; bearing += 15)
                {
                    float x = reach * Mathf.Cos(bearing * Mathf.Deg2Rad);
                    float z = reach * Mathf.Sin(bearing * Mathf.Deg2Rad);
                    for (float over = -100f; over <= 200f; over += 25f)
                    {
                        Assert.AreEqual(0f, CloudAt(map, lookup, x, z, over), 1e-3f,
                            "cloud " + reach + " out at " + bearing + " degrees, " + over + " over the arena");
                    }
                }
            }

            Object.DestroyImmediate(map);
            Object.DestroyImmediate(lookup);
        }

        [Test]
        public void TheWall_IsNoNearerThanEightHundredMetres_AndAllTheWayRoundByTwoKilometres()
        {
            Texture2D map = Read(MapPath);
            Texture2D lookup = Read(LookupPath);

            for (int bearing = 0; bearing < 360; bearing += 5)
            {
                float cos = Mathf.Cos(bearing * Mathf.Deg2Rad);
                float sin = Mathf.Sin(bearing * Mathf.Deg2Rad);

                Assert.AreEqual(0f, CloudAt(map, lookup, 800f * cos, 800f * sin, 0f), 1e-3f, "cloud level with the arena, 800 out at " + bearing);
                Assert.That(CloudAt(map, lookup, 2100f * cos, 2100f * sin, 0f), Is.GreaterThan(0.5f), "a gap in the wall at " + bearing);
                Assert.That(CloudAt(map, lookup, 2100f * cos, 2100f * sin, 100f), Is.GreaterThan(0.1f), "the wall does not clear the horizon at " + bearing);
            }

            Object.DestroyImmediate(map);
            Object.DestroyImmediate(lookup);
        }

        /// <summary>
        /// The wall is one bank with heads on it, not towers with the stars
        /// between them. The lookup's green is how much the noise shapes the
        /// cloud: at nine tenths the cloud is whatever the noise leaves, and
        /// that is right for the wall's top and wrong for its body.
        /// </summary>
        [Test]
        public void TheWallsBody_IsSolid_UnderARaggedTop()
        {
            Texture2D map = Read(MapPath);
            Texture2D lookup = Read(LookupPath);
            VolumetricClouds clouds = Clouds();

            for (int bearing = 0; bearing < 360; bearing += 15)
            {
                Color over = MapOver(map, 2100f * Mathf.Cos(bearing * Mathf.Deg2Rad), 2100f * Mathf.Sin(bearing * Mathf.Deg2Rad));
                float level = (ArenaAltitude() - clouds.bottomAltitude.value) / clouds.altitudeRange.value;
                float top = level;
                while (top < 1f && lookup.GetPixelBilinear(over.b, top).r > 0.5f)
                {
                    top += 1f / lookup.height;
                }

                Assert.That(lookup.GetPixelBilinear(over.b, level).g, Is.LessThan(0.7f), "the wall is towers level with the arena at " + bearing);
                Assert.That(lookup.GetPixelBilinear(over.b, top).g, Is.GreaterThan(0.85f), "the wall's top is a smooth bank at " + bearing);
            }

            Object.DestroyImmediate(map);
            Object.DestroyImmediate(lookup);
        }

        /// <summary>
        /// The low wall, which is the one Ian picked: every bearing stands
        /// well clear of the horizon, and none reaches the layer's top. From
        /// the camera that is heads between eight and fifteen degrees up.
        /// </summary>
        [Test]
        public void TheWall_IsHighAllTheWayRound_AndNowhereTall()
        {
            Texture2D map = Read(MapPath);
            Texture2D lookup = Read(LookupPath);

            for (int bearing = 0; bearing < 360; bearing += 5)
            {
                float x = 2100f * Mathf.Cos(bearing * Mathf.Deg2Rad);
                float z = 2100f * Mathf.Sin(bearing * Mathf.Deg2Rad);

                Assert.That(CloudAt(map, lookup, x, z, 250f), Is.GreaterThan(0.5f), "the wall is under seven degrees high at " + bearing);
                Assert.That(CloudAt(map, lookup, x, z, 650f), Is.LessThan(0.35f), "the wall is over seventeen degrees high at " + bearing);
            }

            Object.DestroyImmediate(map);
            Object.DestroyImmediate(lookup);
        }

        /// <summary>
        /// The map's green is how fast the cloud swallows light. Every tier
        /// starts the clouds on six light steps, and with so few a heavy wall
        /// shows a grain of dark specks on its lit side: plain at 0.35.
        /// </summary>
        [Test]
        public void TheWallsCloud_IsLightEnoughForSixLightSteps()
        {
            Texture2D map = Read(MapPath);

            for (int bearing = 0; bearing < 360; bearing += 5)
            {
                float weight = MapOver(map, 2500f * Mathf.Cos(bearing * Mathf.Deg2Rad), 2500f * Mathf.Sin(bearing * Mathf.Deg2Rad)).g;
                Assert.That(weight, Is.LessThan(0.32f), "the wall at " + bearing + " is heavy enough to grain");
            }

            Object.DestroyImmediate(map);
        }

        /// <summary>
        /// Until 7 October a deck of heavy cloud rode over the eye, 240 over
        /// the island and up, to cross the moon and darken the scene. Ian
        /// found the clouds hid too much of the sky, so it is out, and the
        /// sky over the eye is open from the island to the layer's top. If
        /// the moon is raised again and the dark spells are wanted back, the
        /// builder's Deck puts it back and this test goes the other way.
        /// </summary>
        [Test]
        public void TheSkyOverTheEye_IsOpen()
        {
            Texture2D map = Read(MapPath);
            Texture2D lookup = Read(LookupPath);

            foreach (float over in new[] { 250f, 450f, 700f })
            {
                Assert.AreEqual(0f, CloudAt(map, lookup, 0f, 0f, over), 1e-3f, "cloud " + over + " over the island");
                for (int bearing = 0; bearing < 360; bearing += 15)
                {
                    float x = 550f * Mathf.Cos(bearing * Mathf.Deg2Rad);
                    float z = 550f * Mathf.Sin(bearing * Mathf.Deg2Rad);

                    Assert.AreEqual(0f, CloudAt(map, lookup, x, z, over), 1e-3f, "cloud " + over + " over the eye, 550 out at " + bearing);
                }
            }

            Object.DestroyImmediate(map);
            Object.DestroyImmediate(lookup);
        }

        [Test]
        public void TheFloor_LiesUnderTheIsland()
        {
            Texture2D map = Read(MapPath);
            Texture2D lookup = Read(LookupPath);

            Assert.That(CloudAt(map, lookup, 0f, 0f, -500f), Is.GreaterThan(0.9f), "no floor 500 under the island");
            Assert.AreEqual(0f, CloudAt(map, lookup, 0f, 0f, -200f), 1e-3f, "the floor comes within 200 of the island");

            Object.DestroyImmediate(map);
            Object.DestroyImmediate(lookup);
        }

        /// <summary>The map clamps, so its edge is what stands from there to the horizon.</summary>
        [Test]
        public void TheMapsEdge_IsWall()
        {
            Texture2D map = Read(MapPath);

            for (int i = 0; i < map.width; i += 8)
            {
                foreach (Color edge in new[] { map.GetPixel(i, 0), map.GetPixel(i, map.height - 1), map.GetPixel(0, i), map.GetPixel(map.width - 1, i) })
                {
                    Assert.That(edge.r, Is.GreaterThan(0.99f));
                    Assert.That(edge.b, Is.GreaterThan(0.4f), "the edge at " + i + " is floor, so the wall ends where the map does");
                }
            }

            Object.DestroyImmediate(map);
        }

        /// <summary>
        /// Both are numbers, not pictures: colour-managed, compressed or
        /// mipped they say something else. And the map must clamp, or the
        /// eye repeats every sixteen kilometres.
        /// </summary>
        [TestCase(MapPath)]
        [TestCase(LookupPath)]
        public void TheTextures_AreImportedAsData(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, path + " is missing: run Survival Chaos > Environment > Build Cloud Ring");

            Assert.IsFalse(importer.sRGBTexture, "colour-managed");
            Assert.IsFalse(importer.mipmapEnabled, "mipped");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        }
    }
}
