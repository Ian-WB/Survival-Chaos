using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Puts ThirdPartyNotices.txt, from the project folder, next to the game's
    /// executable in every Windows build, and with it Microsoft's own notices
    /// for what DirectStorage is built from.
    ///
    /// Every Windows build carries AMD's FSR DLLs, NVIDIA's DLSS DLL and two OFL
    /// fonts, and their licences ask for their notices to go with every copy.
    /// Beside the exe is where a player, or anyone checking, looks for them. A
    /// missing file stops the build before it starts rather than letting it ship
    /// without the notices. <c>ThirdPartyNoticesTests</c> checks what is in it.
    /// </summary>
    public sealed class ThirdPartyNotices : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string FileName = "ThirdPartyNotices.txt";

        /// <summary>
        /// The NOTICES.txt of Microsoft's DirectStorage package, as it came.
        /// Unity ships DirectStorage's two DLLs in every Windows build whether
        /// or not the project uses it, and Boost, C++/WinRT, DirectXTex and
        /// WIL are compiled into them. It is the file for DirectStorage 1.3.0;
        /// an engine update that brings a newer one wants the newer file,
        /// from the Microsoft.Direct3D.DirectStorage package on NuGet.
        /// </summary>
        public const string DirectStorageFileName = "ThirdPartyNotices-DirectStorage.txt";

        public static readonly string[] FileNames = { FileName, DirectStorageFileName };

        public int callbackOrder => 0;

        private static string SourceOf(string file) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), file);

        private static bool IsWindows(BuildReport report) =>
            report.summary.platform == BuildTarget.StandaloneWindows64 ||
            report.summary.platform == BuildTarget.StandaloneWindows;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsWindows(report))
            {
                return;
            }

            foreach (string file in FileNames)
            {
                if (!File.Exists(SourceOf(file)))
                {
                    throw new BuildFailedException(
                        file + " is missing from the project folder. The licences of what ships " +
                        "with the game need their notices shipped with it.");
                }
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsWindows(report))
            {
                return;
            }

            string buildFolder = Path.GetDirectoryName(report.summary.outputPath);
            foreach (string file in FileNames)
            {
                File.Copy(SourceOf(file), Path.Combine(buildFolder, file), overwrite: true);
            }
        }
    }
}
