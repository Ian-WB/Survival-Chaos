using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Turns "run in background" off for every build, because the project setting
    /// will not stay off on its own.
    ///
    /// com.unity.pipeline runs an HTTP server inside the Editor so the unity CLI
    /// can drive it, and that server sets <c>Application.runInBackground = true</c>
    /// every time it starts - BasePipelineServer.Start(), which runs on every Editor
    /// launch and after every domain reload. In the Editor that assignment writes
    /// through to PlayerSettings, so the value in ProjectSettings.asset is true no
    /// matter what anyone sets it to. Setting it back by hand lasts until the next
    /// launch, and then it is true again.
    ///
    /// That would be harmless if it stayed in the Editor, but PlayerSettings is
    /// baked into the build. The player would inherit the setting from a server that
    /// never runs in it: the runtime half of the pipeline is opt-in through a
    /// RuntimePipelineConfig asset in a Resources folder, which this project does
    /// not have and should not gain - its own tooltip says never to enable it in a
    /// production build.
    ///
    /// It matters because this game has no pause. Left on, alt-tabbing does not
    /// stop the run - enemies keep closing, waves keep arriving, and the player
    /// comes back to a death they were not present for.
    ///
    /// Forcing it here rather than in the game keeps the workaround where the
    /// problem is, in an Editor package, instead of shipping a line of runtime code
    /// whose only job is to undo something the Editor did.
    /// </summary>
    public sealed class RunInBackgroundOff : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Already off means the pipeline server has not started since someone
            // last cleared it. Nothing to do, and nothing worth logging about.
            if (!PlayerSettings.runInBackground)
            {
                return;
            }

            PlayerSettings.runInBackground = false;

            Debug.Log(
                "Build: forced PlayerSettings.runInBackground off. com.unity.pipeline's Editor " +
                "server sets it on at every startup, so without this it would ship enabled.");
        }
    }
}
