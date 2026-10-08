using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Cuts the two player gun clips into eight shorter variants and points
    /// PlayerShot at them.
    ///
    /// The gun was two Bluezone clips of about a second each, picked in August.
    /// Two problems came out of that, both heard in play on 28 September 2026 as
    /// "a bit loud and repetitive":
    ///
    /// Two clips under PickClip's no-repeat rule is not variety, it is a strict
    /// A-B-A-B. And a second of sound on a gun that fires every 0.5 s, faster
    /// with Attack Speed, means the next shot always lands on the last one's tail:
    /// the cannon clip holds -11 to -15 dB for 350 ms and the gun clip sits at
    /// -20 dB until 0.7 s, so three copies overlapped into a wash that never
    /// went quiet between shots.
    ///
    /// Each variant keeps the source's attack untouched - that is the part that
    /// reads as the gun - holds it for a while, then fades it out on a raised
    /// cosine, so every tail is gone by 0.44 s at the latest. Half the variants
    /// also go through a gentle low-pass, which moves the tone without moving
    /// the transient. Two sources times four cuts is eight clips, which the
    /// no-repeat rule shuffles rather than alternates.
    ///
    /// Balanced by BalanceSoundLevels' arithmetic, as every other clip was. The
    /// level against the rest of the mix is the volume on PlayerShot's own
    /// definition. The source clips stay in Balanced, so
    /// pointing PlayerShot back at them is a straight swap.
    /// </summary>
    public static class BuildPlayerShotVariants
    {
        private const string BalancedFolder = "Assets/Audio/SFX/Balanced";
        private const string DefinitionPath = "Assets/Audio/Definitions/PlayerShot.asset";

        private static readonly (string File, string Tag)[] Sources =
        {
            ("1_(1,05s)_Bluezone_BC0295_sci_fi_weapon_cannon_shot_002", "cannon"),
            ("2_(1,00s)_Bluezone_BC0295_sci_fi_weapon_gun_shot_008", "gun"),
        };

        /// <summary>
        /// How each variant is cut: seconds held at full level, seconds of fade
        /// after that, and a low-pass corner in Hz, 0 for none.
        ///
        /// The longest ends at 0.44 s, under the base fire interval, so a gun
        /// without Attack Speed never overlaps itself. With every pick taken the
        /// interval is about 0.33 s and only the fades overlap, well down.
        /// </summary>
        private static readonly (string Tag, float Hold, float Fade, float LowPassHz)[] Cuts =
        {
            ("tight", 0.14f, 0.16f, 0f),
            ("tight_dark", 0.16f, 0.18f, 4500f),
            ("full", 0.22f, 0.20f, 0f),
            ("full_dark", 0.20f, 0.24f, 3200f),
        };

        [MenuItem("Survival Chaos/Audio/Build Player Shot Variants", priority = 104)]
        public static void Build()
        {
            SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(DefinitionPath);
            if (definition == null)
            {
                Debug.LogError($"No SoundDefinition at {DefinitionPath}.");
                return;
            }

            List<AudioClip> built = new List<AudioClip>();
            List<string> report = new List<string>();

            foreach ((string file, string sourceTag) in Sources)
            {
                string sourcePath = $"{BalancedFolder}/{file}.wav";
                AudioClip source = AssetDatabase.LoadAssetAtPath<AudioClip>(sourcePath);

                if (source == null || !BalanceSoundLevels.TryRead(source, out float[] samples))
                {
                    Debug.LogError($"Could not read {sourcePath}; PlayerShot left as it was.");
                    return;
                }

                int channels = source.channels;
                int rate = source.frequency;

                foreach ((string cutTag, float hold, float fade, float lowPassHz) in Cuts)
                {
                    float[] cut = Cut(samples, channels, rate, hold, fade);

                    if (lowPassHz > 0f)
                    {
                        LowPass(cut, channels, rate, lowPassHz);
                    }

                    float gain = Balance(cut);

                    string outPath = $"{BalancedFolder}/playerShot_{sourceTag}_{cutTag}.wav";
                    SfxrSynth.WriteWav(outPath, cut, channels, rate);
                    AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                    BalanceSoundLevels.ApplyImportSettings(outPath, definition.SpatialBlend > 0f);

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
                    if (clip != null)
                    {
                        built.Add(clip);
                    }

                    report.Add($"{outPath}: {cut.Length / channels / (float)rate:F2} s, " +
                               $"{BalanceSoundLevels.ToDb(gain):+0.0;-0.0} dB to balance");
                }
            }

            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty array = serialized.FindProperty("clips");
            array.arraySize = built.Count;

            for (int i = 0; i < built.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = built[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            Debug.Log($"PlayerShot now plays {built.Count} variants:\n  " + string.Join("\n  ", report));
        }

        /// <summary>
        /// The source up to hold + fade seconds, faded out over the last
        /// <paramref name="fade"/> seconds on a raised cosine. The start is
        /// left alone.
        /// </summary>
        private static float[] Cut(float[] source, int channels, int rate, float hold, float fade)
        {
            int frames = source.Length / channels;
            int holdFrames = Mathf.Min(frames, Mathf.RoundToInt(hold * rate));
            int endFrames = Mathf.Min(frames, Mathf.RoundToInt((hold + fade) * rate));
            int fadeFrames = Mathf.Max(1, endFrames - holdFrames);

            float[] cut = new float[endFrames * channels];

            for (int frame = 0; frame < endFrames; frame++)
            {
                float level = 1f;

                if (frame >= holdFrames)
                {
                    float t = (frame - holdFrames) / (float)fadeFrames;
                    level = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI);
                }

                for (int c = 0; c < channels; c++)
                {
                    cut[frame * channels + c] = source[frame * channels + c] * level;
                }
            }

            return cut;
        }

        /// <summary>
        /// Two one-pole low-passes in a row, 12 dB an octave: enough to darken
        /// the body audibly, gentle enough to leave the attack's edge.
        /// </summary>
        private static void LowPass(float[] samples, int channels, int rate, float cornerHz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cornerHz / rate);

            for (int c = 0; c < channels; c++)
            {
                float first = 0f;
                float second = 0f;

                for (int i = c; i < samples.Length; i += channels)
                {
                    first += a * (samples[i] - first);
                    second += a * (first - second);
                    samples[i] = second;
                }
            }
        }

        /// <summary>Equal gated RMS, peak-limited, by BalanceSoundLevels' arithmetic.</summary>
        private static float Balance(float[] samples)
        {
            float rms = BalanceSoundLevels.GatedRms(samples);
            if (rms <= 0f)
            {
                return 1f;
            }

            float gain = BalanceSoundLevels.FromDb(BalanceSoundLevels.TargetRmsDb) / rms;
            float peak = BalanceSoundLevels.Peak(samples);
            float ceiling = BalanceSoundLevels.FromDb(BalanceSoundLevels.PeakCeilingDb);

            if (peak * gain > ceiling)
            {
                gain = ceiling / peak;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            }

            return gain;
        }
    }
}
