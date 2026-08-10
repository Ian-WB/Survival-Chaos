using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Creates the sound assets the game asks for, and the GameSounds asset that
    /// names them. Re-running updates in place rather than duplicating, so it is
    /// safe after pulling changes.
    ///
    /// It deliberately does not touch the clip lists. Tuning belongs to this tool;
    /// which audio file plays is a decision only a person listening can make, and
    /// a re-run must never quietly unassign one.
    /// </summary>
    public static class DefaultSoundAssets
    {
        private const string FolderPath = "Assets/Audio/Definitions";

        /// <summary>Where GameSounds has to live for Resources.Load to find it.</summary>
        private const string ResourcesFolder = "Assets/Resources";

        /// <summary>
        /// One sound and the tuning that suits what triggers it.
        ///
        /// The numbers are the point of this tool. A shot fired five times a
        /// second needs a short retrigger and pitch spread or it turns into a
        /// buzz; a victory sting needs neither and must never be dropped for
        /// being too frequent.
        /// </summary>
        private readonly struct Spec
        {
            public readonly string Field;
            public readonly string File;
            public readonly AudioChannel Channel;
            public readonly float Volume;
            public readonly Vector2 Pitch;
            public readonly float Retrigger;
            public readonly int MaxVoices;
            public readonly float Spatial;
            public readonly string Note;

            public Spec(string field, string file, AudioChannel channel, float volume,
                Vector2 pitch, float retrigger, int maxVoices, float spatial, string note)
            {
                Field = field;
                File = file;
                Channel = channel;
                Volume = volume;
                Pitch = pitch;
                Retrigger = retrigger;
                MaxVoices = maxVoices;
                Spatial = spatial;
                Note = note;
            }
        }

        private static readonly Spec[] Sounds =
        {
            // Fires every 0.5s at base attack speed and faster with upgrades, so
            // it carries the widest pitch spread and the tightest retrigger.
            new Spec("playerShot", "PlayerShot", AudioChannel.Sfx, 0.55f,
                new Vector2(0.94f, 1.06f), 0.06f, 4, 0f, "one per volley"),

            new Spec("playerHit", "PlayerHit", AudioChannel.Sfx, 0.9f,
                new Vector2(0.96f, 1.04f), 0.08f, 2, 0f, "taking damage"),

            // Once per run. No spread and no throttle - it should never be the
            // sound that gets dropped.
            new Spec("playerDeath", "PlayerDeath", AudioChannel.Sfx, 1f,
                Vector2.one, 0f, 1, 0f, "the run ending badly"),

            new Spec("levelUp", "LevelUp", AudioChannel.Sfx, 0.9f,
                Vector2.one, 0f, 1, 0f, "reaching a new level"),

            new Spec("skillPicked", "SkillPicked", AudioChannel.Ui, 0.85f,
                Vector2.one, 0f, 1, 0f, "a skill being granted"),

            // Dozens die a minute and several can land in the same frame, so this
            // is the one most in need of variation and a voice cap.
            new Spec("enemyDeath", "EnemyDeath", AudioChannel.Sfx, 0.7f,
                new Vector2(0.9f, 1.1f), 0.04f, 5, 0.8f, "shared enemy explosion"),

            new Spec("bossShot", "BossShot", AudioChannel.Sfx, 0.5f,
                new Vector2(0.95f, 1.05f), 0.12f, 3, 0.6f, "one per boss attack"),

            new Spec("bossDeath", "BossDeath", AudioChannel.Sfx, 1f,
                Vector2.one, 0f, 1, 0f, "the boss going down"),

            new Spec("victory", "Victory", AudioChannel.Ui, 1f,
                Vector2.one, 0f, 1, 0f, "the victory panel"),

            // Menu sounds answer to the Interface slider, which until now had
            // nothing to attenuate.
            new Spec("uiClick", "UiClick", AudioChannel.Ui, 0.8f,
                Vector2.one, 0.05f, 2, 0f, "any framed button"),

            // Retrigger matters here: hover fires again on reselection, and the
            // pointer crossing a column of buttons should not machine-gun.
            new Spec("uiHover", "UiHover", AudioChannel.Ui, 0.45f,
                new Vector2(0.98f, 1.02f), 0.07f, 2, 0f, "pointer arriving on a button")
        };

        [MenuItem("Survival Chaos/Create Default Sound Assets", priority = 41)]
        public static void Create()
        {
            Directory.CreateDirectory(FolderPath);
            Directory.CreateDirectory(ResourcesFolder);
            AssetDatabase.Refresh();

            Dictionary<string, SoundDefinition> created = new Dictionary<string, SoundDefinition>();

            foreach (Spec spec in Sounds)
            {
                created[spec.Field] = CreateOrUpdate(spec);
            }

            GameSounds sounds = LoadOrCreateRegistry();
            SerializedObject serialized = new SerializedObject(sounds);

            foreach (KeyValuePair<string, SoundDefinition> entry in created)
            {
                SerializedProperty property = serialized.FindProperty(entry.Key);
                if (property == null)
                {
                    Debug.LogError($"GameSounds has no '{entry.Key}' field. The spec list and the " +
                                   "asset have drifted apart - one of them needs updating.");
                    continue;
                }

                property.objectReferenceValue = entry.Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sounds);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameSounds.Forget();

            Report();
        }

        private static SoundDefinition CreateOrUpdate(Spec spec)
        {
            string path = $"{FolderPath}/{spec.File}.asset";

            SoundDefinition asset = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SoundDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("channel").intValue = (int)spec.Channel;
            serialized.FindProperty("volume").floatValue = spec.Volume;
            serialized.FindProperty("pitchRange").vector2Value = spec.Pitch;
            serialized.FindProperty("minRetrigger").floatValue = spec.Retrigger;
            serialized.FindProperty("maxVoices").intValue = spec.MaxVoices;
            serialized.FindProperty("spatialBlend").floatValue = spec.Spatial;
            // clips is deliberately untouched.
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            return asset;
        }

        private static GameSounds LoadOrCreateRegistry()
        {
            string path = $"{ResourcesFolder}/{GameSounds.ResourcePath}.asset";

            GameSounds sounds = AssetDatabase.LoadAssetAtPath<GameSounds>(path);
            if (sounds == null)
            {
                sounds = ScriptableObject.CreateInstance<GameSounds>();
                AssetDatabase.CreateAsset(sounds, path);
            }

            return sounds;
        }

        /// <summary>
        /// Lists which sounds still have no clip.
        ///
        /// A SoundDefinition with an empty clip list is dropped silently by the
        /// AudioDirector, which is correct behaviour and completely invisible -
        /// exactly the kind of dead control this project keeps finding. So the
        /// tool says out loud what is not going to make a noise yet.
        /// </summary>
        [MenuItem("Survival Chaos/Report Silent Sounds", priority = 42)]
        public static void Report()
        {
            List<string> silent = new List<string>();
            int total = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:SoundDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SoundDefinition sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
                if (sound == null)
                {
                    continue;
                }

                total++;
                if (!sound.HasClips)
                {
                    silent.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            if (silent.Count == 0)
            {
                Debug.Log($"All {total} sounds have clips assigned.");
                return;
            }

            Debug.LogWarning(
                $"{silent.Count} of {total} sounds have no clip and will play nothing:\n  " +
                string.Join("\n  ", silent) +
                $"\n\nDrop audio files into {FolderPath} and assign them to each asset's Clips list.");
        }
    }
}
