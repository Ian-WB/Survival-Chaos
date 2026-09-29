using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Music streams from disk. Decompressed on load, the menu's three-minute
    /// track held up every return to the menu by about 0.4 s and then sat in
    /// 32 MB of memory; streamed, it opens in a millisecond and holds about
    /// 200 KB. The wrong setting fails without saying so - the track still
    /// plays - so this reads the importers.
    ///
    /// And it loops. The menu's did not from the day it was added, so a player
    /// who stayed on the menu got 3:09 of music and then silence.
    /// </summary>
    public class MusicImportTests
    {
        [TestCase("Assets/Scenes/Menu.unity")]
        [TestCase("Assets/Scenes/Game.unity")]
        public void EachScenesMusicLoops(string path)
        {
            SavedScene scene = SavedScene.Load(path);
            string music = scene.GameObjectNamed("Music");
            Assert.IsNotNull(music, path + " has no Music object");
            Assert.IsNotNull(scene.ScriptWith(music, "track"), "Music in " + path + " has no MusicSource");

            string source = scene.Component(music, "AudioSource");
            Assert.IsNotNull(source, "Music in " + path + " has no AudioSource");
            Assert.AreEqual(1f, scene.Float(source, "Loop"), path);
        }

        [Test]
        public void EveryMusicTrackStreams()
        {
            int tracks = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:SoundDefinition", new[] { "Assets/Audio/Definitions" }))
            {
                SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.Channel != AudioChannel.Music)
                {
                    continue;
                }

                SerializedProperty clips = new SerializedObject(definition).FindProperty("clips");

                for (int i = 0; i < clips.arraySize; i++)
                {
                    AudioClip clip = clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                    if (clip == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(clip);
                    AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);

                    // A Standalone override would win over the default in a build.
                    AudioImporterSampleSettings settings = importer.ContainsSampleSettingsOverride("Standalone")
                        ? importer.GetOverrideSampleSettings("Standalone")
                        : importer.defaultSampleSettings;

                    Assert.AreEqual(AudioClipLoadType.Streaming, settings.loadType, path);
                    tracks++;
                }
            }

            Assert.GreaterOrEqual(tracks, 2, "the menu and gameplay tracks were not found");
        }
    }
}
