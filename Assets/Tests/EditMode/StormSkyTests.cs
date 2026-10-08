using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The sky behind the storm (environment roadmap, item 41). Painted by
    /// StormSkyBuilder, in the editor assembly, which this one does not see -
    /// so these read the sheet it wrote, the way the importer does, and the
    /// scene's volume profile.
    ///
    /// The one that matters is the planet. It is painted where the scene's
    /// moon shines from, and Ian turns that light himself: when he does, the
    /// planet is left behind in the wrong part of the sky and nothing else
    /// says so.
    ///
    /// The rest hold the sky to what it was made for: dark, so cloud reads
    /// against it; with nothing warm in it, because the lava owns orange and
    /// red; and the same size and format as the sheet it replaced, so it
    /// costs the frame nothing.
    /// </summary>
    public class StormSkyTests
    {
        private const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";
        private const string DefaultProfilePath = "Assets/Settings/DefaultSettingsVolumeProfile.asset";
        private const string SkyPath = "Assets/Art/Skybox/StormSky.png";
        private const string OldSkyPath = "Assets/Art/Skybox/StarfieldSky.png";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>StormSkyBuilder's: the planet's half width in degrees, and the light it stands for.</summary>
        private const float PlanetRadius = 12f;
        private const string MoonName = "Directional Light";

        private Texture2D sheet;
        private NativeArray<byte> bytes;
        private int stride;
        private int red;

        [OneTimeSetUp]
        public void ReadTheSheet()
        {
            Assert.IsTrue(File.Exists(SkyPath), SkyPath + " is missing: run Survival Chaos > Build Storm Sky");
            sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(SkyPath));

            // Read in place: a copy of fifty million texels is not worth making.
            // Unity loads a PNG as ARGB, so red is the second byte.
            bytes = sheet.GetRawTextureData<byte>();
            stride = sheet.format == TextureFormat.RGB24 ? 3 : 4;
            red = sheet.format == TextureFormat.ARGB32 ? 1 : 0;
        }

        [OneTimeTearDown]
        public void DropTheSheet()
        {
            Object.DestroyImmediate(sheet);
        }

        private Color32 Texel(int x, int y)
        {
            int at = (y * sheet.width + x) * stride + red;
            return new Color32(bytes[at], bytes[at + 1], bytes[at + 2], 255);
        }

        /// <summary>
        /// The sheet's texel along a direction in the world. The sheet is a
        /// horizontal cross, +Y over +Z and -Y under it, with -X, +Z, +X, -Z
        /// along the middle, each face as it is seen from inside the cube.
        /// </summary>
        private Color32 Towards(Vector3 direction)
        {
            float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y), z = Mathf.Abs(direction.z);
            int column, rowFromTop;
            float across, up;

            if (y >= x && y >= z)
            {
                column = 1;
                rowFromTop = direction.y > 0f ? 0 : 2;
                across = direction.x / y;
                up = direction.y > 0f ? -direction.z / y : direction.z / y;
            }
            else if (x >= z)
            {
                column = direction.x > 0f ? 2 : 0;
                rowFromTop = 1;
                across = direction.x > 0f ? -direction.z / x : direction.z / x;
                up = direction.y / x;
            }
            else
            {
                column = direction.z > 0f ? 1 : 3;
                rowFromTop = 1;
                across = direction.z > 0f ? direction.x / z : -direction.x / z;
                up = direction.y / z;
            }

            int face = sheet.width / 4;
            int px = Mathf.Clamp(Mathf.FloorToInt((across * 0.5f + 0.5f) * face), 0, face - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt((up * 0.5f + 0.5f) * face), 0, face - 1);
            return Texel(column * face + px, (2 - rowFromTop) * face + py);
        }

        private static int Brightest(Color32 colour)
        {
            return Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
        }

        /// <summary>The way to the moon, from the scene as it is saved.</summary>
        private static Vector3 ToTheMoon()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string moon = scene.GameObjectNamed(MoonName);
            Assert.IsNotNull(moon, "no " + MoonName + " in " + ScenePath);
            return -(scene.WorldRotation(scene.Component(moon, "Transform")) * Vector3.forward);
        }

        /// <summary>How much of a ring round the moon's line, so many degrees out, is brighter than a level.</summary>
        private float ShareOfRingBrighterThan(Vector3 toMoon, float degreesOut, int level)
        {
            Vector3 aside = Vector3.Cross(Vector3.up, toMoon).normalized;
            Vector3 over = Vector3.Cross(toMoon, aside);
            float out_ = degreesOut * Mathf.Deg2Rad;
            int bright = 0;
            const int Points = 360;

            for (int i = 0; i < Points; i++)
            {
                float round = i * Mathf.PI * 2f / Points;
                Vector3 direction = Mathf.Cos(out_) * toMoon
                    + Mathf.Sin(out_) * (Mathf.Cos(round) * aside + Mathf.Sin(round) * over);
                if (Brightest(Towards(direction)) > level)
                {
                    bright++;
                }
            }

            return bright / (float)Points;
        }

        [Test]
        public void TheScene_ShowsTheStormSky()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsNotNull(profile, ProfilePath);
            Assert.IsTrue(profile.TryGet(out HDRISky sky), "the scene has no HDRI Sky override");

            Assert.IsTrue(sky.hdriSky.overrideState);
            Assert.AreEqual(SkyPath, AssetDatabase.GetAssetPath(sky.hdriSky.value));

            // The sheet is painted in the world's own directions. Any turn here
            // moves the planet off the moon's line.
            Assert.IsTrue(sky.rotation.overrideState);
            Assert.AreEqual(0f, sky.rotation.value, 0.001f);
        }

        /// <summary>
        /// The pipeline's default profile is built into the game with
        /// whatever it names. While it named the old sheet both skies
        /// shipped: the first build with this one was 33 MB bigger.
        /// </summary>
        [Test]
        public void TheOldSheet_IsNotBuiltIn()
        {
            VolumeProfile fallback = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath);
            Assert.IsNotNull(fallback, DefaultProfilePath);

            if (fallback.TryGet(out HDRISky sky) && sky.hdriSky.value != null)
            {
                Assert.AreEqual(SkyPath, AssetDatabase.GetAssetPath(sky.hdriSky.value),
                    "The default profile names another sky, so two are built in. Run Survival Chaos > Build Storm Sky.");
            }

            string guid = AssetDatabase.AssetPathToGUID(OldSkyPath);
            foreach (string path in new[] { DefaultProfilePath, ProfilePath, ScenePath, "Assets/Scenes/Menu.unity" })
            {
                StringAssert.DoesNotContain(guid, File.ReadAllText(path), path + " still names " + OldSkyPath + ".");
            }
        }

        [Test]
        public void ThePlanet_StandsWhereTheMoonShinesFrom()
        {
            Vector3 toMoon = ToTheMoon();
            string where = " The moon is " + (Mathf.Asin(toMoon.y) * Mathf.Rad2Deg).ToString("0.0") +
                           " degrees up towards " + toMoon.ToString("0.00") +
                           ". If the light has been turned, run Survival Chaos > Build Storm Sky with the Game scene open.";

            // Half way out from its middle the moon is lit all the way round:
            // it is nearly full. (Until 7 October it was a planet showing a
            // crescent, and this test held its middle dark.)
            Assert.Greater(ShareOfRingBrighterThan(toMoon, PlanetRadius * 0.5f, 60), 0.9f,
                "The moon is not lit all the way round half way out from its middle." + where);
            Assert.Greater(Brightest(Towards(toMoon)), 50, "The middle of the moon is dark." + where);

            // Just inside its edge the side towards its sun is lit and the
            // side away is not, which is what makes it a ball.
            float edge = ShareOfRingBrighterThan(toMoon, PlanetRadius - 0.2f, 90);
            Assert.Greater(edge, 0.3f, "The moon's edge is hardly lit." + where);
            Assert.Less(edge, 0.7f, "The moon's edge is lit all the way round, which is a plate, not a ball.");

            // A little way outside it there is only its glow, space and the odd star.
            Assert.Less(ShareOfRingBrighterThan(toMoon, PlanetRadius * 1.3f, 90), 0.03f,
                "Something bright stands just outside where the moon should end." + where);
        }

        [Test]
        public void ThePlanet_IsInsideTheFrame()
        {
            // The camera looks level through a 65 degree lens, so the frame
            // ends 32.5 degrees up. A planet that reaches past that is cut
            // off by the top of the screen from every side.
            float top = Mathf.Asin(ToTheMoon().y) * Mathf.Rad2Deg + PlanetRadius;
            Assert.Less(top, 32.5f, "The planet's top is " + top.ToString("0.0") + " degrees up, over the top of the frame.");
        }

        /// <summary>
        /// The four faces round the horizon, which are all the camera ever
        /// shows. Every other texel each way is plenty.
        /// </summary>
        [Test]
        public void TheSky_IsDark_AndNothingInItIsWarm()
        {
            int face = sheet.width / 4;
            long count = 0, sum = 0, lit = 0, warm = 0;

            for (int y = face; y < 2 * face; y += 2)
            {
                for (int x = 0; x < sheet.width; x += 2)
                {
                    Color32 texel = Texel(x, y);
                    count++;
                    sum += texel.r + texel.g + texel.b;

                    if (Brightest(texel) > 40)
                    {
                        lit++;
                        // Straw is as warm as a star gets: red a little over green, and never far over blue.
                        if (texel.r > texel.g + 20 || texel.r > texel.b + 60)
                        {
                            warm++;
                        }
                    }
                }
            }

            float mean = sum / (3f * count);
            Assert.Less(mean, 30f, "The sky averages " + mean.ToString("0.0") +
                                   " of 255. The old sheet's fault was that cloud did not read against it.");
            Assert.Greater(lit, count / 500, "Hardly anything in the sky is lit: the stars, the band or the planet are missing.");
            Assert.AreEqual(0, warm, warm + " lit texels are orange or red, which are the lava's colours.");
        }

        [Test]
        public void TheSheet_IsImportedLikeTheOneItReplaced()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SkyPath);
            Assert.AreEqual(TextureImporterShape.TextureCube, importer.textureShape);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.IsTrue(importer.mipmapEnabled);
            // BC7. The plain setting picks BC1, which turns dark gradients into blocks.
            Assert.AreEqual(TextureImporterCompression.CompressedHQ, importer.textureCompression);

            Cubemap sky = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyPath);
            Assert.IsNotNull(sky, SkyPath + " did not import as a cubemap.");
            Assert.AreEqual(sheet.width / 4, sky.width, "The faces are being resized on import.");

            Cubemap old = AssetDatabase.LoadAssetAtPath<Cubemap>(OldSkyPath);
            Assert.IsNotNull(old, OldSkyPath + " is the only copy of the art the game shipped with before this. It should still be here.");
            Assert.AreEqual(old.width, sky.width);
            Assert.AreEqual(old.format, sky.format);
            Assert.AreEqual(old.mipmapCount, sky.mipmapCount);
        }
    }
}
