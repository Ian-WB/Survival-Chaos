using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the nine sounds added on 30 September 2026 for moments that had
    /// none, or borrowed another's: salvage, the deflector blocking a hit,
    /// Slow Mo starting and running out, an offer about to expire, an act of
    /// the Leviathan fight ending, the two horns before it arrives, and the
    /// heartbeat on the ship's last hit point.
    ///
    /// Seven come from the Kenney packs already in the project, which sit in
    /// folders Unity ignores (the trailing ~). Each is copied in for as long as
    /// it takes to read, cut and balanced by BalanceSoundLevels' arithmetic, as
    /// every other clip was, and written to Balanced/ like the rest. The Sonniss
    /// folder holds only the three clips already in use.
    ///
    /// Neither pack has a horn or a heartbeat, so those two are synthesised
    /// here: a low reedy horn, heard close as the Leviathan arrives and far
    /// off, muffled and echoing, ten seconds before; and a soft two-beat thump.
    ///
    /// Picked without being heard, by measurement: length, loudness, and
    /// whether the pitch rises or falls (minimize_* falls, which is Slow Mo
    /// starting; maximize_* rises, which is it ending). Each has two
    /// alternatives written beside it; swapping one in is a change to that
    /// line and a run of this tool. Levels come from BalanceSoundLevels'
    /// IntentDb, which holds these nine and no others (see the note on it).
    ///
    /// Safe to run again: it rebuilds the nine clips and definitions and
    /// touches nothing else.
    /// </summary>
    public static class BuildMomentSounds
    {
        private const string BalancedFolder = "Assets/Audio/SFX/Balanced";
        private const string DefinitionFolder = "Assets/Audio/Definitions";
        private const string LibraryFolder = "Assets/Audio/SFX";
        private const string ScratchFolder = "Assets/Temp/MomentSounds";
        private const string GameSoundsPath = "Assets/Resources/GameSounds.asset";
        private const int SynthRate = 44100;

        private readonly struct Moment
        {
            public readonly string Field;
            public readonly string Name;
            public readonly string Source;
            public readonly AudioChannel Channel;
            public readonly Vector2 Pitch;
            public readonly float Retrigger;
            public readonly int Voices;
            public readonly string Alternatives;

            public Moment(string field, string name, string source, AudioChannel channel,
                Vector2 pitch, float retrigger, int voices, string alternatives)
            {
                Field = field;
                Name = name;
                Source = source;
                Channel = channel;
                Pitch = pitch;
                Retrigger = retrigger;
                Voices = voices;
                Alternatives = alternatives;
            }
        }

        /// <summary>
        /// The sounds, their sources and how they play. A source is a Kenney
        /// file, "pack/file.ogg", or "synth:" and the name of one of the
        /// generators below.
        /// </summary>
        private static readonly Moment[] Moments =
        {
            // Short and low, rising: a lift rather than the pluck an upgrade
            // makes. On the Interface channel with Skill Picked, the other
            // collection sound.
            new Moment("salvagePicked", "SalvagePicked", "kenney_interface-sounds~/maximize_006.ogg",
                AudioChannel.Ui, new Vector2(0.96f, 1.04f), 0.05f, 2,
                "confirmation_003, forceField_001"),

            // A ricochet: something struck and glanced off. The hit it replaces
            // already has a force field in it, so the block must not.
            new Moment("deflectorBlock", "DeflectorBlock", "kenney_sci-fi-sounds~/laserRetro_000.ogg",
                AudioChannel.Sfx, new Vector2(0.97f, 1.03f), 0.1f, 1,
                "glass_004, forceField_003"),

            // A pair: the same interface sweep falling, then rising. On the
            // Interface channel, so the game's own slowdown does not slow them.
            new Moment("slowMoStart", "SlowMoStart", "kenney_interface-sounds~/minimize_004.ogg",
                AudioChannel.Ui, Vector2.one, 0.2f, 1,
                "minimize_005, minimize_002"),
            new Moment("slowMoEnd", "SlowMoEnd", "kenney_interface-sounds~/maximize_004.ogg",
                AudioChannel.Ui, Vector2.one, 0.2f, 1,
                "maximize_005, maximize_002"),

            // Three a warning, and three pickups ticking together: the
            // retrigger makes those one tick.
            new Moment("offerExpiring", "OfferExpiring", "kenney_interface-sounds~/tick_002.ogg",
                AudioChannel.Sfx, Vector2.one, 0.25f, 1,
                "tick_004, toggle_004"),

            // A heavy metal strike, played a third low for weight.
            new Moment("actEnd", "ActEnd", "kenney_sci-fi-sounds~/impactMetal_003.ogg",
                AudioChannel.Sfx, new Vector2(0.78f, 0.82f), 0.5f, 1,
                "doorClose_001, error_006"),

            new Moment("bossHornDistant", "BossHornDistant", "synth:horn-distant",
                AudioChannel.Sfx, Vector2.one, 1f, 1,
                "spaceEngine_001 or spaceEngineLow_003, cut to 3 s"),
            new Moment("bossHornArrival", "BossHornArrival", "synth:horn-arrival",
                AudioChannel.Sfx, Vector2.one, 1f, 1,
                "spaceEngineLarge_000 or spaceEngine_000, cut to 3 s"),

            new Moment("lowHealth", "LowHealth", "synth:heartbeat",
                AudioChannel.Sfx, Vector2.one, 0.3f, 1,
                "drop_002 or impactMetal_004, played at half pitch")
        };

        [MenuItem("Survival Chaos/Audio/Build Moment Sounds", priority = 105)]
        public static void Build()
        {
            GameSounds registry = AssetDatabase.LoadAssetAtPath<GameSounds>(GameSoundsPath);
            if (registry == null)
            {
                Debug.LogError($"No GameSounds at {GameSoundsPath}. It names every sound the game plays, and this tool does not make it.");
                return;
            }

            Directory.CreateDirectory(ScratchFolder);
            List<string> report = new List<string>();
            SerializedObject slots = new SerializedObject(registry);

            try
            {
                foreach (Moment moment in Moments)
                {
                    if (!TryMake(moment, out float[] samples, out int channels, out int rate, out string file))
                    {
                        report.Add($"{moment.Name}: could not read {moment.Source}, left alone");
                        continue;
                    }

                    float gain = Balance(samples);
                    string outPath = $"{BalancedFolder}/{file}.wav";
                    SfxrSynth.WriteWav(outPath, samples, channels, rate);
                    AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                    BalanceSoundLevels.ApplyImportSettings(outPath, false);

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
                    SoundDefinition definition = Define(moment, clip);

                    SerializedProperty slot = slots.FindProperty(moment.Field);
                    if (slot == null)
                    {
                        report.Add($"{moment.Name}: GameSounds has no '{moment.Field}' field");
                        continue;
                    }

                    slot.objectReferenceValue = definition;
                    report.Add($"{moment.Name}: {file}.wav, {samples.Length / channels / (float)rate:F2} s, " +
                               $"{BalanceSoundLevels.ToDb(gain):+0.0;-0.0} dB to balance, volume " +
                               $"{definition.Volume:F3}. Alternatives: {moment.Alternatives}");
                }

                slots.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
                GameSounds.Forget();
            }
            finally
            {
                AssetDatabase.DeleteAsset(ScratchFolder);
            }

            Debug.Log("Moment sounds built:\n  " + string.Join("\n  ", report));
        }

        private static bool TryMake(Moment moment, out float[] samples, out int channels, out int rate,
            out string file)
        {
            if (moment.Source.StartsWith("synth:", StringComparison.Ordinal))
            {
                string which = moment.Source.Substring("synth:".Length);
                channels = 1;
                rate = SynthRate;
                file = which == "heartbeat" ? "heartbeat" : "bossHorn_" + which.Replace("horn-", "");
                samples = which == "heartbeat" ? Heartbeat()
                    : Horn(distant: which == "horn-distant");
                return true;
            }

            file = Path.GetFileNameWithoutExtension(moment.Source);
            samples = null;
            channels = 1;
            rate = SynthRate;

            string from = $"{LibraryFolder}/{moment.Source}";
            if (!File.Exists(from))
            {
                return false;
            }

            string scratch = $"{ScratchFolder}/{Path.GetFileName(moment.Source)}";
            File.Copy(from, scratch, overwrite: true);
            AssetDatabase.ImportAsset(scratch, ImportAssetOptions.ForceSynchronousImport);

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(scratch);
            if (clip == null || !BalanceSoundLevels.TryRead(clip, out samples))
            {
                return false;
            }

            channels = clip.channels;
            rate = clip.frequency;
            return true;
        }

        /// <summary>To the same loudness as every other clip, peak-limited, by BalanceSoundLevels' arithmetic.</summary>
        private static float Balance(float[] samples)
        {
            float rms = BalanceSoundLevels.GatedRms(samples);
            if (rms <= 0f)
            {
                return 1f;
            }

            float gain = BalanceSoundLevels.FromDb(BalanceSoundLevels.TargetRmsDb) / rms;
            float ceiling = BalanceSoundLevels.FromDb(BalanceSoundLevels.PeakCeilingDb);
            float peak = BalanceSoundLevels.Peak(samples);
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

        private static SoundDefinition Define(Moment moment, AudioClip clip)
        {
            string path = $"{DefinitionFolder}/{moment.Name}.asset";
            SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SoundDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("channel").intValue = (int)moment.Channel;
            so.FindProperty("volume").floatValue =
                Mathf.Clamp01(BalanceSoundLevels.FromDb(BalanceSoundLevels.IntentFor(moment.Name)));
            so.FindProperty("pitchRange").vector2Value = moment.Pitch;
            so.FindProperty("minRetrigger").floatValue = moment.Retrigger;
            so.FindProperty("maxVoices").intValue = moment.Voices;
            so.FindProperty("spatialBlend").floatValue = 0f;

            SerializedProperty clips = so.FindProperty("clips");
            clips.arraySize = clip != null ? 1 : 0;
            if (clip != null)
            {
                clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // ---------- synthesis ----------

        /// <summary>
        /// A low reedy horn: a band-limited sawtooth on D2 with a sub-octave
        /// under it, a scoop up into the note, a slow vibrato once it has
        /// settled, and a filter that opens as it swells, which is most of what
        /// makes a buzz read as brass.
        ///
        /// The distant one swells and fades more slowly, is darker, and is
        /// sent through a small room of echoes: far away is mostly less top,
        /// and more reflection than direct sound.
        /// </summary>
        internal static float[] Horn(bool distant)
        {
            const float f0 = 73.42f;
            float attack = distant ? 0.7f : 0.35f;
            float hold = distant ? 1.6f : 1.9f;
            float release = distant ? 1.4f : 0.9f;
            float tail = distant ? 1.6f : 0.4f;
            float length = attack + hold + release + tail;

            int n = Mathf.CeilToInt(length * SynthRate);
            float[] dry = new float[n];
            double phase = 0, phaseDetuned = 0, subPhase = 0;
            // Nothing above the filter's widest opening is heard, so none is built.
            int harmonics = Mathf.FloorToInt(3500f / f0);

            float lowA = 0f, lowB = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SynthRate;
                float env = t < attack ? Smooth(t / attack)
                    : t < attack + hold ? 1f
                    : t < attack + hold + release ? 1f - Smooth((t - attack - hold) / release)
                    : 0f;

                if (env <= 0f && t > attack)
                {
                    dry[i] = 0f;
                    continue;
                }

                float scoop = 1f - 0.03f * Mathf.Exp(-t / 0.12f);
                float vibrato = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 4.6f * t) * Mathf.Clamp01((t - 0.8f) / 0.6f);
                float f = f0 * scoop * vibrato;

                phase += f / SynthRate;
                phaseDetuned += f * 1.0035f / SynthRate;
                subPhase += f * 0.5f / SynthRate;

                float saw = 0f;
                for (int k = 1; k <= harmonics; k++)
                {
                    saw += (Mathf.Sin((float)(2.0 * Math.PI * k * (phase % 1.0))) +
                            Mathf.Sin((float)(2.0 * Math.PI * k * (phaseDetuned % 1.0)))) / k;
                }

                float voice = saw * 0.35f + 0.5f * Mathf.Sin((float)(2.0 * Math.PI * (subPhase % 1.0)));

                // The filter opens with the swell: dark at the edges of the note,
                // brassy at its height.
                float cutoff = distant ? 180f + 420f * env : 260f + 1500f * env;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SynthRate);
                lowA += a * (voice - lowA);
                lowB += a * (lowA - lowB);

                dry[i] = lowB * env;
            }

            if (!distant)
            {
                return dry;
            }

            // A small Schroeder room: four combs into two all-passes, after a
            // short pre-delay. Mostly echo, a little of the horn itself.
            float[] wet = Room(dry, preDelay: 0.07f);
            float[] mixed = new float[n];
            for (int i = 0; i < n; i++)
            {
                mixed[i] = dry[i] * 0.35f + wet[i] * 0.9f;
            }

            return mixed;
        }

        /// <summary>
        /// A soft heartbeat, lub-dub: two low thumps, each a sine that falls in
        /// pitch as it dies, the second a little higher and softer. Gently
        /// driven, which gives a low thump enough harmonics to be heard on
        /// small speakers without making it any sharper.
        /// </summary>
        internal static float[] Heartbeat()
        {
            float length = 0.55f;
            int n = Mathf.CeilToInt(length * SynthRate);
            float[] beat = new float[n];

            Thump(beat, start: 0f, fromHz: 80f, toHz: 46f, decay: 0.05f, level: 1f);
            Thump(beat, start: 0.19f, fromHz: 92f, toHz: 54f, decay: 0.04f, level: 0.7f);

            float drive = 2.2f;
            float norm = (float)Math.Tanh(drive);
            float low = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 500f / SynthRate);

            for (int i = 0; i < n; i++)
            {
                float shaped = (float)Math.Tanh(drive * beat[i]) / norm;
                low += a * (shaped - low);
                beat[i] = low;
            }

            return beat;
        }

        private static void Thump(float[] into, float start, float fromHz, float toHz, float decay, float level)
        {
            int first = Mathf.RoundToInt(start * SynthRate);
            double phase = 0;

            for (int i = first; i < into.Length; i++)
            {
                float t = (i - first) / (float)SynthRate;
                float f = toHz + (fromHz - toHz) * Mathf.Exp(-t / 0.035f);
                phase += f / SynthRate;

                float attack = Mathf.Clamp01(t / 0.006f);
                float env = attack * Mathf.Exp(-t / decay);
                if (env < 0.0005f && t > 0.05f)
                {
                    break;
                }

                into[i] += level * env * Mathf.Sin((float)(2.0 * Math.PI * (phase % 1.0)));
            }
        }

        private static float[] Room(float[] dry, float preDelay)
        {
            int n = dry.Length;
            float[] wet = new float[n];
            int[] combs = { 1427, 1637, 1813, 1933 };
            float feedback = 0.82f;
            int pre = Mathf.RoundToInt(preDelay * SynthRate);

            foreach (int delay in combs)
            {
                float[] line = new float[delay];
                int at = 0;
                float damp = 0f;

                for (int i = 0; i < n; i++)
                {
                    float input = i >= pre ? dry[i - pre] : 0f;
                    float output = line[at];
                    damp += 0.35f * (output - damp);
                    line[at] = input + damp * feedback;
                    at = (at + 1) % delay;
                    wet[i] += output * 0.25f;
                }
            }

            foreach (int delay in new[] { 223, 79 })
            {
                float[] line = new float[delay];
                int at = 0;

                for (int i = 0; i < n; i++)
                {
                    float buffered = line[at];
                    float output = -wet[i] + buffered;
                    line[at] = wet[i] + buffered * 0.5f;
                    at = (at + 1) % delay;
                    wet[i] = output;
                }
            }

            return wet;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
