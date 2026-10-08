using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The sky behind the storm: paints the cubemap the Game scene's HDRI Sky
    /// shows, and points the scene's volume profile at it.
    ///
    /// Environment roadmap, item 41, 6 October 2026. The sheet before this
    /// one, StarfieldSky.png, is a soft grey-blue and pink nebula with few
    /// stars. It has the colour and the billow of the clouds drawn in front
    /// of it, so cloud and space ran together, and its pink is the colour of
    /// lava-lit smoke. This one is the opposite on each count: near-black,
    /// with a dense field of hard stars, one cool band of gas, and a moon
    /// where the moonlight comes from.
    ///
    /// **The moon is bright and nearly full since 7 October.** It was a
    /// planet showing a thin crescent, its dark face towards the island,
    /// which cannot be what lights the scene. Ian asked for the moon to be
    /// the light's source and showed the Witcher's: a pale, nearly full disc
    /// with seas and craters and a glow round it. The surface is NASA's map
    /// of the Moon (<see cref="MoonMapPath"/>), tinted cold, turned so it is
    /// not quite the face over Earth, and lit from a little to one side so
    /// it is a ball and not a plate. The code still calls it the planet.
    ///
    /// **The planet stands where the scene's moon is.** The builder reads the
    /// Game scene's Directional Light when it runs and paints the planet on
    /// that line, so the scene has to be open. Ian turns that light himself;
    /// when he does, run this again. StormSkyTests fails when the two have
    /// come apart.
    ///
    /// **Only the middle of the sheet is ever on screen.** The camera looks
    /// level with a 65 degree lens, so the frame reaches 32 degrees up and
    /// down and no further. The planet, the band's crest and the stars that
    /// matter are all placed inside that. The top and bottom faces are
    /// painted the same way, for the reflections on the ships.
    ///
    /// **The band** is a great circle of gas tilted <see cref="BandTilt"/>
    /// degrees, with its crest on the side of the island away from the
    /// planet, so each side of the lap has one thing in its sky. It is kept
    /// streaky and faint on purpose: anything billowy reads as cloud.
    ///
    /// **Nothing in it is warm.** The lava owns orange and red, the hostile
    /// fire violet, so the stars run from blue-white to a pale straw and the
    /// gas from teal to indigo.
    ///
    /// The sheet is a horizontal cross, six faces of <see cref="FaceSize"/>,
    /// imported as the old one is: a cubemap, sRGB, BC7. So it costs the
    /// frame what the old one did. StarfieldSky.png is left where it is: it
    /// is the only copy of that art. Nothing in the game names it any more,
    /// so it is not built in.
    ///
    /// Re-running repaints the sheet from the numbers here and puts the HDRI
    /// Sky's texture and rotation back, on the scene and on the pipeline's
    /// default profile. Nothing is random between runs: the
    /// stars come from a fixed seed.
    /// </summary>
    public static class StormSkyBuilder
    {
        public const string SkyPath = "Assets/Art/Skybox/StormSky.png";
        public const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";

        /// <summary>
        /// The pipeline's own profile, which every scene starts from. The
        /// menu never draws a sky, but this profile named the old sheet, and
        /// whatever it names is built into the game: the first build with
        /// both skies in it was 33 MB bigger, for a sky nobody sees.
        /// </summary>
        public const string DefaultProfilePath = "Assets/Settings/DefaultSettingsVolumeProfile.asset";
        public const string MoonName = "Directional Light";

        /// <summary>Texels along one face. At 1440p a texel is about a pixel.</summary>
        public const int FaceSize = 2048;

        // Space itself. Linear light, as everything here is until the sheet is written.
        private static readonly Vector3 Base = new Vector3(0.0022f, 0.0030f, 0.0052f);

        // The stars.

        /// <summary>Stars spread evenly over the whole sky, and the extra ones that follow the band.</summary>
        private const int FieldStars = 30000;
        private const int BandStars = 18000;

        /// <summary>The faintest star's brightness at its middle. Each tenfold step up in brightness is about twenty times rarer.</summary>
        private const float FaintestStar = 0.03f;
        private const float StarFalloff = 1.3f;
        private const float BrightestStar = 7f;

        /// <summary>A star's width in texels, as the spread of a bell curve. Bright ones grow instead of clipping.</summary>
        private const float StarWidth = 0.55f;
        private const float WidestStar = 1.7f;
        private const int Seed = 4141;

        // The band of gas.

        /// <summary>How far the band's circle is tipped from the horizon, and so how high its crest stands.</summary>
        public const float BandTilt = 22f;

        /// <summary>Where the crest is, in degrees round from straight away from the planet.</summary>
        private const float BandCrestOffset = 25f;

        /// <summary>The band's half width in degrees, and its brightest.</summary>
        private const float BandWidth = 8f;
        private const float BandPeak = 0.2f;

        private static readonly Vector3 Teal = new Vector3(0.10f, 0.62f, 0.66f);
        private static readonly Vector3 Indigo = new Vector3(0.22f, 0.20f, 0.85f);

        // The moon. The code calls it the planet, which it was until 7 October.

        /// <summary>Half of how wide the moon stands in the sky, in degrees. The frame is 65 tall.</summary>
        public const float PlanetRadius = 12f;

        /// <summary>
        /// NASA's map of the Moon, from the CGI Moon Kit of its Scientific
        /// Visualization Studio (lroc_color_poles_2k.tif): public domain.
        /// It is laid out by longitude and latitude with the middle of the
        /// near side in the middle. Only this builder reads it, so it is not
        /// built into the game.
        /// </summary>
        public const string MoonMapPath = "Assets/Art/Skybox/MoonMap.tif";

        /// <summary>
        /// How far the moon's sun stands off the line from straight behind
        /// the camera, in degrees: 0 is a full moon, flat as a plate, and 90
        /// a half moon.
        /// </summary>
        private const float SunAside = 30f;

        /// <summary>
        /// Which side that sun is on, in degrees round from straight down
        /// towards the right. The shaded edge is opposite, at the upper left,
        /// where the storm's wall does not hide it.
        /// </summary>
        private const float SunTurn = 55f;

        /// <summary>How far the face is turned round the line of sight, in degrees.</summary>
        private const float MoonRoll = -25f;

        /// <summary>How bright the moon's highlands are painted. The sheet tops out at 1.</summary>
        private const float PlanetPeak = 0.55f;
        private static readonly Vector3 PlanetPale = new Vector3(0.80f, 0.93f, 1.00f);
        private static readonly Vector3 NightSide = new Vector3(0.010f, 0.013f, 0.020f);
        private static readonly Vector3 Air = new Vector3(0.30f, 0.75f, 0.90f);

        // The glow round the moon: a tight one at its edge and a wide faint
        // one, in degrees, both gone by HaloReach out from the edge.
        private const float HaloNear = 0.07f;
        private const float HaloNearWidth = 2.5f;
        private const float HaloFar = 0.018f;
        private const float HaloFarWidth = 9f;
        private const float HaloReach = 30f;

        // The six faces, by where each sits in the cross: column, then row
        // counted from the top. Seen from inside, the way the importer reads it:
        //
        //        +Y
        //    -X  +Z  +X  -Z
        //        -Y
        private static readonly Vector2Int[] Cells =
        {
            new Vector2Int(2, 1), new Vector2Int(0, 1), new Vector2Int(1, 0),
            new Vector2Int(1, 2), new Vector2Int(1, 1), new Vector2Int(3, 1)
        };

        /// <summary>
        /// The direction a point on a face looks along. Faces are +X, -X,
        /// +Y, -Y, +Z, -Z; across and up run -1 to 1 over the face as it
        /// lies in the cross.
        /// </summary>
        public static Vector3 Direction(int face, float across, float up)
        {
            switch (face)
            {
                case 0: return new Vector3(1f, up, -across).normalized;
                case 1: return new Vector3(-1f, up, across).normalized;
                case 2: return new Vector3(across, 1f, -up).normalized;
                case 3: return new Vector3(across, -1f, up).normalized;
                case 4: return new Vector3(across, up, 1f).normalized;
                default: return new Vector3(-across, up, -1f).normalized;
            }
        }

        /// <summary>
        /// Where a direction lands on a face, the other way round. Returns
        /// false when the direction points away from the face. Across and up
        /// can come back outside -1 to 1: the point is then off the face's edge.
        /// </summary>
        public static bool Locate(int face, Vector3 direction, out float across, out float up)
        {
            float depth;
            switch (face)
            {
                case 0: depth = direction.x; across = -direction.z; up = direction.y; break;
                case 1: depth = -direction.x; across = direction.z; up = direction.y; break;
                case 2: depth = direction.y; across = direction.x; up = -direction.z; break;
                case 3: depth = -direction.y; across = direction.x; up = direction.z; break;
                case 4: depth = direction.z; across = direction.x; up = direction.y; break;
                default: depth = -direction.z; across = -direction.x; up = direction.y; break;
            }

            if (depth <= 0.2f)
            {
                across = 0f;
                up = 0f;
                return false;
            }

            across /= depth;
            up /= depth;
            return true;
        }

        /// <summary>The line the band's circle is tipped about: every point of the band is square to it.</summary>
        public static Vector3 BandAxis(Vector3 toPlanet)
        {
            Vector3 away = new Vector3(-toPlanet.x, 0f, -toPlanet.z).normalized;
            Vector3 crest = Quaternion.AngleAxis(BandCrestOffset, Vector3.up) * away;
            float tilt = BandTilt * Mathf.Deg2Rad;
            return (Mathf.Cos(tilt) * Vector3.up - Mathf.Sin(tilt) * crest).normalized;
        }

        /// <summary>How much band there is along a direction, 0 to 1, before the gas is broken up.</summary>
        public static float Band(Vector3 direction, Vector3 bandAxis)
        {
            float off = Mathf.Asin(Mathf.Clamp(Vector3.Dot(direction, bandAxis), -1f, 1f)) * Mathf.Rad2Deg / BandWidth;
            return Mathf.Exp(-off * off);
        }

        [MenuItem("Survival Chaos/Build Storm Sky", priority = 60)]
        public static void Build()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null || !profile.TryGet(out HDRISky sky))
            {
                Debug.LogError("Storm sky: " + ProfilePath + " is missing, or has no HDRI Sky on it.");
                return;
            }

            if (!TryFindMoon(out Vector3 toPlanet))
            {
                Debug.LogError("Storm sky: open the Game scene first. The planet goes where its '" + MoonName +
                               "' shines from, and no directional light of that name is loaded.");
                return;
            }

            Paint(SkyPath, FaceSize, toPlanet);

            Cubemap cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyPath);
            sky.hdriSky.Override(cubemap);
            // The sheet is painted in the world's own directions.
            sky.rotation.Override(0f);
            EditorUtility.SetDirty(sky);
            EditorUtility.SetDirty(profile);
            // Not SaveAssets: that would also write whatever else is being tuned unsaved.
            AssetDatabase.SaveAssetIfDirty(profile);

            VolumeProfile fallback = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath);
            if (fallback != null && fallback.TryGet(out HDRISky fallbackSky))
            {
                fallbackSky.hdriSky.Override(cubemap);
                EditorUtility.SetDirty(fallbackSky);
                EditorUtility.SetDirty(fallback);
                AssetDatabase.SaveAssetIfDirty(fallback);
            }
            else
            {
                Debug.LogWarning("Storm sky: " + DefaultProfilePath + " has no HDRI Sky to point at the sheet. " +
                                 "If it still names the old sky, both ship.");
            }

            float height = Mathf.Asin(toPlanet.y) * Mathf.Rad2Deg;
            Debug.Log("Storm sky built: " + SkyPath + ", the planet " + height.ToString("0.0") +
                      " degrees up towards " + toPlanet.ToString("0.00") + ".", cubemap);
        }

        /// <summary>The way to the moon, from the light that stands for it in the open scene.</summary>
        private static bool TryFindMoon(out Vector3 toMoon)
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional && light.name == MoonName)
                {
                    toMoon = -light.transform.forward;
                    return true;
                }
            }

            toMoon = Vector3.forward;
            return false;
        }

        private struct Star
        {
            public Vector3 Direction;
            public Vector3 Colour;
            public float Peak;
            public float Width;
        }

        private static void Paint(string path, int size, Vector3 toPlanet)
        {
            Vector3 bandAxis = BandAxis(toPlanet);
            List<Star> stars = Scatter(bandAxis);

            // The moon's own frame: which way is down and right across its
            // disc, where its sun is, and how its face is turned.
            Vector3 right = Vector3.Cross(Vector3.up, toPlanet).normalized;
            Vector3 down = -Vector3.Cross(toPlanet, right).normalized;
            float turn = SunTurn * Mathf.Deg2Rad;
            Vector3 side = Mathf.Cos(turn) * down + Mathf.Sin(turn) * right;
            float aside = SunAside * Mathf.Deg2Rad;
            Vector3 toSun = (-Mathf.Cos(aside) * toPlanet + Mathf.Sin(aside) * side).normalized;
            Quaternion roll = Quaternion.AngleAxis(MoonRoll, toPlanet);
            Vector3 east = roll * right;
            Vector3 north = roll * -down;
            MoonMap map = MoonMap.Read(MoonMapPath);

            int width = size * 4;
            int height = size * 3;
            Color32[] sheet = new Color32[width * height];
            float[] light = new float[size * size * 3];

            for (int face = 0; face < 6; face++)
            {
                int current = face;
                EditorUtility.DisplayProgressBar("Storm sky", "Painting face " + (face + 1) + " of 6", face / 6f);

                Parallel.For(0, size, y =>
                {
                    float up = (y + 0.5f) / size * 2f - 1f;
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 direction = Direction(current, (x + 0.5f) / size * 2f - 1f, up);
                        Vector3 colour = Base + Gas(direction, bandAxis);
                        int at = (y * size + x) * 3;
                        light[at] = colour.x;
                        light[at + 1] = colour.y;
                        light[at + 2] = colour.z;
                    }
                });

                foreach (Star star in stars)
                {
                    Stamp(light, size, current, star);
                }

                Parallel.For(0, size, y =>
                {
                    float up = (y + 0.5f) / size * 2f - 1f;
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 direction = Direction(current, (x + 0.5f) / size * 2f - 1f, up);
                        int at = (y * size + x) * 3;
                        Vector3 behindIt = new Vector3(light[at], light[at + 1], light[at + 2]);
                        Vector3 colour = Planet(direction, toPlanet, toSun, east, north, map, behindIt);
                        light[at] = colour.x;
                        light[at + 1] = colour.y;
                        light[at + 2] = colour.z;
                    }
                });

                Vector2Int cell = Cells[face];
                int left = cell.x * size;
                int bottom = (2 - cell.y) * size;
                Parallel.For(0, size, y =>
                {
                    for (int x = 0; x < size; x++)
                    {
                        int at = (y * size + x) * 3;
                        // Half a step of noise before rounding, or the dark gradients band.
                        // Empty space gets none: it is one colour, and the grain would treble the file.
                        bool empty = light[at] == Base.x && light[at + 1] == Base.y && light[at + 2] == Base.z;
                        float grain = empty ? 0f : Hash(x + current * 7919, y, 31) - 0.5f;
                        sheet[(bottom + y) * width + left + x] = new Color32(
                            Encode(light[at], grain), Encode(light[at + 1], grain), Encode(light[at + 2], grain), 255);
                    }
                });
            }

            EditorUtility.DisplayProgressBar("Storm sky", "Writing the sheet", 1f);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(sheet);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            EditorUtility.ClearProgressBar();

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.FullCubemap;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            // For a cubemap this is the size of one face.
            importer.maxTextureSize = size;
            // BC7. The plain setting picks BC1, which turns the dark gradients into blocks.
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static byte Encode(float linear, float grain)
        {
            // The sRGB curve, written out: Unity's own is not for worker threads.
            float clamped = Mathf.Clamp01(linear);
            float curved = clamped <= 0.0031308f ? clamped * 12.92f : 1.055f * Mathf.Pow(clamped, 1f / 2.4f) - 0.055f;
            float display = curved * 255f + grain;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(display), 0, 255);
        }

        /// <summary>The gas along a direction: the band, torn into streaks that run its length.</summary>
        private static Vector3 Gas(Vector3 direction, Vector3 bandAxis)
        {
            float band = Band(direction, bandAxis);
            if (band < 0.003f)
            {
                return Vector3.zero;
            }

            // Squeezed across the band and left long along it, so the gas
            // comes out as streaks, not as the round heaps clouds make.
            Vector3 along = direction - bandAxis * Vector3.Dot(direction, bandAxis);
            Vector3 stretched = along * 2.2f + bandAxis * (Vector3.Dot(direction, bandAxis) * 9f);
            float broad = Spread(Fbm(stretched + new Vector3(11.3f, 4.1f, 7.7f), 4));
            float fine = Spread(Fbm(stretched * 3.1f + new Vector3(2.9f, 13.7f, 5.3f), 4));
            float rift = Spread(Fbm(stretched * 1.6f + new Vector3(31.1f, 17.3f, 23.9f), 3));

            float amount = band
                * Ramp(0.15f, 0.85f, broad)
                * (0.35f + 0.65f * fine)
                * (1f - 0.85f * Ramp(0.62f, 0.80f, rift) * band);

            float hue = Spread(Fbm(along * 1.3f + new Vector3(5.5f, 9.1f, 1.7f), 3));
            return Vector3.Lerp(Indigo, Teal, Ramp(0.2f, 0.8f, hue)) * (amount * BandPeak);
        }

        /// <summary>
        /// The moon over whatever is behind it along a direction: a ball
        /// wearing NASA's map, lit from behind the camera and a little to
        /// one side, with a glow round it that fades to nothing.
        /// </summary>
        private static Vector3 Planet(Vector3 direction, Vector3 toPlanet, Vector3 toSun, Vector3 east, Vector3 north,
            MoonMap map, Vector3 behindIt)
        {
            float radius = PlanetRadius * Mathf.Deg2Rad;
            float towards = Vector3.Dot(direction, toPlanet);
            float angle = Mathf.Acos(Mathf.Clamp(towards, -1f, 1f));
            float beyond = (angle - radius) * Mathf.Rad2Deg;
            if (beyond > HaloReach)
            {
                return behindIt;
            }

            // The glow outside the edge. It ends at exactly nothing, so the
            // space past it stays one colour.
            float over = Mathf.Max(0f, beyond);
            float glow = (HaloNear * Mathf.Exp(-over / HaloNearWidth) + HaloFar * Mathf.Exp(-over / HaloFarWidth))
                * (1f - Ramp(HaloReach * 0.6f, HaloReach, over));
            Vector3 result = behindIt + Air * glow;

            // The ball, with its edge softened over about a texel.
            float soft = 0.03f * Mathf.Deg2Rad;
            float cover = 1f - Ramp(radius - soft, radius + soft, angle);
            if (cover <= 0f)
            {
                return result;
            }

            // Where the line of sight meets a ball one unit away.
            float sine = Mathf.Sin(radius);
            float inside = Mathf.Min(towards, 1f);
            float reach = inside - Mathf.Sqrt(Mathf.Max(0f, inside * inside - (1f - sine * sine)));
            Vector3 normal = (direction * reach - toPlanet).normalized;

            // The real Moon is flat across its lit face and falls away only
            // near the shadow, so the light comes up fast from the dark edge;
            // the rim is taken down a little so it still reads as a ball.
            float lit = Ramp(-0.02f, 0.3f, Vector3.Dot(normal, toSun));
            float facing = Mathf.Clamp01(-Vector3.Dot(normal, direction));
            float rim = 0.75f + 0.25f * Mathf.Sqrt(facing);

            float longitude = Mathf.Atan2(Vector3.Dot(normal, east), -Vector3.Dot(normal, toPlanet));
            float latitude = Mathf.Asin(Mathf.Clamp(Vector3.Dot(normal, north), -1f, 1f));
            Vector3 ball = NightSide + PlanetPale * (map.Sample(longitude, latitude) * lit * rim * PlanetPeak);
            return Vector3.Lerp(result, ball, cover);
        }

        /// <summary>
        /// The moon's map as plain brightness, read once before the painting
        /// starts, because the painting runs on many threads and a texture
        /// can only be read on one. 1 is the highlands; the seas come out
        /// near 0.45 and the brightest craters a little over 1.
        /// </summary>
        private sealed class MoonMap
        {
            private float[] light;
            private int width;
            private int height;

            public static MoonMap Read(string path)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new FileNotFoundException("Storm sky: the moon's map is missing.", path);
                }

                // Readable and as it is on disk. Nothing in the game uses the texture itself.
                if (!importer.isReadable || importer.mipmapEnabled || importer.maxTextureSize < 2048
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.isReadable = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 2048;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.sRGBTexture = true;
                    importer.SaveAndReimport();
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Color32[] texels = texture.GetPixels32();
                MoonMap map = new MoonMap { width = texture.width, height = texture.height, light = new float[texels.Length] };

                int[] counts = new int[1024];
                for (int i = 0; i < texels.Length; i++)
                {
                    float value = 0.2126f * Mathf.GammaToLinearSpace(texels[i].r / 255f)
                        + 0.7152f * Mathf.GammaToLinearSpace(texels[i].g / 255f)
                        + 0.0722f * Mathf.GammaToLinearSpace(texels[i].b / 255f);
                    map.light[i] = value;
                    counts[Mathf.Min(1023, (int)(value * 1024f))]++;
                }

                // The level 95 texels in 100 are under is the highlands.
                int under = 0;
                int level = 0;
                while (level < 1023 && under + counts[level] < texels.Length * 0.95f)
                {
                    under += counts[level];
                    level++;
                }

                float highlands = Mathf.Max(1e-4f, (level + 0.5f) / 1024f);
                for (int i = 0; i < map.light.Length; i++)
                {
                    // Raised a little, or the seas are holes.
                    map.light[i] = Mathf.Pow(Mathf.Min(map.light[i] / highlands, 1.4f), 0.75f);
                }

                return map;
            }

            /// <summary>The brightness at a longitude and latitude on the moon, in radians, east and north positive.</summary>
            public float Sample(float longitude, float latitude)
            {
                float x = (0.5f + longitude / (Mathf.PI * 2f)) * width - 0.5f;
                float y = Mathf.Clamp((0.5f + latitude / Mathf.PI) * height - 0.5f, 0f, height - 1f);
                int x0 = Mathf.FloorToInt(x);
                int y0 = Mathf.Min(Mathf.FloorToInt(y), height - 2);
                float fx = x - x0;
                float fy = y - y0;
                int left = ((x0 % width) + width) % width;
                int rightOf = (left + 1) % width;

                float low = Mathf.Lerp(light[y0 * width + left], light[y0 * width + rightOf], fx);
                float high = Mathf.Lerp(light[(y0 + 1) * width + left], light[(y0 + 1) * width + rightOf], fx);
                return Mathf.Lerp(low, high, fy);
            }
        }

        /// <summary>The stars: an even field, and more along the band. Most are faint; a few are bright enough to spread.</summary>
        private static List<Star> Scatter(Vector3 bandAxis)
        {
            System.Random random = new System.Random(Seed);
            List<Star> stars = new List<Star>(FieldStars + BandStars);

            while (stars.Count < FieldStars + BandStars)
            {
                // Evenly over the ball: an even height and an even turn.
                float y = (float)random.NextDouble() * 2f - 1f;
                float turn = (float)random.NextDouble() * Mathf.PI * 2f;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                Vector3 direction = new Vector3(ring * Mathf.Cos(turn), y, ring * Mathf.Sin(turn));

                bool ofTheBand = stars.Count >= FieldStars;
                float keep = (float)random.NextDouble();
                if (ofTheBand && keep > Band(direction, bandAxis))
                {
                    continue;
                }

                float rarity = Mathf.Max(1e-5f, (float)random.NextDouble());
                float brightness = Mathf.Min(BrightestStar, FaintestStar * Mathf.Pow(rarity, -1f / StarFalloff));
                // The band's extra stars are its dust: none of them bright.
                if (ofTheBand)
                {
                    brightness = Mathf.Min(brightness, 0.35f);
                }

                float warmth = (float)random.NextDouble();
                Vector3 colour = warmth < 0.55f
                    ? Vector3.Lerp(new Vector3(0.72f, 0.84f, 1f), Vector3.one, warmth / 0.55f)
                    : Vector3.Lerp(Vector3.one, new Vector3(1f, 0.94f, 0.80f), (warmth - 0.55f) / 0.45f);

                stars.Add(new Star
                {
                    Direction = direction,
                    Colour = colour,
                    Peak = Mathf.Min(1f, brightness),
                    Width = Mathf.Min(WidestStar, StarWidth * Mathf.Sqrt(Mathf.Max(1f, brightness)))
                });
            }

            return stars;
        }

        /// <summary>Adds one star to a face, if it falls on it or within reach of its edge.</summary>
        private static void Stamp(float[] light, int size, int face, Star star)
        {
            if (!Locate(face, star.Direction, out float across, out float up))
            {
                return;
            }

            // A texel near a corner of a face covers less sky than one in
            // the middle, so the same star needs more of them there.
            float width = star.Width * Mathf.Pow(1f + across * across + up * up, 0.75f);
            int reach = Mathf.CeilToInt(width * 3f);
            float centreX = (across * 0.5f + 0.5f) * size - 0.5f;
            float centreY = (up * 0.5f + 0.5f) * size - 0.5f;
            int fromX = Mathf.Max(0, Mathf.FloorToInt(centreX) - reach);
            int toX = Mathf.Min(size - 1, Mathf.FloorToInt(centreX) + reach + 1);
            int fromY = Mathf.Max(0, Mathf.FloorToInt(centreY) - reach);
            int toY = Mathf.Min(size - 1, Mathf.FloorToInt(centreY) + reach + 1);

            for (int y = fromY; y <= toY; y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    float dx = x - centreX;
                    float dy = y - centreY;
                    float glow = star.Peak * Mathf.Exp(-(dx * dx + dy * dy) / (2f * width * width));
                    int at = (y * size + x) * 3;
                    light[at] += star.Colour.x * glow;
                    light[at + 1] += star.Colour.y * glow;
                    light[at + 2] += star.Colour.z * glow;
                }
            }
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = ((uint)x * 0x9E3779B1u) ^ ((uint)y * 0x85EBCA77u) ^ ((uint)z * 0xC2B2AE3Du);
                h = (h ^ (h >> 15)) * 0x2C1B3C6Du;
                h = (h ^ (h >> 12)) * 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Smooth noise, 0 to 1, the same for the same point every run.</summary>
        private static float Noise(Vector3 point)
        {
            int x = Mathf.FloorToInt(point.x);
            int y = Mathf.FloorToInt(point.y);
            int z = Mathf.FloorToInt(point.z);
            float fx = point.x - x;
            float fy = point.y - y;
            float fz = point.z - z;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float near = Mathf.Lerp(
                Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), fx),
                Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx), fy);
            float far = Mathf.Lerp(
                Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx),
                Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx), fy);
            return Mathf.Lerp(near, far, fz);
        }

        /// <summary>
        /// 0 below <paramref name="from"/>, 1 above <paramref name="to"/>, and a
        /// smooth climb between. Mathf.SmoothStep takes its arguments the
        /// other way round and is not this.
        /// </summary>
        private static float Ramp(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        /// <summary>
        /// Layered noise crowds round a half and seldom reaches either end.
        /// This pulls it out to fill 0 to 1, so there are real gaps and real knots.
        /// </summary>
        private static float Spread(float noise)
        {
            return Mathf.Clamp01((noise - 0.5f) * 2.8f + 0.5f);
        }

        /// <summary>Noise in layers, each twice as fine and half as strong, 0 to 1.</summary>
        private static float Fbm(Vector3 point, int layers)
        {
            float sum = 0f;
            float weight = 0f;
            float strength = 1f;
            for (int layer = 0; layer < layers; layer++)
            {
                sum += Noise(point) * strength;
                weight += strength;
                point = point * 2.03f + new Vector3(19.1f, 7.3f, 13.7f);
                strength *= 0.5f;
            }

            return sum / weight;
        }
    }
}
