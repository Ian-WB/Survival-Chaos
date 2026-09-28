using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The clip picker's two promises: a sound never plays the same clip twice
    /// running, and before its first play every clip can come up. The second
    /// was broken for as long as the callers started the cursor at 0, which
    /// the picker read as clip 0 having just played.
    /// </summary>
    public class SoundDefinitionTests
    {
        private SoundDefinition sound;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private Random.State randomState;

        [SetUp]
        public void SetUp()
        {
            randomState = Random.state;
            Random.InitState(28);

            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            var serialized = new SerializedObject(sound);
            SerializedProperty list = serialized.FindProperty("clips");
            list.arraySize = 3;

            for (int i = 0; i < 3; i++)
            {
                AudioClip clip = AudioClip.Create("clip " + i, 441, 1, 44100, false);
                clips.Add(clip);
                list.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Random.state = randomState;
            Object.DestroyImmediate(sound);

            foreach (AudioClip clip in clips)
            {
                Object.DestroyImmediate(clip);
            }

            clips.Clear();
        }

        [Test]
        public void TheFirstPlay_CanBeAnyClip_ClipZeroIncluded()
        {
            var seen = new HashSet<AudioClip>();

            for (int i = 0; i < 300; i++)
            {
                int cursor = -1;
                seen.Add(sound.PickClip(ref cursor));
            }

            Assert.AreEqual(3, seen.Count);
            Assert.IsTrue(seen.Contains(clips[0]), "clip 0");
        }

        [Test]
        public void LaterPlays_NeverRepeatTheLastClip()
        {
            int cursor = -1;
            sound.PickClip(ref cursor);

            for (int i = 0; i < 300; i++)
            {
                int last = cursor;
                sound.PickClip(ref cursor);
                Assert.AreNotEqual(last, cursor, "play " + i);
            }
        }
    }
}
