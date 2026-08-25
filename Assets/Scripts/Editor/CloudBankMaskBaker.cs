using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Bakes the 3D noise texture that ArenaCloudBank scrolls through the arena.
    ///
    /// HDRP's volumetric clouds cannot be used for cloud that touches the
    /// island. They refuse to render within roughly twenty units of the camera,
    /// their altitude range has a hard 100m floor, and their noise runs at
    /// kilometre scale - measured across this 37 unit island the field is
    /// uniform, so the sky is either wholly overcast or wholly clear and no
    /// cloud ever drifts past anything. Local Volumetric Fog is the local-scale
    /// equivalent and it takes a Texture3D density mask, which is what this
    /// makes.
    ///
    /// The noise has to tile, because the mask scrolls forever and a seam would
    /// sweep through the arena once per lap. Every octave hashes its lattice
    /// modulo its own period, and the periods are powers of two that divide the
    /// resolution, so opposite faces line up exactly.
    ///
    /// Density goes in the alpha channel - HDRP multiplies fog density by the
    /// mask's alpha and tints albedo by its RGB, so RGB stays white.
    /// </summary>
    public class CloudBankMaskBaker : ScriptableWizard
    {
        private const string OutputPath = "Assets/Art/Textures/CloudBankMask.asset";

        [Tooltip("Texels per side. 64 is 1MB at RGBA32 and is plenty - the mask is " +
                 "stretched over more than a hundred units, so detail beyond this is " +
                 "smaller than a froxel.")]
        [SerializeField]
        private int resolution = 64;

        [Tooltip("Lattice period of the first octave. The largest cloud features come out " +
                 "one over this fraction of a tile, so 4 gives banks a quarter of a tile wide.")]
        [SerializeField]
        private int basePeriod = 4;

        [Tooltip("Octaves of value noise. Each doubles the period, so this is capped by the " +
                 "resolution - past that an octave aliases instead of adding detail.")]
        [SerializeField]
        private int octaves = 4;

        [Tooltip("How much quieter each octave is than the one before.")]
        [SerializeField]
        private float gain = 0.5f;

        [Tooltip("Noise below this is empty sky. This is the knob that decides how often a " +
                 "bank passes: higher means rarer, more separated clouds.")]
        [SerializeField]
        private float coverage = 0.55f;

        [Tooltip("Raises the remaining density to this power. Above 1 thins the edges and " +
                 "keeps the cores, which reads as wisps rather than a slab.")]
        [SerializeField]
        private float edgeFalloff = 1.6f;

        [Tooltip("Fraction of the height faded out at the top and bottom, so banks are " +
                 "layered rather than filling the box floor to ceiling.")]
        [SerializeField]
        private float verticalFeather = 0.2f;

        [SerializeField]
        private int seed = 20260825;

        [MenuItem("Survival Chaos/Bake Cloud Bank Mask")]
        private static void Open()
        {
            DisplayWizard<CloudBankMaskBaker>("Bake Cloud Bank Mask", "Bake");
        }

        private void OnWizardCreate()
        {
            int size = Mathf.Max(16, Mathf.ClosestPowerOfTwo(resolution));
            int period = Mathf.Max(2, Mathf.ClosestPowerOfTwo(basePeriod));

            // An octave whose period passes the resolution has more lattice points
            // than texels, so it is noise rather than detail. Stop before that.
            int usableOctaves = 0;
            for (int p = period; p <= size && usableOctaves < Mathf.Max(1, octaves); p *= 2)
            {
                usableOctaves++;
            }

            var texture = new Texture3D(size, size, size, TextureFormat.RGBA32, true)
            {
                name = "CloudBankMask",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            int count = size * size * size;
            var field = new float[count];
            float lowest = float.MaxValue;
            float highest = float.MinValue;

            // First pass builds the raw field. Four octaves of value noise never
            // reach 0 or 1 - they cluster around the middle - so the field has to
            // be normalised to what it actually spans before the coverage
            // threshold can mean anything.
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var uvw = new Vector3(
                            (x + 0.5f) / size,
                            (y + 0.5f) / size,
                            (z + 0.5f) / size);

                        float noise = Fbm(uvw, period, usableOctaves, gain, seed);
                        field[x + y * size + z * size * size] = noise;

                        lowest = Mathf.Min(lowest, noise);
                        highest = Mathf.Max(highest, noise);
                    }
                }
            }

            var texels = new Color32[count];
            float threshold = Mathf.Clamp01(coverage);
            float feather = Mathf.Clamp(verticalFeather, 0f, 0.49f);
            float falloff = Mathf.Max(0.01f, edgeFalloff);
            int filled = 0;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int index = x + y * size + z * size * size;

                        float normalised = Mathf.InverseLerp(lowest, highest, field[index]);
                        float density = Mathf.InverseLerp(threshold, 1f, normalised);
                        density = Mathf.Pow(density, falloff);
                        density *= Feather((y + 0.5f) / size, feather);
                        density = Mathf.Clamp01(density);

                        if (density > 0.05f)
                        {
                            filled++;
                        }

                        texels[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(density * 255f));
                    }
                }
            }

            texture.SetPixels32(texels);
            texture.Apply(true);

            Save(texture);

            Debug.Log($"Baked {OutputPath} at {size}^3, {usableOctaves} octaves from period {period}. " +
                      $"Raw field spanned {lowest:0.00}-{highest:0.00}; {100f * filled / count:0.0}% of the " +
                      "volume holds cloud. Somewhere near 20% gives banks with real gaps between them - " +
                      "push coverage up for rarer cloud, down for a solid overcast.");
        }

        /// <summary>
        /// Writes over the existing asset rather than replacing it, so the scene's
        /// reference to the mask survives a rebake.
        /// </summary>
        private static void Save(Texture3D texture)
        {
            Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(OutputPath);

            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                Object.DestroyImmediate(texture);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                AssetDatabase.CreateAsset(texture, OutputPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static float Fbm(Vector3 uvw, int basePeriod, int octaves, float gain, int seed)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            int period = basePeriod;

            for (int octave = 0; octave < octaves; octave++)
            {
                var scaled = new Vector3(uvw.x * period, uvw.y * period, uvw.z * period);
                sum += amplitude * ValueNoise(scaled, period, seed + octave * 8191);
                total += amplitude;
                amplitude *= gain;
                period *= 2;
            }

            return total > 0f ? sum / total : 0f;
        }

        private static float ValueNoise(Vector3 point, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(point.x);
            int y0 = Mathf.FloorToInt(point.y);
            int z0 = Mathf.FloorToInt(point.z);

            float fx = Ease(point.x - x0);
            float fy = Ease(point.y - y0);
            float fz = Ease(point.z - z0);

            float c00 = Mathf.Lerp(Lattice(x0, y0, z0, period, seed), Lattice(x0 + 1, y0, z0, period, seed), fx);
            float c10 = Mathf.Lerp(Lattice(x0, y0 + 1, z0, period, seed), Lattice(x0 + 1, y0 + 1, z0, period, seed), fx);
            float c01 = Mathf.Lerp(Lattice(x0, y0, z0 + 1, period, seed), Lattice(x0 + 1, y0, z0 + 1, period, seed), fx);
            float c11 = Mathf.Lerp(Lattice(x0, y0 + 1, z0 + 1, period, seed), Lattice(x0 + 1, y0 + 1, z0 + 1, period, seed), fx);

            return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
        }

        /// <summary>
        /// Hashed lattice value. Wrapping the coordinates by the octave's own period
        /// is what makes the finished texture tile.
        /// </summary>
        private static float Lattice(int x, int y, int z, int period, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h = h * 747796405u + (uint)Wrap(x, period) * 2891336453u;
                h ^= h >> 15;
                h = h * 747796405u + (uint)Wrap(y, period) * 2654435761u;
                h ^= h >> 13;
                h = h * 747796405u + (uint)Wrap(z, period) * 1103515245u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0xFFFFFF;
            }
        }

        private static int Wrap(int value, int period)
        {
            int wrapped = value % period;
            return wrapped < 0 ? wrapped + period : wrapped;
        }

        private static float Ease(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float Feather(float v, float width)
        {
            if (width <= 0f)
            {
                return 1f;
            }

            return Mathf.Min(Band(0f, width, v), Band(1f, 1f - width, v));
        }

        /// <summary>
        /// A GLSL-style smoothstep. Mathf.SmoothStep interpolates between two values
        /// instead, which is not what is wanted here.
        /// </summary>
        private static float Band(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(edge0, edge1, x));
            return t * t * (3f - 2f * t);
        }
    }
}
