using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The second roadmap's last six items, 27 to 32: a tip on the loading
    /// screen, a sound when a volume steps, credits that cover the rebuild, a
    /// screenshot key and the log of slow frames. Each has one rule that
    /// fails without anything looking wrong - a tip shown twice running, a
    /// slider that buzzes, a file name Windows refuses - and those are what
    /// is held here.
    /// </summary>
    public class ShippingPolishTests
    {
        // ---------- 27: tips ----------

        [Test]
        public void Tips_NeverRepeatTheLastOne()
        {
            for (int last = 0; last < LoadingTips.All.Length; last++)
            {
                for (int roll = 0; roll < 200; roll++)
                {
                    Assert.AreNotEqual(last, LoadingTips.Next(last, roll), "last " + last + ", roll " + roll);
                }
            }
        }

        [Test]
        public void Tips_AreAllReachable_AndInRange()
        {
            HashSet<int> seen = new HashSet<int>();

            for (int roll = 0; roll < 200; roll++)
            {
                int first = LoadingTips.Next(-1, roll);
                Assert.That(first, Is.InRange(0, LoadingTips.All.Length - 1));
                seen.Add(first);

                int after = LoadingTips.Next(3, roll);
                Assert.That(after, Is.InRange(0, LoadingTips.All.Length - 1));
            }

            Assert.AreEqual(LoadingTips.All.Length, seen.Count, "some tip can never be the first one shown");

            // A roll from a source that can go negative must not index off the end.
            Assert.That(LoadingTips.Next(2, int.MinValue), Is.InRange(0, LoadingTips.All.Length - 1));
        }

        [Test]
        public void Tips_AreOneShortLineEach()
        {
            Assert.That(LoadingTips.All.Length, Is.GreaterThanOrEqualTo(8));

            foreach (string tip in LoadingTips.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(tip));

                // The screen is up for about a second and a half.
                Assert.That(tip.Length, Is.LessThanOrEqualTo(80), tip);

                // The interface font's atlas is Extended ASCII.
                foreach (char c in tip)
                {
                    Assert.That((int)c, Is.LessThan(256), tip);
                }
            }
        }

        // ---------- 28: hear the level ----------

        [Test]
        public void Preview_WaitsOutItsGap_SoAHeldSliderDoesNotBuzz()
        {
            Assert.IsTrue(AudioDirector.MayPreview(5f, float.NegativeInfinity, AudioDirector.PreviewGap));
            Assert.IsFalse(AudioDirector.MayPreview(5.05f, 5f, AudioDirector.PreviewGap));
            Assert.IsTrue(AudioDirector.MayPreview(5.2f, 5f, AudioDirector.PreviewGap));
            Assert.That(AudioDirector.PreviewGap, Is.GreaterThanOrEqualTo(0.1f));
        }

        [Test]
        public void Preview_IsForTheTwoSlidersThatMakeNoSoundOfTheirOwn()
        {
            Assert.IsTrue(VolumeControl.PreviewsOnChange(AudioChannel.Master));
            Assert.IsTrue(VolumeControl.PreviewsOnChange(AudioChannel.Sfx));

            // Music is already playing, and a sample over it would hide the
            // very level being set.
            Assert.IsFalse(VolumeControl.PreviewsOnChange(AudioChannel.Music));
        }

        // ---------- 30: credits ----------

        [Test]
        public void Credits_NameTheRebuildsSources_AndCarryNoAddress()
        {
            string menu = File.ReadAllText("Assets/Scenes/Menu.unity");

            StringAssert.Contains("DEVELOPMENT TEAM", menu);
            StringAssert.Contains("Adiutorium", menu);
            StringAssert.Contains("Kenney", menu);
            StringAssert.Contains("Chakra Petch", menu);
            StringAssert.Contains("Advanced Micro Devices", menu);

            // The attribution NVIDIA's DLSS licence asks a game's credits for.
            StringAssert.Contains("NVIDIA Corporation", menu);

            // The 2023 screen carried the team's personal email addresses.
            StringAssert.DoesNotContain("@gmail", menu);
            StringAssert.DoesNotContain("@hotmail", menu);
            StringAssert.DoesNotContain("@outlook", menu);
        }

        [Test]
        public void CreditsRoll_StopsAtBothEnds()
        {
            Assert.AreEqual(0f, CreditsRoll.Clamp(-50f, 1200f, 400f));
            Assert.AreEqual(300f, CreditsRoll.Clamp(300f, 1200f, 400f));
            Assert.AreEqual(800f, CreditsRoll.Clamp(5000f, 1200f, 400f));

            // Shorter than its window: nothing to scroll.
            Assert.AreEqual(0f, CreditsRoll.Clamp(120f, 300f, 400f));
        }

        // ---------- 29: the icon ----------

        [Test]
        public void TheGameHasAnIcon_AndItsImageExists()
        {
            // An icon slot pointing at a deleted image fails the way the splash
            // logo did: nothing complains, and the build shows Unity's default.
            UnityEngine.Texture2D[] icons = UnityEditor.PlayerSettings.GetIcons(
                UnityEditor.Build.NamedBuildTarget.Unknown, UnityEditor.IconKind.Any);

            Assert.That(icons.Length, Is.GreaterThanOrEqualTo(1), "no default icon is set");
            Assert.IsNotNull(icons[0], "the default icon points at a missing image");
            Assert.AreEqual(icons[0].width, icons[0].height, "an icon is square");
        }

        // ---------- 31: screenshots ----------

        [Test]
        public void ScreenshotName_CarriesTheBuildAndTheTime()
        {
            DateTime when = new DateTime(2026, 9, 30, 21, 14, 7);

            Assert.AreEqual("26_0930-0825_polish 8ee1a3a+ 2026-09-30 21.14.07.png",
                ScreenshotName.For("26_0930-0825_polish · 8ee1a3a+", when));
            Assert.AreEqual("EDITOR 2026-09-30 21.14.07.png", ScreenshotName.For("EDITOR", when));
            Assert.AreEqual("2026-09-30 21.14.07.png", ScreenshotName.For(null, when));
        }

        [Test]
        public void ScreenshotName_TakesANumber_WhenItsSecondIsTaken()
        {
            var taken = new HashSet<string> { "A 21.14.07.png" };
            Assert.AreEqual("A 21.14.08.png", ScreenshotName.Free("A 21.14.08.png", taken.Contains));
            Assert.AreEqual("A 21.14.07 (2).png", ScreenshotName.Free("A 21.14.07.png", taken.Contains));

            taken.Add("A 21.14.07 (2).png");
            Assert.AreEqual("A 21.14.07 (3).png", ScreenshotName.Free("A 21.14.07.png", taken.Contains));
            Assert.AreEqual("A 21.14.07.png", ScreenshotName.Free("A 21.14.07.png", null));
        }

        [Test]
        public void PreviewVolume_IsSilentAtZero_AndTheSoundsOwnAtFull()
        {
            Assert.AreEqual(0f, AudioDirector.PreviewVolume(0.8f, 0f));
            Assert.AreEqual(0.8f, AudioDirector.PreviewVolume(0.8f, 1f), 0.0001f);
            Assert.Less(AudioDirector.PreviewVolume(0.8f, 0.5f), 0.8f);
        }

        /// <summary>
        /// The README tells a fresh clone which editor to install. It said
        /// 6000.6.3f1 for a day after the project moved to 6000.6.4f1.
        /// </summary>
        [Test]
        public void TheReadme_NamesTheEditorTheProjectIsOn()
        {
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string version = File.ReadAllText(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt"));
            var match = System.Text.RegularExpressions.Regex.Match(version, @"m_EditorVersion:\s*(\S+)");
            Assert.IsTrue(match.Success, "no editor version in ProjectVersion.txt");

            string readme = File.ReadAllText(Path.Combine(project, "README.md"));
            StringAssert.Contains("Unity " + match.Groups[1].Value, readme);

            foreach (System.Text.RegularExpressions.Match named in
                     System.Text.RegularExpressions.Regex.Matches(readme, @"6000\.\d+\.\d+[abf]\d+"))
            {
                Assert.AreEqual(match.Groups[1].Value, named.Value, "the README names another editor version");
            }
        }

        [Test]
        public void ScreenshotName_HoldsNothingAFileNameCannot()
        {
            string name = ScreenshotName.For("bad:name/with\\every*thing?\"<>|\n\there", new DateTime(2026, 1, 2, 3, 4, 5));

            Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()), name);
            StringAssert.EndsWith(".png", name);
            StringAssert.DoesNotContain("  ", name);
        }

        // ---------- 32: slow frames ----------

        [Test]
        public void HitchLog_WritesDownOnlyFramesOverFiftyMilliseconds()
        {
            Assert.IsFalse(HitchLog.IsHitch(1f / 60f));
            Assert.IsFalse(HitchLog.IsHitch(0.05f));
            Assert.IsTrue(HitchLog.IsHitch(0.051f));
            Assert.IsTrue(HitchLog.IsHitch(0.48f));
        }

        [Test]
        public void HitchLog_LineSaysHowLongWhereAndWhat()
        {
            string line = HitchLog.Describe(0.48f, 12.5f, 0.02f, "Game", 0f, "loading Game; scene Game loaded");

            StringAssert.Contains("480 ms", line);
            StringAssert.Contains("12.50 s", line);
            StringAssert.Contains("Game +0.02 s", line);
            StringAssert.Contains("x0", line);
            StringAssert.Contains("loading Game; scene Game loaded", line);

            StringAssert.Contains("nothing noted", HitchLog.Describe(0.06f, 1f, 1f, "Game", 1f, string.Empty));

            // A comma for the decimal point would make the log unreadable by
            // the script that reads it, whatever the machine's language.
            StringAssert.DoesNotContain(",", HitchLog.Describe(0.0612f, 1234.5f, 3.25f, "Menu", 0.4f, "x"));
        }
    }
}
