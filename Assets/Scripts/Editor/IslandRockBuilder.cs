using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Puts the island's four rock materials on the rock shader (environment
    /// roadmap, item 43) and paints the two sheets it reads. What the shader
    /// does with them is in Assets/Art/Shaders/IslandRock.hlsl.
    ///
    /// **The detail sheet** repeats, and is laid on the rock from three
    /// sides, every <see cref="TileEvery"/> units. Red and green are the
    /// slope of a relief made of slow lumps and a net of cracks; blue is how
    /// deep in a crack a point is; alpha is a slow light-and-dark. It is
    /// imported as plain numbers, not as a normal map, so that all four
    /// channels are kept.
    ///
    /// **The heat map** is the island from above, <see cref="HeatSpan"/>
    /// units across with the axis in the middle: white on the lava, fading
    /// to black <see cref="HeatReach"/> units from it. It is painted from
    /// the lava's own mesh in the open scene, so move the lava and build
    /// again.
    ///
    /// **The materials keep their colours.** ilha, pedra, METADE CIMA and
    /// ponta were one flat colour each, and each is that colour's rock now:
    /// the shader takes it as its tint. The trees and the ruins keep their
    /// flat materials.
    ///
    /// The rock's colour changed, so the light it bounces did: the lighting
    /// wants baking again after this.
    /// </summary>
    public static class IslandRockBuilder
    {
        public const string ShaderPath = "Assets/Art/Shaders/IslandRock.shadergraph";
        public const string DetailPath = "Assets/Art/Textures/IslandRockDetail.png";
        public const string HeatPath = "Assets/Art/Textures/IslandHeat.png";
        private const string MaterialFolder = "Assets/Art/Materials/Scenario/";
        private const string LavaName = "ilha lava";

        /// <summary>The four materials, each with the flat colour it had until 8 October.</summary>
        private static readonly (string name, Color tint)[] Rock =
        {
            ("ilha", new Color(0.313f, 0.171f, 0.179f)),
            ("pedra", new Color(0.153f, 0.086f, 0.078f)),
            ("METADE CIMA", new Color(0.493f, 0.184f, 0.188f)),
            ("ponta", new Color(0.513f, 0.334f, 0.213f))
        };

        private const int DetailSize = 512;
        private const int HeatSize = 256;

        /// <summary>The detail sheet repeats this often, in units. A crack's cell is about a fifth of it.</summary>
        public const float TileEvery = 8f;

        /// <summary>How much ground the heat map covers, edge to edge, centred on the axis.</summary>
        public const float HeatSpan = 48f;

        /// <summary>How far from the lava a crack still glows, at rest.</summary>
        private const float HeatReach = 2.5f;

        /// <summary>How bright a crack glows at rest, against the lava's own 6. At 2, on every crack, the cone wore a net of light.</summary>
        private const float Glow = 1f;

        private const int CrackCells = 5;
        private const int FineCells = 11;

        [MenuItem("Survival Chaos/Build Island Rock", priority = 61)]
        public static void Build()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            GameObject lava = GameObject.Find(LavaName);

            if (shader == null)
            {
                Debug.LogError("Island rock: " + ShaderPath + " is missing.");
                return;
            }

            if (lava == null || !lava.TryGetComponent(out MeshFilter lavaMesh) || lavaMesh.sharedMesh == null)
            {
                Debug.LogError("Island rock: there is no " + LavaName + " in the open scene. Open the Game scene.");
                return;
            }

            Texture2D detail = Save(DetailPath, PaintDetail(), DetailSize, TextureWrapMode.Repeat, true);
            Texture2D heat = Save(HeatPath, PaintHeat(lavaMesh.sharedMesh, lava.transform), HeatSize, TextureWrapMode.Clamp, false);

            foreach ((string name, Color tint) in Rock)
            {
                string path = MaterialFolder + name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    Debug.LogError("Island rock: " + path + " is missing.");
                    continue;
                }

                material.shader = shader;
                material.SetColor("_Tint", tint);
                material.SetTexture("_Detail", detail);
                material.SetTexture("_Heat", heat);
                material.SetVector("_HeatBox", new Vector4(0f, 0f, HeatSpan, 0f));
                material.SetFloat("_Tile", 1f / TileEvery);
                material.SetFloat("_Glow", Glow);
                // The cracks' glow comes and goes with the eruption, so it is kept out of the bake.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            Debug.Log("Island rock built: " + Rock.Length + " materials on " + ShaderPath + ". Bake the lighting again: the rock's colour changed.");
        }

        private static Texture2D Save(string path, Color[] pixels, int size, TextureWrapMode wrap, bool alpha)
        {
            Texture2D sheet = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            sheet.SetPixels(pixels);
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            // Numbers, not colours: nothing here may be bent by a colour curve.
            importer.sRGBTexture = false;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The detail sheet.

        private static Color[] PaintDetail()
        {
            int size = DetailSize;
            float[] height = new float[size * size];
            float[] crack = new float[size * size];
            float[] tone = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    int i = y * size + x;

                    float lumps = Clouds(u, v, 4, 4, 11);
                    float wide = 1f - Ramp(0.015f, 0.075f, Edge(u, v, CrackCells, 23));
                    float fine = 1f - Ramp(0.02f, 0.07f, Edge(u, v, FineCells, 57));
                    crack[i] = Mathf.Max(wide, fine * 0.55f);
                    tone[i] = Clouds(u, v, 2, 3, 91);
                    // The cracks are cut into the lumps.
                    height[i] = lumps * 0.7f - crack[i] * 0.45f;
                }
            }

            Color[] pixels = new Color[size * size];
            // One texel's rise as a slope, with the sheet's size taken out so a bigger sheet is not a steeper one.
            float steep = size / 48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float east = height[y * size + (x + 1) % size] - height[y * size + (x + size - 1) % size];
                    float north = height[((y + 1) % size) * size + x] - height[((y + size - 1) % size) * size + x];
                    int i = y * size + x;

                    pixels[i] = new Color(
                        Mathf.Clamp01(0.5f - east * steep * 0.5f),
                        Mathf.Clamp01(0.5f - north * steep * 0.5f),
                        crack[i],
                        tone[i]);
                }
            }

            return pixels;
        }

        /// <summary>Soft noise that repeats across the sheet: <paramref name="octaves"/> layers, the first with <paramref name="cells"/> lumps across.</summary>
        private static float Clouds(float u, float v, int cells, int octaves, int seed)
        {
            float sum = 0f;
            float weight = 0f;
            float strength = 1f;

            for (int o = 0; o < octaves; o++)
            {
                sum += strength * Lumps(u * cells, v * cells, cells, seed + o * 17);
                weight += strength;
                strength *= 0.5f;
                cells *= 2;
            }

            return sum / weight;
        }

        private static float Lumps(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Dice(x0, y0, period, seed);
            float b = Dice(x0 + 1, y0, period, seed);
            float c = Dice(x0, y0 + 1, period, seed);
            float d = Dice(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>
        /// How far a point is from the nearest wall between two cells of a
        /// net that repeats across the sheet, in cells: 0 on a wall. The
        /// walls are the cracks.
        /// </summary>
        private static float Edge(float u, float v, int cells, int seed)
        {
            float x = u * cells;
            float y = v * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float nearest = float.MaxValue;
            float second = float.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ix = cx + dx;
                    int iy = cy + dy;
                    float px = ix + 0.15f + 0.7f * Dice(ix, iy, cells, seed);
                    float py = iy + 0.15f + 0.7f * Dice(ix, iy, cells, seed + 1);
                    float away = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));

                    if (away < nearest)
                    {
                        second = nearest;
                        nearest = away;
                    }
                    else if (away < second)
                    {
                        second = away;
                    }
                }
            }

            return second - nearest;
        }

        /// <summary>A number from 0 to 1 for a lattice point, the same every time, and the same one <paramref name="period"/> points along.</summary>
        private static float Dice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;

            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519u);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float Ramp(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        // The heat map.

        private static Color[] PaintHeat(Mesh lava, Transform place)
        {
            int size = HeatSize;
            float perUnit = size / HeatSpan;
            float[] away = new float[size * size];

            for (int i = 0; i < away.Length; i++)
            {
                away[i] = float.MaxValue;
            }

            Vector3[] corners = lava.vertices;
            int[] faces = lava.triangles;
            Vector2[] flat = new Vector2[corners.Length];

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 world = place.TransformPoint(corners[i]);
                // In texels, with the axis at the sheet's middle.
                flat[i] = new Vector2(world.x * perUnit + size * 0.5f, world.z * perUnit + size * 0.5f);
            }

            // Every texel the lava lies over, seen from above.
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector2 a = flat[faces[f]];
                Vector2 b = flat[faces[f + 1]];
                Vector2 c = flat[faces[f + 2]];
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1);
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1);
                int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (Within(new Vector2(x + 0.5f, y + 0.5f), a, b, c))
                        {
                            away[y * size + x] = 0f;
                        }
                    }
                }
            }

            // How far every other texel is from one of those: two sweeps, each passing the distance on.
            const float straight = 1f;
            const float slant = 1.41421356f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float least = away[y * size + x];
                    if (x > 0) least = Mathf.Min(least, away[y * size + x - 1] + straight);
                    if (y > 0) least = Mathf.Min(least, away[(y - 1) * size + x] + straight);
                    if (x > 0 && y > 0) least = Mathf.Min(least, away[(y - 1) * size + x - 1] + slant);
                    if (x < size - 1 && y > 0) least = Mathf.Min(least, away[(y - 1) * size + x + 1] + slant);
                    away[y * size + x] = least;
                }
            }

            for (int y = size - 1; y >= 0; y--)
            {
                for (int x = size - 1; x >= 0; x--)
                {
                    float least = away[y * size + x];
                    if (x < size - 1) least = Mathf.Min(least, away[y * size + x + 1] + straight);
                    if (y < size - 1) least = Mathf.Min(least, away[(y + 1) * size + x] + straight);
                    if (x < size - 1 && y < size - 1) least = Mathf.Min(least, away[(y + 1) * size + x + 1] + slant);
                    if (x > 0 && y < size - 1) least = Mathf.Min(least, away[(y + 1) * size + x - 1] + slant);
                    away[y * size + x] = least;
                }
            }

            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                float heat = 1f - Ramp(0f, HeatReach, away[i] / perUnit);
                pixels[i] = new Color(heat, heat, heat, 1f);
            }

            return pixels;
        }

        private static bool Within(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = Cross(b - a, p - a);
            float bc = Cross(c - b, p - b);
            float ca = Cross(a - c, p - c);
            return (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
