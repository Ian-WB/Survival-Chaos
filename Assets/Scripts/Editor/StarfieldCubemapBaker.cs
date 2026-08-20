using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Turns the six starfield face textures into something HDRP's HDRI Sky can
    /// use - it cannot read a 6-sided skybox material.
    ///
    /// Two output modes:
    ///
    ///   CrossTexture   writes one PNG holding all six faces in a horizontal
    ///                  cross and imports it as a cubemap. This is the mode to
    ///                  use. The result is a normal texture asset, so it gets
    ///                  block compression, mipmap streaming and per-platform
    ///                  size overrides.
    ///
    ///   LegacyCubemap  writes a Cubemap asset directly. Simpler, but it stores
    ///                  raw uncompressed pixels with no compression settings -
    ///                  roughly 250MB for this set, against tens of MB for the
    ///                  cross. Kept as a fallback.
    ///
    /// Faces are copied through a RenderTexture, so the sources do not need
    /// their Read/Write flag enabled and can be resampled on the way through.
    /// </summary>
    public class StarfieldCubemapBaker : ScriptableWizard
    {
        public enum OutputMode
        {
            CrossTexture = 0,
            LegacyCubemap = 1
        }

        private const string TextureFolder = "Assets/Art/Skybox/Source";
        private const string OutputFolder = "Assets/Art/Skybox";

        [Tooltip("CrossTexture is strongly preferred - see the class comment for why.")]
        [SerializeField]
        private OutputMode output= OutputMode.CrossTexture;

        [Tooltip("Resolution of each cube face. The sources import at 2048, so above that gains nothing.")]
        [SerializeField]
        private int faceSize= 2048;

        // Both on - a 180 degree rotation per face - is the combination verified
        // correct for this starfield set in LegacyCubemap mode. The cross path
        // is decoded by Unity's importer rather than written face by face, so
        // it may want a different pair; check the seams after the first bake.
        [Tooltip("Mirror each face left-to-right.")]
        [SerializeField]
        private bool flipHorizontally= true;

        [Tooltip("Mirror each face top-to-bottom.")]
        [SerializeField]
        private bool flipVertically= true;

        [SerializeField]
        private bool generateMipmaps= true;

        [MenuItem("Survival Chaos/Bake Starfield Cubemap")]
        private static void Open()
        {
            DisplayWizard<StarfieldCubemapBaker>("Bake Starfield Cubemap", "Bake");
        }

        private void OnWizardCreate()
        {
            int size = Mathf.Max(16, Mathf.ClosestPowerOfTwo(faceSize));

            if (!TryReadFaces(size, out Color[][] faces))
            {
                return;
            }

            if (output == OutputMode.CrossTexture)
            {
                WriteCrossTexture(faces, size);
            }
            else
            {
                WriteLegacyCubemap(faces, size);
            }
        }

        /// <summary>Face order throughout: +X, -X, +Y, -Y, +Z, -Z.</summary>
        private static readonly string[] FaceFiles =
        {
            "Right_Tex", "Left_Tex", "Up_Tex", "Down_Tex", "Front_Tex", "Back_Tex"
        };

        private bool TryReadFaces(int size, out Color[][] faces)
        {
            faces = new Color[6][];

            for (int i = 0; i < FaceFiles.Length; i++)
            {
                string path = $"{TextureFolder}/{FaceFiles[i]}.png";
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                if (source == null)
                {
                    Debug.LogError($"Missing face texture: {path}. Nothing written.");
                    return false;
                }

                faces[i] = Orient(ReadResampled(source, size), size);
            }

            return true;
        }

        /// <summary>
        /// Writes a horizontal cross:
        ///
        ///        +Y
        ///    -X  +Z  +X  -Z
        ///        -Y
        ///
        /// Unity picks the 6-frame layout from the 4:3 aspect ratio. A 6x1 strip
        /// would waste no pixels, but at 2048 a strip is 12288 wide - not a power
        /// of two, so maxTextureSize would clamp it and leave ragged faces. The
        /// cross's empty quarters cost almost nothing once PNG-compressed.
        /// </summary>
        private void WriteCrossTexture(Color[][] faces, int size)
        {
            int width = size * 4;
            int height = size * 3;

            Texture2D cross = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cross.SetPixels(new Color[width * height]);

            // Column, and row counted from the top.
            Place(cross, faces[2], size, column: 1, rowFromTop: 0); // +Y
            Place(cross, faces[1], size, column: 0, rowFromTop: 1); // -X
            Place(cross, faces[4], size, column: 1, rowFromTop: 1); // +Z
            Place(cross, faces[0], size, column: 2, rowFromTop: 1); // +X
            Place(cross, faces[5], size, column: 3, rowFromTop: 1); // -Z
            Place(cross, faces[3], size, column: 1, rowFromTop: 2); // -Y

            cross.Apply();

            string path = $"{OutputFolder}/StarfieldSky.png";
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllBytes(path, cross.EncodeToPNG());
            DestroyImmediate(cross);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.generateCubemap = TextureImporterGenerateCubemap.FullCubemap;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = generateMipmaps;
                importer.wrapMode = TextureWrapMode.Clamp;
                // High quality, not the default Compressed. Normal quality picks
                // BC1, which quantises the nebula's smooth gradients into
                // visible blocks - it looks crunchy. HQ picks BC7, which is
                // built for gradients. It costs twice the VRAM of BC1 and is
                // worth it here; a sky is one texture with nothing but gradients.
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = Mathf.Min(16384, width);
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture>(path);

            long bytes = new FileInfo(path).Length;
            Debug.Log($"Wrote {width}x{height} cubemap cross to {path} ({bytes / (1024 * 1024)} MB on disk), " +
                      "imported as a compressed cubemap. Assign it to the HDRI Sky override, " +
                      "then check the seams before deleting the source PNGs.");
        }

        private static void Place(Texture2D target, Color[] face, int size, int column, int rowFromTop)
        {
            // SetPixels works from the bottom-left, so flip the row index.
            int y = (2 - rowFromTop) * size;
            target.SetPixels(column * size, y, size, size, face);
        }

        private void WriteLegacyCubemap(Color[][] faces, int size)
        {
            TextureCreationFlags flags = generateMipmaps
                ? TextureCreationFlags.MipChain
                : TextureCreationFlags.None;

            // Explicitly sRGB: the TextureFormat overload creates a linear
            // texture, which reads as "Linear" ticked in the inspector and
            // renders far too bright in Linear colour space.
            Cubemap cubemap = new Cubemap(size, GraphicsFormat.R8G8B8A8_SRGB, flags);

            CubemapFace[] order =
            {
                CubemapFace.PositiveX, CubemapFace.NegativeX,
                CubemapFace.PositiveY, CubemapFace.NegativeY,
                CubemapFace.PositiveZ, CubemapFace.NegativeZ
            };

            for (int i = 0; i < order.Length; i++)
            {
                cubemap.SetPixels(faces[i], order[i]);
            }

            cubemap.wrapMode = TextureWrapMode.Clamp;
            cubemap.filterMode = FilterMode.Bilinear;

            // Drops the CPU-side copy, which otherwise doubles the asset size.
            cubemap.Apply(updateMipmaps: generateMipmaps, makeNoLongerReadable: true);

            string path = $"{OutputFolder}/StarfieldCubemap.asset";
            Directory.CreateDirectory(OutputFolder);

            if (AssetDatabase.LoadAssetAtPath<Cubemap>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(cubemap, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            Debug.Log($"Baked {size}x{size} legacy cubemap to {path}.");
        }

        /// <summary>
        /// Copies a texture at an arbitrary size without needing Read/Write on
        /// the source, by rendering it into a temporary RenderTexture.
        /// </summary>
        private static Color[] ReadResampled(Texture2D source, int size)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            RenderTexture previous = RenderTexture.active;

            Graphics.Blit(source, target);
            RenderTexture.active = target;

            Texture2D readable = new Texture2D(size, size, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            readable.Apply();

            Color[] pixels = readable.GetPixels();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            DestroyImmediate(readable);

            return pixels;
        }

        private Color[] Orient(Color[] pixels, int size)
        {
            if (!flipHorizontally && !flipVertically)
            {
                return pixels;
            }

            Color[] result = new Color[pixels.Length];

            for (int y = 0; y < size; y++)
            {
                int sourceY = flipVertically ? size - 1 - y : y;

                for (int x = 0; x < size; x++)
                {
                    int sourceX = flipHorizontally ? size - 1 - x : x;
                    result[y * size + x] = pixels[sourceY * size + sourceX];
                }
            }

            return result;
        }
    }
}
