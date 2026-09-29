using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Puts ThirdPartyNotices.txt, from the project folder, next to the game's
    /// executable in every Windows build.
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

        public int callbackOrder => 0;

        private static string Source =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), FileName);

        private static bool IsWindows(BuildReport report) =>
            report.summary.platform == BuildTarget.StandaloneWindows64 ||
            report.summary.platform == BuildTarget.StandaloneWindows;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (IsWindows(report) && !File.Exists(Source))
            {
                throw new BuildFailedException(
                    FileName + " is missing from the project folder. The FSR, DLSS and font " +
                    "licences need their notices shipped with the game.");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsWindows(report))
            {
                return;
            }

            string buildFolder = Path.GetDirectoryName(report.summary.outputPath);
            File.Copy(Source, Path.Combine(buildFolder, FileName), overwrite: true);
        }
    }
}
