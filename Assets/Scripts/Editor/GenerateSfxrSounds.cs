using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Fills every SoundDefinition with generated clips, so the game makes a noise
    /// everywhere before anyone has sourced a single audio file.
    ///
    /// These are placeholders with a real job: they prove the wiring works. A
    /// silent call site and a correctly-wired one look identical, and the only way
    /// to tell them apart is to hear something. Replace the ones that matter -
    /// boss death, victory, player death - with recorded audio later; the short
    /// combat sounds are the ones sfxr is genuinely good at.
    ///
    /// Everything is derived from <see cref="Seed"/>. Change it and re-run to
    /// reroll every sound; the variants of a single sound stay siblings because
    /// they are drawn in sequence from the same stream.
    /// </summary>
    public static class GenerateSfxrSounds
    {
        /// <summary>Change this to reroll. Any value works; there is nothing special about this one.</summary>
        private const int Seed = 20260810;

        private const string ClipFolder = "Assets/Audio/SFX";
        private const string DefinitionFolder = "Assets/Audio/Definitions";

        private readonly struct Spec
        {
            public readonly string Definition;
            public readonly SfxrKind Kind;
            public readonly int Variants;
            public readonly float PitchScale;

            public Spec(string definition, SfxrKind kind, int variants, float pitchScale = 1f)
            {
                Definition = definition;
                Kind = kind;
                Variants = variants;
                PitchScale = pitchScale;
            }
        }

        /// <summary>
        /// Variant counts are not uniform: they exist to stop repetition being
        /// audible, so they follow how often a sound actually fires. The player's
        /// shot goes five times a second and needs four; a victory sting plays
        /// once a run and needs one.
        ///
        /// The order of this list is load-bearing, which is not obvious. One
        /// System.Random walks the whole thing, so every entry's sound depends on
        /// how many draws the entries above it took. Inserting a spec in the
        /// middle silently rerolls every sound below it - the game comes back
        /// sounding different for a change that looks local. Add new sounds at the
        /// end, or accept that everything after the insertion point is new.
        /// </summary>
        private static readonly Spec[] Specs =
        {
            new Spec("PlayerShot", SfxrKind.Laser, 4, 1.15f),
            new Spec("PlayerHit", SfxrKind.HitHurt, 3),
            new Spec("PlayerDeath", SfxrKind.Explosion, 1, 0.75f),
            new Spec("LevelUp", SfxrKind.Powerup, 1),
            new Spec("SkillPicked", SfxrKind.Powerup, 1, 1.2f),
            new Spec("EnemyDeath", SfxrKind.Explosion, 4),
            // Lower than the player's, so the two are distinguishable when both
            // are firing - which is most of the boss fight.
            new Spec("BossShot", SfxrKind.Laser, 3, 0.65f),
            new Spec("BossDeath", SfxrKind.Explosion, 1, 0.55f),
            new Spec("Victory", SfxrKind.Powerup, 1, 0.9f),
            new Spec("UiClick", SfxrKind.Blip, 2),
            new Spec("UiHover", SfxrKind.Blip, 2, 1.35f),

            // Added last, and it has to stay last. A thruster rather than a
            // weapon: the same synth as the player's shot dropped well over an
            // octave, so a dash is never mistaken for having fired. Two variants
            // because it goes often enough in a fight for one to become a tic.
            new Spec("PlayerDash", SfxrKind.Laser, 2, 0.4f)
        };

        [MenuItem("Survival Chaos/Generate Placeholder SFX", priority = 43)]
        public static void Generate()
        {
            Directory.CreateDirectory(ClipFolder);
            AssetDatabase.Refresh();

            System.Random rng = new System.Random(Seed);
            int clipsWritten = 0;
            List<string> missing = new List<string>();

            foreach (Spec spec in Specs)
            {
                string definitionPath = $"{DefinitionFolder}/{spec.Definition}.asset";
                SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(definitionPath);

                if (definition == null)
                {
                    missing.Add(spec.Definition);
                    continue;
                }

                List<AudioClip> clips = new List<AudioClip>();

                for (int v = 0; v < spec.Variants; v++)
                {
                    SfxrParams p = SfxrPresets.Build(spec.Kind, rng);
                    SfxrPresets.Pitch(ref p, spec.PitchScale);

                    float[] samples = SfxrSynth.Render(p);
                    if (samples.Length == 0)
                    {
                        continue;
                    }

                    SfxrSynth.Normalise(samples);
                    SfxrSynth.FadeOut(samples);

                    string name = spec.Variants == 1 ? spec.Definition : $"{spec.Definition}_{v + 1}";
                    string path = $"{ClipFolder}/{name}.wav";
                    SfxrSynth.WriteWav(path, samples);
                    clipsWritten++;

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    ApplyImportSettings(path);

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }

                AssignClips(definition, clips);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    "No SoundDefinition asset for: " + string.Join(", ", missing) +
                    ".\nRun 'Survival Chaos/Create Default Sound Assets' first - this tool fills " +
                    "those assets in, it does not create them.");
            }

            Debug.Log($"Wrote {clipsWritten} placeholder clips to {ClipFolder} and assigned them. " +
                      $"Seed {Seed} - change it in GenerateSfxrSounds and re-run to reroll.");

            DefaultSoundAssets.Report();
        }

        /// <summary>
        /// Short, uncompressed and mono, which is what a game firing five shots a
        /// second wants: Vorbis would cost a decode on every one of them, and
        /// Unity only spatialises mono properly - two of these sounds are
        /// positional.
        /// </summary>
        private static void ApplyImportSettings(string path)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            importer.forceToMono = true;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            // Per-platform since Unity moved it off the importer itself.
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;

            importer.SaveAndReimport();
        }

        private static void AssignClips(SoundDefinition definition, List<AudioClip> clips)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty array = serialized.FindProperty("clips");

            if (array == null)
            {
                Debug.LogError("SoundDefinition has no 'clips' field.", definition);
                return;
            }

            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

    }
}
