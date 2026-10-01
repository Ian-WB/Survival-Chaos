using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// ThirdPartyNotices.txt goes beside the exe in every Windows build, and the
    /// licences of what ships ask for their notices to go with every copy. A
    /// notice left out says nothing on screen, so these read the file.
    /// </summary>
    public class ThirdPartyNoticesTests
    {
        private static string ProjectFolder => Path.GetDirectoryName(Application.dataPath);

        private static string Notices()
        {
            string path = Path.Combine(ProjectFolder, "ThirdPartyNotices.txt");
            Assert.IsTrue(File.Exists(path), "ThirdPartyNotices.txt is missing from the project folder");
            return File.ReadAllText(path);
        }

        [Test]
        public void AmdsFsrNoticeIsThere()
        {
            string text = Notices();
            StringAssert.Contains("Copyright (C) Advanced Micro Devices, Inc.", text);
            StringAssert.Contains("The above copyright notice and this permission notice shall be included", text);
        }

        [Test]
        public void EveryFontsOwnCopyrightIsThere()
        {
            // Each OFL font in the project arrived with its licence file, whose
            // first line is its copyright. A new font brings a new file, and
            // this fails until its notice joins the rest.
            string text = Notices();
            string[] licences = Directory.GetFiles(Application.dataPath, "*OFL*.txt", SearchOption.AllDirectories);
            Assert.IsNotEmpty(licences, "found no font licences to check");

            foreach (string licence in licences)
            {
                string copyright = File.ReadAllLines(licence)[0].Trim();
                StringAssert.Contains(copyright, text, Path.GetFileName(licence) + "'s copyright is not in the notices");
            }

            StringAssert.Contains("SIL OPEN FONT LICENSE Version 1.1", text);
            StringAssert.Contains("each copy\ncontains the above copyright notice and this license", text.Replace("\r\n", "\n"));
        }

        [Test]
        public void DlssIsAttributed()
        {
            StringAssert.Contains("NVIDIA and DLSS are trademarks", Notices());
        }

        /// <summary>
        /// CC0, so nothing requires it, but the composer asks for credit where
        /// it can be given, and this file ships beside every build.
        /// </summary>
        [Test]
        public void EveryMusicTrackIsCredited()
        {
            string text = Notices();
            StringAssert.Contains("Adiutorium", text);
            StringAssert.Contains("opengameart.org/content/darbuka-delight", text);
            StringAssert.Contains("opengameart.org/content/chase-2", text);
            StringAssert.Contains("opengameart.org/content/neon-hyperdrive", text);
        }
    }
}
