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

        /// <summary>
        /// Unity's denoising package puts Intel's Open Image Denoise and TBB
        /// in every build, used or not, and Apache 2.0 asks for the licence
        /// to go with them. Taking the package out of the manifest is the
        /// other way to pass this.
        /// </summary>
        [Test]
        public void IntelsDenoiserIsCoveredWhileItShips()
        {
            string manifest = File.ReadAllText(Path.Combine(ProjectFolder, "Packages", "manifest.json"));
            if (!manifest.Contains("com.unity.rendering.denoising"))
            {
                Assert.Pass("the denoising package is not installed");
            }

            string text = Notices();
            StringAssert.Contains("Intel Open Image Denoise", text);
            StringAssert.Contains("Threading Building Blocks", text);
            StringAssert.Contains("Apache License", text);
            StringAssert.Contains("END OF TERMS AND CONDITIONS", text);
        }

        /// <summary>
        /// Newtonsoft.Json.dll is in the Release build of 2 October as well
        /// as the debug ones: it comes in with the Unity CLI's package, whose
        /// other libraries a Release build drops. MIT asks for its notice
        /// with every copy.
        /// </summary>
        [Test]
        public void JsonNetIsCoveredWhileItShips()
        {
            string packages = File.ReadAllText(Path.Combine(ProjectFolder, "Packages", "packages-lock.json"));
            if (!packages.Contains("com.unity.nuget.newtonsoft-json"))
            {
                Assert.Pass("Newtonsoft.Json is not installed");
            }

            string text = Notices();
            StringAssert.Contains("Newtonsoft.Json", text);
            StringAssert.Contains("Copyright (c) 2007 James Newton-King", text);
        }

        /// <summary>
        /// Microsoft's licence for DirectStorage asks only that its notices
        /// are left alone, but the MIT and Apache components compiled into it
        /// ask for theirs with every copy. So Microsoft's own notices file
        /// ships whole, and the game's file says where it is.
        /// </summary>
        [Test]
        public void DirectStoragesNoticesShipWhole()
        {
            string path = Path.Combine(ProjectFolder, "ThirdPartyNotices-DirectStorage.txt");
            Assert.IsTrue(File.Exists(path), "ThirdPartyNotices-DirectStorage.txt is missing from the project folder");

            string microsofts = File.ReadAllText(path);
            StringAssert.StartsWith("NOTICES AND INFORMATION", microsofts);
            foreach (string component in new[] { "boost", "Microsoft.Windows.CppWinRT", "microsoft/directxtex", "microsoft/wil" })
            {
                StringAssert.Contains(component, microsofts);
            }

            StringAssert.Contains("ThirdPartyNotices-DirectStorage.txt", Notices());
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
