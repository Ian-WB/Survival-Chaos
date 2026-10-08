using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// How a clip is measured and brought to the common level: what the two
    /// sound builders, BuildMomentSounds and BuildPlayerShotVariants, share.
    ///
    /// This was a tool of its own until 8 October 2026: Balance Sound Levels
    /// read every clip a SoundDefinition named, wrote a levelled copy to
    /// Audio/SFX/Balanced, and set every definition's volume from a table.
    /// The clips and the volumes it made are what the game ships. It went
    /// because it no longer gave them back: run again on its own output it
    /// moved 27 of the files, by up to 0.4 dB, and 15 of the volumes, some of
    /// which have been set by hand since. What is left is the arithmetic.
    ///
    /// The two halves it kept apart still are. A file carries the matching:
    /// equal gated RMS, peak-limited so nothing clips. A definition's volume
    /// carries only intent: how much quieter a sound should be because of how
    /// often it fires, which is a design decision and belongs somewhere a
    /// person can read it.
    /// </summary>
    public static class BalanceSoundLevels
    {
        /// <summary>
        /// Target loudness for every effect, as RMS below full scale.
        ///
        /// -16 dBFS leaves room for several sounds at once without the mix
        /// clipping, which this game reaches routinely - a volley landing while
        /// two enemies explode is an ordinary second of play.
        /// </summary>
        internal const float TargetRmsDb = -16f;

        /// <summary>Nothing is allowed to peak nearer than this to full scale.</summary>
        internal const float PeakCeilingDb = -0.5f;

        /// <summary>Below this, a sample counts as silence and is left out of the average.</summary>
        private const float GateDb = -60f;

        /// <summary>
        /// How much quieter each of the nine sounds BuildMomentSounds makes
        /// should be than Player Hit, in dB, and why. Player Hit is the anchor
        /// at 0 because volume cannot exceed 1 and it is given all of it.
        ///
        /// Every other sound's level is on its SoundDefinition and nowhere
        /// else: the rest of this table went with the tool that applied it.
        /// </summary>
        private static readonly Dictionary<string, float> IntentDb = new Dictionary<string, float>
        {
            // Rare and big: an act ending, the Leviathan arriving. The horn is
            // long and sustained, which reads louder than its RMS says, so it
            // sits under the deaths; the distant one is quieter again, and
            // darker and echoing in the clip itself.
            { "ActEnd", -3f },
            { "BossHornArrival", -6f },
            { "BossHornDistant", -10f },

            // Something struck the ship and was stopped: a little under the hit
            // it stands in for.
            { "DeflectorBlock", -3f },

            // Once a slowdown each, and the salvage as often as it drops. Near
            // Skill Picked, the other collection sound.
            { "SalvagePicked", -6f },
            { "SlowMoStart", -6f },
            { "SlowMoEnd", -6f },

            // Repeat for as long as they apply - three ticks a warning, a beat
            // every 0.9 s on the last hit point - so they stay underneath.
            { "OfferExpiring", -10f },
            { "LowHealth", -12f }
        };

        /// <summary>The level a sound is meant to sit at, in dB against Player Hit. 0 when unlisted.</summary>
        internal static float IntentFor(string name)
        {
            return IntentDb.TryGetValue(name, out float db) ? db : 0f;
        }

        // ---------- measurement ----------

        /// <summary>
        /// RMS ignoring near-silence.
        ///
        /// A plain average over the whole file makes anything with a long tail
        /// read as quiet, which is most explosions - the exact sounds being
        /// complained about.
        /// </summary>
        internal static float GatedRms(float[] samples)
        {
            float gate = FromDb(GateDb);
            double sum = 0;
            int counted = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                float a = Mathf.Abs(samples[i]);
                if (a >= gate)
                {
                    sum += samples[i] * (double)samples[i];
                    counted++;
                }
            }

            return counted == 0 ? 0f : (float)Math.Sqrt(sum / counted);
        }

        internal static float Peak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            return peak;
        }

        internal static float FromDb(float db) => Mathf.Pow(10f, db / 20f);

        internal static float ToDb(float linear) => linear <= 0f ? -144f : 20f * Mathf.Log10(linear);

        /// <summary>
        /// Reads a clip's samples, decompressing it first if it is not already.
        ///
        /// GetData returns silence for a streaming or still-compressed clip rather
        /// than failing, so the import settings are forced and restored around the
        /// read - measuring zeros and calling it quiet would be the worst possible
        /// outcome here.
        /// </summary>
        internal static bool TryRead(AudioClip clip, out float[] samples)
        {
            samples = null;

            string path = AssetDatabase.GetAssetPath(clip);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                return false;
            }

            AudioImporterSampleSettings original = importer.defaultSampleSettings;
            bool changed = original.loadType != AudioClipLoadType.DecompressOnLoad;

            if (changed)
            {
                AudioImporterSampleSettings temp = original;
                temp.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = temp;
                importer.SaveAndReimport();
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            bool ok = false;

            if (clip != null && clip.samples > 0)
            {
                float[] data = new float[clip.samples * clip.channels];
                ok = clip.GetData(data, 0);
                if (ok)
                {
                    samples = data;
                }
            }

            if (changed)
            {
                importer.defaultSampleSettings = original;
                importer.SaveAndReimport();
            }

            return ok;
        }

        // ---------- asset plumbing ----------

        internal static void ApplyImportSettings(string path, bool forceMono)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            importer.forceToMono = forceMono;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
