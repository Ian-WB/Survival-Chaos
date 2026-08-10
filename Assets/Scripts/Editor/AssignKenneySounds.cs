using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Points every SoundDefinition at the Kenney packs, replacing the generated
    /// placeholders.
    ///
    /// The clips were chosen by filename, not by listening. That is a real
    /// limitation and the mapping below says out loud what each choice assumes -
    /// "laserSmall" is the player because the boss gets "laserLarge", "impactMetal"
    /// is the ship being struck. Anything that turns out wrong is one line here,
    /// and re-running fixes it everywhere.
    ///
    /// Both packs are CC0, so nothing here needs attribution to ship.
    /// </summary>
    public static class AssignKenneySounds
    {
        private const string DefinitionFolder = "Assets/Audio/Definitions";
        private const string SciFi = "Assets/Audio/SFX/kenney_sci-fi-sounds";
        private const string Interface = "Assets/Audio/SFX/kenney_interface-sounds";

        private readonly struct Mapping
        {
            public readonly string Definition;
            public readonly string Folder;
            public readonly string[] Files;
            public readonly string Why;

            public Mapping(string definition, string folder, string why, params string[] files)
            {
                Definition = definition;
                Folder = folder;
                Why = why;
                Files = files;
            }
        }

        private static readonly Mapping[] Mappings =
        {
            // Small against large is the whole reason these two are separable
            // during the boss fight, when both are firing constantly.
            new Mapping("PlayerShot", SciFi, "small laser - the rapid one",
                "laserSmall_000", "laserSmall_001", "laserSmall_002", "laserSmall_003"),

            new Mapping("BossShot", SciFi, "large laser - heavier than the player's",
                "laserLarge_000", "laserLarge_001", "laserLarge_002"),

            new Mapping("PlayerHit", SciFi, "metal impact - something striking the ship",
                "impactMetal_000", "impactMetal_001", "impactMetal_002"),

            new Mapping("EnemyDeath", SciFi, "crunchy explosion - happens dozens of times a run",
                "explosionCrunch_000", "explosionCrunch_001", "explosionCrunch_002", "explosionCrunch_003"),

            // The pack has exactly two low-frequency explosions, and the two
            // deaths that end a run get one each so they cannot be confused.
            new Mapping("PlayerDeath", SciFi, "low explosion - bigger than an enemy pop",
                "lowFrequency_explosion_000"),

            new Mapping("BossDeath", SciFi, "the other low explosion",
                "lowFrequency_explosion_001"),

            new Mapping("LevelUp", Interface, "confirmation chime",
                "confirmation_001"),

            // Fires in the same instant as LevelUp, so it needs a different
            // register rather than a second chime on top of the first.
            new Mapping("SkillPicked", Interface, "short pluck - punctuates the level-up chime",
                "pluck_001"),

            new Mapping("Victory", Interface, "the fullest confirmation in the pack",
                "confirmation_004"),

            new Mapping("UiClick", Interface, "button press",
                "click_001", "click_002"),

            // "select" is the pack's menu-navigation sound, which is what hover is.
            new Mapping("UiHover", Interface, "menu navigation - swap for tick_001 if too present",
                "select_001", "select_002")
        };

        [MenuItem("Survival Chaos/Assign Kenney SFX", priority = 44)]
        public static void Assign()
        {
            int assigned = 0;
            List<string> problems = new List<string>();

            foreach (Mapping map in Mappings)
            {
                string definitionPath = $"{DefinitionFolder}/{map.Definition}.asset";
                SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(definitionPath);

                if (definition == null)
                {
                    problems.Add($"no SoundDefinition at {definitionPath}");
                    continue;
                }

                List<AudioClip> clips = new List<AudioClip>();

                foreach (string file in map.Files)
                {
                    AudioClip clip = Load(map.Folder, file);
                    if (clip == null)
                    {
                        problems.Add($"{map.Definition}: no clip named {file} in {map.Folder}");
                        continue;
                    }

                    // Only the positional sounds are forced to mono. Unity will
                    // not spatialise a stereo clip properly, and the rest have
                    // nothing to gain from being halved.
                    ApplyImportSettings(AssetDatabase.GetAssetPath(clip), definition.SpatialBlend > 0f);
                    clips.Add(clip);
                }

                if (clips.Count == 0)
                {
                    problems.Add($"{map.Definition}: nothing assigned, left as it was");
                    continue;
                }

                AssignClips(definition, clips);
                assigned += clips.Count;
                Debug.Log($"{map.Definition} <- {clips.Count} clip(s): {map.Why}", definition);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (problems.Count > 0)
            {
                Debug.LogWarning("Kenney assignment had problems:\n  " + string.Join("\n  ", problems));
            }

            Debug.Log($"Assigned {assigned} Kenney clips across {Mappings.Length} sounds. " +
                      "The generated placeholders in Audio/SFX/Generated are now unreferenced.");

            DefaultSoundAssets.Report();
        }

        /// <summary>Kenney ships .ogg; the extension is checked rather than assumed.</summary>
        private static AudioClip Load(string folder, string file)
        {
            foreach (string extension in new[] { ".ogg", ".wav", ".mp3" })
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{file}{extension}");
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private static void ApplyImportSettings(string path, bool forceMono)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            bool changed = importer.forceToMono != forceMono
                || settings.loadType != AudioClipLoadType.DecompressOnLoad
                || settings.compressionFormat != AudioCompressionFormat.PCM
                || !settings.preloadAudioData;

            // Reimporting is slow and these are 200 files. Only touch the ones
            // that are actually wrong.
            if (!changed)
            {
                return;
            }

            importer.forceToMono = forceMono;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
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
