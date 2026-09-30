using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Writes <see cref="BuildStamp"/> into the build: the build folder's name
    /// and the commit, with a "+" when the working tree had uncommitted changes.
    ///
    /// The file exists for the length of the build only. It is written before
    /// the build starts and deleted once it finishes, and it is gitignored in
    /// case a failed build never reaches the clean-up. The editor never reads
    /// it either way (see BuildStamp).
    /// </summary>
    public sealed class BuildStampWriter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string AssetPath = "Assets/Resources/" + BuildStamp.ResourcePath + ".txt";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string stamp = Compose(report.summary.outputPath);
            File.WriteAllText(AssetPath, stamp + "\n");
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Build stamp: " + stamp);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            AssetDatabase.DeleteAsset(AssetPath);
        }

        /// <summary>
        /// "26_0929-1431_newDrivers  ·  6fd5d30+". The folder is the one holding
        /// the executable, which is what the builds are named by.
        /// </summary>
        internal static string Compose(string outputPath)
        {
            string folder = string.IsNullOrEmpty(outputPath)
                ? "unnamed"
                : Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            string commit = Git("rev-parse --short HEAD");
            if (string.IsNullOrEmpty(commit))
            {
                return folder;
            }

            // Anything in the status is a change the commit does not hold, and
            // untracked files count: a new script is a change. ProjectSettings
            // does not, because a build switches settings of its own on the way
            // out - the code variant for the debug menu, run in background -
            // and every build would carry the mark.
            string status = Git("status --porcelain -- . \":(exclude)ProjectSettings\"");
            return folder + "  ·  " + commit + (string.IsNullOrEmpty(status) ? "" : "+");
        }

        /// <summary>Runs git in the project folder. Empty when git is missing or fails.</summary>
        private static string Git(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process git = Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd();
                    git.WaitForExit(5000);
                    return git.ExitCode == 0 ? output.Trim() : string.Empty;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
