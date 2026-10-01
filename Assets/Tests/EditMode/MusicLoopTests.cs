using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Leviathan's track: the loop arithmetic, the handover, and the
    /// Game scene's settings for it.
    ///
    /// Neon Hyperdrive opens on 17 s of near-silence and ends on a fade with
    /// 4.4 s of nothing after it. Looped whole, the fight would drop to almost
    /// no music for about half a minute of every two. The loop points keep it
    /// to the build and the two drops, bars 10 to 65.
    /// </summary>
    public class MusicLoopTests
    {
        private const int Rate = 44100;

        [Test]
        public void BeforeTheLoopEndNothingMoves()
        {
            int start = MusicLoop.ToSamples(17.313f, Rate);
            int end = MusicLoop.ToSamples(111.599f, Rate);

            Assert.AreEqual(-1, MusicLoop.Wrap(start, start, end));
            Assert.AreEqual(-1, MusicLoop.Wrap(end - 1, start, end));
        }

        [Test]
        public void AtTheLoopEndPlaybackGoesBackToTheStart()
        {
            int start = MusicLoop.ToSamples(17.313f, Rate);
            int end = MusicLoop.ToSamples(111.599f, Rate);

            Assert.AreEqual(start, MusicLoop.Wrap(end, start, end));
        }

        [Test]
        public void AFrameOfOvershootIsCarriedOver()
        {
            // Seen a frame late at 60 fps: the beat stays where it was.
            int start = 1000;
            int end = 50000;
            int late = Rate / 60;

            Assert.AreEqual(start + late, MusicLoop.Wrap(end + late, start, end));
        }

        [Test]
        public void NoLoopEndMeansTheClipLoopsWhole()
        {
            Assert.AreEqual(-1, MusicLoop.Wrap(999999, 1000, 0));
            Assert.AreEqual(-1, MusicLoop.Wrap(999999, 1000, 1000));
        }

        [Test]
        public void TheHandoverKeepsTheLevel()
        {
            // Equal power: the two gains' squares always sum to one, so the
            // music does not dip halfway through the Leviathan's arrival.
            for (float mix = 0f; mix <= 1f; mix += 0.125f)
            {
                MusicLoop.Crossfade(mix, out float from, out float to);
                Assert.AreEqual(1f, from * from + to * to, 1e-4f, "at " + mix);
            }

            MusicLoop.Crossfade(0f, out float allFrom, out float noneTo);
            Assert.AreEqual(1f, allFrom, 1e-6f);
            Assert.AreEqual(0f, noneTo, 1e-6f);

            MusicLoop.Crossfade(1f, out float noneFrom, out float allTo);
            Assert.AreEqual(0f, noneFrom, 1e-6f);
            Assert.AreEqual(1f, allTo, 1e-6f);
        }

        [Test]
        public void TheGameSceneHasTheLeviathansTrackWithItsLoopInsideTheClip()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string music = scene.GameObjectNamed("Music");
            Assert.IsNotNull(music, "Game has no Music object");

            string script = scene.ScriptWith(music, "bossTrack");
            Assert.IsNotNull(script, "Music in Game has no Leviathan track field");
            Assert.AreNotEqual("0", scene.Reference(script, "bossTrack"), "no Leviathan track is set");

            float start = scene.Float(script, "bossLoopStart");
            float end = scene.Float(script, "bossLoopEnd");
            Assert.Greater(end, start, "the loop ends before it starts");

            SoundDefinition boss = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/Audio/Definitions/BossMusic.asset");
            Assert.IsNotNull(boss, "BossMusic.asset is missing");
            Assert.AreEqual(AudioChannel.Music, boss.Channel);

            int cursor = -1;
            AudioClip clip = boss.PickClip(ref cursor);
            Assert.IsNotNull(clip, "BossMusic has no clip");
            Assert.LessOrEqual(end, clip.length, "the loop ends after the clip does");
        }
    }
}
