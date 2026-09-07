using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Measures how loud every sound actually is and makes them match.
    ///
    /// The levels on the SoundDefinitions were chosen when every clip was
    /// generated and peak-normalised, so they were pure attenuations of a known
    /// starting point. Clips from a pack share no such starting point: the same
    /// 0.7 that was a deliberate step down for one is now most of the reason
    /// another is inaudible. A number tuned against an assumption that has since
    /// changed is worse than no number.
    ///
    /// So loudness is measured rather than guessed, and the two halves are
    /// separated. The files carry the matching - equal gated RMS, peak-limited so
    /// nothing clips. The volume field carries only intent: how much quieter a
    /// sound should be *because of how often it fires*, which is a design decision
    /// and belongs somewhere a person can read it.
    ///
    /// Music is handled the other way round. Rewriting a three-minute track as an
    /// uncompressed copy to correct its level would be tens of megabytes to fix
    /// one number, so the two tracks are balanced by attenuating the louder one on
    /// its asset instead.
    /// </summary>
    public static class BalanceSoundLevels
    {
        private const string DefinitionFolder = "Assets/Audio/Definitions";
        private const string BalancedFolder = "Assets/Audio/SFX/Balanced";

        /// <summary>
        /// Target loudness for every effect, as RMS below full scale.
        ///
        /// -16 dBFS leaves room for several sounds at once without the mix
        /// clipping, which this game reaches routinely - a volley landing while
        /// two enemies explode is an ordinary second of play.
        /// </summary>
        private const float TargetRmsDb = -16f;

        /// <summary>Nothing is allowed to peak nearer than this to full scale.</summary>
        private const float PeakCeilingDb = -0.5f;

        /// <summary>Below this, a sample counts as silence and is left out of the average.</summary>
        private const float GateDb = -60f;

        /// <summary>
        /// How much quieter a sound should be than the rest, in dB, and why.
        ///
        /// This is the only part of levelling that is a judgement rather than a
        /// measurement, which is exactly why it is a short readable table instead
        /// of eleven numbers spread across eleven assets.
        /// </summary>
        /// Rewritten on 2026-09-07 against measurement, because the table above it
        /// does not do what the comment on TargetRmsDb assumes. Gated RMS matches
        /// clips on their average level over everything above the gate, and that
        /// is not what a listener hears: measured over the loudest 300 ms - which
        /// is much closer to perception - two clips both "balanced" to -16 dBFS
        /// came out 13 dB apart. The dash explosion measured -10.5 and the impact
        /// the player takes damage to measured -23.4.
        ///
        /// So these numbers now carry two things at once: how often a sound fires,
        /// and a correction for how far its clip's perceived loudness sits from
        /// its gated RMS. That is not ideal - the honest fix is to match clips on
        /// a windowed loudness in the first place, which would let this table go
        /// back to being purely about frequency - but that rewrites every file in
        /// Balanced, and this does not.
        ///
        /// The ladder is anchored on PlayerHit at 0, because its clip is the
        /// quietest thing here and volume cannot exceed 1. Everything else is
        /// placed relative to it, in dB: deaths +5 and +6, the charges and the
        /// boss's fire a little under, and the sounds that repeat furthest down.
        /// Taking damage now sits above your own gun rather than seven decibels
        /// below it, which is the single thing that was most backwards.
        private static readonly Dictionary<string, float> IntentDb = new Dictionary<string, float>
        {
            // Rare, and the run is over - loud is the point.
            { "PlayerDeath", -5f },
            { "BossDeath", -7.4f },
            { "Victory", -1.3f },

            // The anchor. An impact that has to be felt, from the quietest clip
            // in the set, so it gets everything the volume field can give it.
            { "PlayerHit", 0f },

            // Once a run, or once per level.
            { "LevelUp", -2.3f },
            { "SkillPicked", -4.3f },

            // Warnings. They run for a whole wind-up, so they do not need to be
            // loud to be noticed - they need to be clear of the fire around them.
            { "BossChargeLance", -5.9f },
            { "BossChargeRam", -6f },

            // Constant through the whole fight, and the thing being warned about.
            { "BossShot", -7.6f },

            { "UiClick", -1.8f },

            // Frequent, and often several at once.
            { "EnemyDeath", -9.6f },

            // The two that were most wrong. Both are player-caused, both repeat
            // relentlessly - the dash on a 1.2s cycle - and both were sitting
            // above the sounds that carry threat. Their clips are also the two
            // hottest in the set once measured properly, which is why the numbers
            // are so large.
            { "PlayerShot", -18.3f },
            { "PlayerDash", -20.9f },

            // Fires on every pointer crossing, and must sit well under the click
            // it precedes; it was one decibel above it.
            { "UiHover", -17.8f }
        };

        [MenuItem("Survival Chaos/Balance Sound Levels", priority = 45)]
        public static void Balance()
        {
            Directory.CreateDirectory(BalancedFolder);
            AssetDatabase.Refresh();

            List<string> report = new List<string>();
            List<string> limited = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:SoundDefinition", new[] { DefinitionFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
                if (definition == null || !definition.HasClips)
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);

                if (definition.Channel == AudioChannel.Music)
                {
                    // Measured below, together, since they are balanced against
                    // each other rather than against a fixed target.
                    continue;
                }

                List<AudioClip> rebuilt = new List<AudioClip>();

                foreach (AudioClip clip in ClipsOf(definition))
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    if (!TryRead(clip, out float[] samples))
                    {
                        report.Add($"{name}: could not read {clip.name}, left alone");
                        rebuilt.Add(clip);
                        continue;
                    }

                    float rms = GatedRms(samples);
                    if (rms <= 0f)
                    {
                        report.Add($"{name}: {clip.name} is silent, left alone");
                        rebuilt.Add(clip);
                        continue;
                    }

                    float gain = FromDb(TargetRmsDb) / rms;

                    // Peak limit rather than clip. A sound that cannot reach the
                    // target without distorting is better left below it.
                    float peak = Peak(samples);
                    float ceiling = FromDb(PeakCeilingDb);
                    if (peak * gain > ceiling)
                    {
                        gain = ceiling / peak;
                        limited.Add($"{clip.name} ({ToDb(GatedRms(samples) * gain):F1} dB RMS)");
                    }

                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
                    }

                    string outPath = $"{BalancedFolder}/{clip.name}.wav";
                    SfxrSynth.WriteWav(outPath, samples, clip.channels, clip.frequency);
                    AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                    ApplyImportSettings(outPath, definition.SpatialBlend > 0f);

                    AudioClip balanced = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
                    rebuilt.Add(balanced != null ? balanced : clip);

                    report.Add($"{name}/{clip.name}: {ToDb(rms):F1} -> {TargetRmsDb:F1} dB RMS ({ToDb(gain):+0.0;-0.0} dB)");
                }

                SetClips(definition, rebuilt);
                SetVolume(definition, FromDb(IntentDb.TryGetValue(name, out float db) ? db : 0f));
            }

            BalanceMusic(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Sound levels balanced:\n  " + string.Join("\n  ", report));

            if (limited.Count > 0)
            {
                Debug.LogWarning(
                    "These hit the peak ceiling before reaching the target, so they stay below it. " +
                    "That is correct - they are dynamic rather than quiet - but if one still sounds " +
                    "weak it wants a different clip, not more gain:\n  " + string.Join("\n  ", limited));
            }
        }

        /// <summary>
        /// Brings the two music tracks level with each other.
        ///
        /// Attenuating the louder one is the whole fix: they only have to match,
        /// not hit an absolute target, and the scene's own AudioSource level
        /// already sets where music sits against everything else.
        /// </summary>
        private static void BalanceMusic(List<string> report)
        {
            List<(SoundDefinition definition, string name, float rms)> tracks =
                new List<(SoundDefinition, string, float)>();

            foreach (string guid in AssetDatabase.FindAssets("t:SoundDefinition", new[] { DefinitionFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);

                if (definition == null || definition.Channel != AudioChannel.Music || !definition.HasClips)
                {
                    continue;
                }

                AudioClip clip = null;
                foreach (AudioClip c in ClipsOf(definition))
                {
                    if (c != null) { clip = c; break; }
                }

                if (clip == null)
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);

                if (!TryRead(clip, out float[] samples))
                {
                    report.Add($"{name}: could not read {clip.name}, music left alone");
                    continue;
                }

                float rms = GatedRms(samples);
                if (rms > 0f)
                {
                    tracks.Add((definition, name, rms));
                }
            }

            if (tracks.Count < 2)
            {
                return;
            }

            float quietest = float.MaxValue;
            foreach ((SoundDefinition _, string _, float rms) in tracks)
            {
                quietest = Mathf.Min(quietest, rms);
            }

            foreach ((SoundDefinition definition, string name, float rms) in tracks)
            {
                float volume = Mathf.Clamp01(quietest / rms);
                SetVolume(definition, volume);
                report.Add($"{name}: {ToDb(rms):F1} dB RMS, volume {volume:F2} ({ToDb(volume):+0.0;-0.0} dB)");
            }
        }

        // ---------- measurement ----------

        /// <summary>
        /// RMS ignoring near-silence.
        ///
        /// A plain average over the whole file makes anything with a long tail
        /// read as quiet, which is most explosions - the exact sounds being
        /// complained about.
        /// </summary>
        private static float GatedRms(float[] samples)
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

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            return peak;
        }

        private static float FromDb(float db) => Mathf.Pow(10f, db / 20f);

        private static float ToDb(float linear) => linear <= 0f ? -144f : 20f * Mathf.Log10(linear);

        /// <summary>
        /// Reads a clip's samples, decompressing it first if it is not already.
        ///
        /// GetData returns silence for a streaming or still-compressed clip rather
        /// than failing, so the import settings are forced and restored around the
        /// read - measuring zeros and calling it quiet would be the worst possible
        /// outcome here.
        /// </summary>
        private static bool TryRead(AudioClip clip, out float[] samples)
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

        private static IEnumerable<AudioClip> ClipsOf(SoundDefinition definition)
        {
            SerializedProperty array = new SerializedObject(definition).FindProperty("clips");
            if (array == null)
            {
                yield break;
            }

            for (int i = 0; i < array.arraySize; i++)
            {
                yield return array.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            }
        }

        private static void SetClips(SoundDefinition definition, List<AudioClip> clips)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty array = serialized.FindProperty("clips");
            array.arraySize = clips.Count;

            for (int i = 0; i < clips.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void SetVolume(SoundDefinition definition, float volume)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("volume").floatValue = Mathf.Clamp01(volume);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void ApplyImportSettings(string path, bool forceMono)
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
