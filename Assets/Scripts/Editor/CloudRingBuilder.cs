using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The clouds as an eye of the storm: the two textures that say where
    /// cloud stands, the settings on the Game scene's volume profile that
    /// use them, and the cloud tracers that make the storm turn.
    ///
    /// Environment roadmap, item 40, 5 October 2026. The clouds were HDRP's
    /// Simple preset, one layer seven kilometres deep with the arena inside
    /// it: cloud on every side and overhead, and the stars only through gaps.
    /// Now a painted map holds a clear eye over the island, a wall of cloud
    /// round it behind the lane, a floor of cloud under it, and broken
    /// cloud high over it, and all of it circles the island once a minute.
    ///
    /// It took three builds. The first had a low wall, an empty sky over the
    /// eye and a slow straight wind. Ian found the scene had stopped going
    /// dark, then that the clouds were too slow and that it did not look
    /// like an eye of the storm.
    ///
    /// **The cloud over the eye is there for the light.** The old layer
    /// crossed the moon and took the island's moonlit rock down to a third
    /// of its brightness for up to half a minute at a time, which nobody had
    /// written down as a feature. So a broken deck rides over the eye, high
    /// enough that nothing flies in it, and crosses the moon as the storm
    /// turns. Measured on the same rock over two and a half minutes, it
    /// swings between 12 and 39, as the old clouds did; with no deck it sat
    /// at 39.
    ///
    /// That was with the moon 40 degrees up. Ian lowered it to 15 on
    /// 6 October, and at 15 the light on the island hardly moves, with this
    /// wall or a taller one: the moon gives the island little at that angle,
    /// and what shades it is no longer the deck but the wall, which is
    /// always there. Tried in play, brief dark spells come back with the moon
    /// at about 25 and there are more of them at 32.
    ///
    /// **The storm turns, which HDRP's wind cannot do.** Its wind pushes the
    /// clouds in a straight line. The project carries copies of HDRP's two
    /// cloud tracers and the include they share, where every point is
    /// turned about the island before anything is read
    /// (Assets/Art/Shaders/VolumetricCloudsUtilities.hlsl says how), and this
    /// builder points the pipeline at them. The scene's wind is then the
    /// speed of the cloud <see cref="TurnRadius"/> out, and
    /// <see cref="TurnSeconds"/> is the number to change.
    ///
    /// **The wall is low and steep.** Ian picked the low wall from the
    /// test's stills. The third build raised it, the layer 2000 deep, so
    /// that it would read as a storm; once it turned he had it lowered
    /// again. It reaches its height within 500 units, where the first
    /// build's took 700.
    ///
    /// **The map** is seen from above, <see cref="Span"/> across, centred on
    /// the island. HDRP centres it over the middle of the planet, and the
    /// scene's Visual Environment puts that straight under the island. Red is
    /// cover, and is full everywhere, because the floor is everywhere. Green
    /// is how heavy the cloud is, and is full inside the eye, so the deck
    /// throws a shadow worth the name. Blue is the kind of cloud, from 0, the
    /// eye's floor and deck, to 1, the wall at its tallest. Alpha
    /// is a ceiling HDRP would cut the cloud off at; it is left at 1, because
    /// a cut is flat, and the lookup shapes the tops instead. Outside the map
    /// the edge repeats, so the wall runs to the horizon.
    ///
    /// **The lookup** is read across by kind and up by height in the layer.
    /// Its red is not a density: it is how much of the shape noise counts as
    /// cloud, and at 0 there is none. So each kind is a column that is full
    /// from just over the layer's bottom and thins out to nothing at that
    /// kind's top, and the eye's column has the deck as a second band over
    /// the gap the ships fly in.
    ///
    /// The wind's straight push is nearly switched off: none for the map,
    /// a tenth for the shapes and a quarter for the fine detail. That little
    /// is what stops each turn of the storm being the last one over again.
    ///
    /// It is not a saving. On a frozen frame in the editor the old clouds
    /// cost 2.1 ms on the High row and 0.9 on Low; this costs 3.4 and 1.1.
    ///
    /// Re-running repaints both textures, puts every setting named here
    /// back and points the pipeline at the project's tracers again. Anything
    /// else on the clouds - the step counts, the shadows, the density - is
    /// left as the scene has it.
    /// </summary>
    public static class CloudRingBuilder
    {
        public const string MapPath = "Assets/Art/Textures/CloudRingMap.png";
        public const string LookupPath = "Assets/Art/Textures/CloudRingLookup.png";
        public const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";
        public const string TracerPath = "Assets/Art/Shaders/VolumetricCloudsTrace.compute";
        public const string ShadowTracerPath = "Assets/Art/Shaders/VolumetricCloudsTraceShadows.compute";

        /// <summary>HDRP keeps its cloud shaders in a settings class it does not show, so they are reached by name.</summary>
        private const string ResourcesType =
            "UnityEngine.Rendering.HighDefinition.VolumetricCloudsRuntimeResources, " +
            "Unity.RenderPipelines.HighDefinition.Runtime";

        // The layer, in altitude over the planet's surface. The arena is at
        // 1000: see the Visual Environment's planet centre.

        /// <summary>The floor's underside.</summary>
        public const float Bottom = 250f;

        /// <summary>
        /// From there to the top of the tallest wall: 750 over the arena, the
        /// low wall. At 2000 it stands between fifteen and thirty degrees
        /// high from the camera; the shares below then have to move with it.
        /// </summary>
        public const float Range = 1500f;

        /// <summary>The floor's top, as a share of the layer: 250 under the arena.</summary>
        private const float FloorTop = 1f / 3f;

        /// <summary>
        /// The deck over the eye, as shares of the layer: from 240 over the
        /// arena to the layer's top, fading in and out over a twentieth of
        /// the layer at each end. With the moon 40 degrees up the cloud that
        /// shades the island is 290 to 900 out, inside the eye, which is
        /// where the deck is. With the moon at 15 it is 900 to 2800 out,
        /// which is the wall.
        /// </summary>
        private const float DeckFrom = 0.66f;
        private const float DeckTo = 1f;

        /// <summary>
        /// How much of the shape noise the deck keeps. At 0.6 on a thinner
        /// band the rock never fell below 33; this is what takes it to 13.
        /// </summary>
        private const float Deck = 1f;

        // The map.

        /// <summary>How much ground the map covers, edge to edge.</summary>
        public const float Span = 16000f;

        /// <summary>How far out the wall starts, give or take <see cref="EyeWander"/> by bearing.</summary>
        private const float Eye = 1100f;
        private const float EyeWander = 0.18f;

        /// <summary>How far the wall takes to reach its height. The first build had 700, a gentler face.</summary>
        private const float Rim = 500f;

        private const int MapSize = 512;
        private const int LookupWidth = 64;
        private const int LookupHeight = 128;

        // The look. The old layer's shape scale, 5, makes features kilometres
        // across: right seen across seven kilometres, a fog bank on a wall
        // this close.
        private const float ShapeScale = 30f;
        private const float ErosionScale = 250f;

        /// <summary>
        /// How long the storm takes to go round the island. Ian found a
        /// straight wind of 60 km/h too slow, then 200. This is 565 at the
        /// wall and turns the sky six degrees a second.
        /// </summary>
        public const float TurnSeconds = 60f;

        /// <summary>
        /// STORM_TURN_RADIUS in the project's VolumetricCloudsUtilities.hlsl:
        /// how far out the cloud moves at the wind's own speed. The two have
        /// to agree, and CloudRingTests reads the shader's to check.
        /// </summary>
        public const float TurnRadius = 1500f;

        /// <summary>The scene's Global Wind Speed, in km/h as HDRP counts it.</summary>
        public static float Wind => 2f * Mathf.PI * TurnRadius / TurnSeconds * 3.6f;

        /// <summary>The shares of the wind's straight push the shapes and the fine detail still take.</summary>
        private const float ShapeDrift = 0.1f;
        private const float DetailDrift = 0.25f;

        /// <summary>
        /// The clouds start this close to the camera and are whole this much
        /// further out. The old layer needed a bubble kept clear; here the
        /// map keeps the eye clear and nothing is near enough to fade.
        /// </summary>
        private const float FadeStart = 0f;
        private const float FadeDistance = 50f;

        /// <summary>HDRP's own, in VolumetricClouds: it scales the map by this and the layer's middle.</summary>
        private const float PlanetRadius = 6378100f;

        // Where the wall starts and how tall it stands, by bearing: a few
        // waves round the compass each, so the eye is not a circle and the
        // top is not a line. The phases are the ones the test was shown with.
        private static readonly Vector2[] EdgeWaves =
        {
            new Vector2(2f, 2.8815f), new Vector2(3f, 5.5158f), new Vector2(5f, 0.2001f), new Vector2(8f, 1.7745f)
        };

        private static readonly Vector2[] CrownWaves =
        {
            new Vector2(1f, 2.394f), new Vector2(3f, 1.4497f), new Vector2(4f, 1.0432f), new Vector2(7f, 5.7418f),
            new Vector2(11f, 3.6313f)
        };

        /// <summary>
        /// What Cloud Tiling has to be for the map to cover <paramref name="span"/>
        /// metres. HDRP stretches the map over a distance that depends on
        /// where the layer is, so this changes with the layer.
        /// </summary>
        public static float Tiling(float bottom, float range, float span)
        {
            float middle = bottom + range * 0.5f;
            float stretched = Mathf.Sqrt((PlanetRadius + middle) * (PlanetRadius + middle) - PlanetRadius * PlanetRadius);
            return stretched / span;
        }

        /// <summary>How far into the wall a point is, in metres from the island: 0 in the eye, 1 once the wall is whole.</summary>
        public static float Wall(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            float start = Eye * (1f + EyeWander * Waves(EdgeWaves, Mathf.Atan2(z, x)));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, start + Rim, distance));
        }

        /// <summary>The kind of cloud over a point, in metres from the island: 0 the eye, up to 1 wall.</summary>
        public static float Kind(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            float bearing = Mathf.Atan2(z, x);
            float wall = Wall(x, z);

            float crown = Waves(CrownWaves, bearing);
            float tall = 0.82f + 0.18f * crown;
            // A second, lower shelf further out, so the far wall is not one height.
            tall *= 0.9f + 0.1f * Mathf.Sin(distance / 900f + 3f * Waves(CrownWaves, bearing + 1f));

            return wall * tall;
        }

        /// <summary>How much of the shape noise is cloud, for a kind of cloud at a height in the layer, both 0 to 1.</summary>
        public static float Fill(float kind, float height)
        {
            float top = Mathf.Lerp(FloorTop, 1f, kind);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.04f, height));
            // The floor thins out from just over half its height, which
            // leaves it soft on top; the wall holds to four fifths, so it
            // stands up.
            float thinsFrom = top * (0.55f + 0.27f * kind);
            float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(thinsFrom, top, height));

            // The deck belongs to the eye and thins out as the wall takes over.
            float deck = Deck * (1f - kind)
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DeckFrom, DeckFrom + 0.05f, height))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DeckTo - 0.05f, DeckTo, height)));

            return Mathf.Max(rise * fall, deck);
        }

        private static float Waves(Vector2[] waves, float bearing)
        {
            float sum = 0f;
            float weight = 0f;
            foreach (Vector2 wave in waves)
            {
                sum += Mathf.Sin(wave.x * bearing + wave.y) / wave.x;
                weight += 1f / wave.x;
            }

            return sum / weight;
        }

        [MenuItem("Survival Chaos/Build Cloud Ring", priority = 59)]
        public static void Build()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null || !profile.TryGet(out VolumetricClouds clouds))
            {
                Debug.LogError("Cloud ring: " + ProfilePath + " is missing, or has no Volumetric Clouds on it.");
                return;
            }

            Texture2D map = Paint(MapPath, MapSize, MapSize, (u, v) =>
            {
                float x = (u - 0.5f) * Span;
                float z = (v - 0.5f) * Span;
                return new Color(1f, 1f - Wall(x, z), Kind(x, z), 1f);
            });

            Texture2D lookup = Paint(LookupPath, LookupWidth, LookupHeight, (u, v) =>
            {
                // Across, the first and last columns are kinds 0 and 1 exactly.
                float kind = Mathf.InverseLerp(0.5f / LookupWidth, 1f - 0.5f / LookupWidth, u);
                float top = Mathf.Lerp(FloorTop, 1f, kind);
                float shade = 0.4f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, top, v)));
                // Green is how much the detail noise eats; blue how much the cloud shades itself low down.
                return new Color(Fill(kind, v), 0.92f, shade, 1f);
            });

            clouds.cloudControl.Override(VolumetricClouds.CloudControl.Manual);
            clouds.cloudMap.Override(map);
            clouds.cloudLut.Override(lookup);
            float tiling = Tiling(Bottom, Range, Span);
            clouds.cloudTiling.Override(new Vector2(tiling, tiling));
            clouds.cloudOffset.Override(Vector2.zero);
            clouds.bottomAltitude.Override(Bottom);
            clouds.altitudeRange.Override(Range);
            clouds.fadeInMode.Override(VolumetricClouds.CloudFadeInMode.Manual);
            clouds.fadeInStart.Override(FadeStart);
            clouds.fadeInDistance.Override(FadeDistance);
            clouds.cloudMapSpeedMultiplier.Override(0f);
            clouds.shapeSpeedMultiplier.Override(ShapeDrift);
            clouds.erosionSpeedMultiplier.Override(DetailDrift);
            clouds.shapeScale.Override(ShapeScale);
            clouds.erosionScale.Override(ErosionScale);
            clouds.globalWindSpeed.Override(new WindParameter.WindParamaterValue
            {
                mode = WindParameter.WindOverrideMode.Custom,
                customValue = Wind,
                additiveValue = 0f,
                multiplyValue = 1f
            });

            EditorUtility.SetDirty(clouds);
            EditorUtility.SetDirty(profile);
            // Not SaveAssets: that would also write whatever else is being tuned unsaved.
            AssetDatabase.SaveAssetIfDirty(profile);

            bool turning = UseTheProjectsTracers();
            Debug.Log("Cloud ring built: " + MapPath + " and " + LookupPath + ", tiling " + tiling.ToString("0.00") +
                      ", wind " + Wind.ToString("0") + (turning ? ", turning." : ", NOT turning: see the error above."), profile);
        }

        /// <summary>
        /// Points the pipeline's two cloud tracers at the project's copies,
        /// the ones that turn the storm. An HDRP upgrade or a Reset on the
        /// global settings puts the package's back; CloudStormShaderTests
        /// says so when it happens.
        /// </summary>
        private static bool UseTheProjectsTracers()
        {
            ComputeShader tracer = AssetDatabase.LoadAssetAtPath<ComputeShader>(TracerPath);
            ComputeShader shadowTracer = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShadowTracerPath);
            System.Type resources = System.Type.GetType(ResourcesType);
            if (tracer == null || shadowTracer == null || resources == null)
            {
                Debug.LogError("Cloud ring: the project's cloud tracers are missing, or HDRP no longer keeps its own in " +
                               "VolumetricCloudsRuntimeResources. The clouds will drift in a line instead of turning.");
                return false;
            }

            object settings = typeof(GraphicsSettings)
                .GetMethod(nameof(GraphicsSettings.GetRenderPipelineSettings), System.Type.EmptyTypes)
                .MakeGenericMethod(resources)
                .Invoke(null, null);

            resources.GetProperty("volumetricCloudsTraceCS").SetValue(settings, tracer);
            resources.GetProperty("volumetricCloudsTraceShadowsCS").SetValue(settings, shadowTracer);

            // The settings class lives inside the pipeline's global settings asset.
            Object owner = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            if (owner != null)
            {
                EditorUtility.SetDirty(owner);
                AssetDatabase.SaveAssetIfDirty(owner);
            }

            return true;
        }

        /// <summary>
        /// Writes a PNG and imports it as data: linear, uncompressed, no
        /// mips, clamped. The painter is asked for each texel by its centre,
        /// 0 to 1, with v counted up from the bottom row.
        /// </summary>
        private static Texture2D Paint(string path, int width, int height, System.Func<float, float, Color> painter)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = painter((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
