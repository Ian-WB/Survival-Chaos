using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Turns "run in background" off for every build, and deliberately leaves it
    /// on in the Editor.
    ///
    /// com.unity.pipeline runs an HTTP server inside the Editor so the unity CLI
    /// can drive it, and that server sets <c>Application.runInBackground = true</c>
    /// when it starts - BasePipelineServer.Start(). In the Editor that assignment
    /// writes through to PlayerSettings, which is why ProjectSettings.asset carries
    /// 1 and is committed that way.
    ///
    /// The Editor value is wanted, and that is the part worth writing down. An
    /// unfocused Editor that has stopped updating cannot be driven, so recompiling,
    /// running tests and entering play mode from the CLI all depend on this being
    /// on while the person working is in another window. It is not damage from the
    /// package to be tolerated; it is the setting that makes the tooling usable.
    ///
    /// It does not restore itself. That was assumed once and is not so: a build
    /// clears it through this callback and it stays clear across a domain reload
    /// afterwards, leaving ProjectSettings.asset modified against the committed 1.
    /// Turn it back on in Project Settings, or through PlayerSettings, after any
    /// build that needs the CLI working again.
    ///
    /// What must not inherit it is the player. PlayerSettings is baked into the
    /// build, so without this the game would ship carrying a setting it got from a
    /// server that never runs in it: the runtime half of the pipeline is opt-in
    /// through a RuntimePipelineConfig asset in a Resources folder, which this
    /// project does not have and should not gain - its own tooltip says never to
    /// enable it in a production build.
    ///
    /// It matters because the game does not pause itself. PauseMenu waits for Esc,
    /// so left on, alt-tabbing does not stop the run - enemies keep closing, waves
    /// keep arriving, and the player comes back to a death they were not present
    /// for.
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
            // Already off means an earlier build cleared it and nothing has turned
            // it back on since. Nothing to do, and nothing worth logging about.
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
