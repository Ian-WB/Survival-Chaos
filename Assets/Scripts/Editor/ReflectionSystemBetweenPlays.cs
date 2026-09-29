using System;
using UnityEditor;
using UnityEngine.Experimental.Rendering;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Hands the engine's own reflection probe system back when play mode ends,
    /// so that entering play again does not log a warning every time.
    ///
    /// HDRP installs its reflection system from a SubsystemRegistration method,
    /// on every entry into play, and the engine warns when one custom system is
    /// assigned over another: "ScriptableRuntimeReflectionSystemSettings.system
    /// is assigned more than once". Nothing ever took HDRP's out again except a
    /// domain reload, which entering play no longer does since 29 September 2026.
    /// So from the second play of an editor session on, HDRP found its last
    /// instance still installed and said so. Harmless, as the new one replaces
    /// the old, but it was the one warning on every Play.
    ///
    /// The built-in system is what the editor has after any domain reload, so
    /// this leaves edit mode as a script recompile would. The type is internal,
    /// hence finding it by name, as HDRP does; if an upgrade renames it this
    /// does nothing, and that warning coming back is the sign to look here.
    /// </summary>
    [InitializeOnLoad]
    internal static class ReflectionSystemBetweenPlays
    {
        private const string BuiltinSystem =
            "UnityEngine.Experimental.Rendering.BuiltinRuntimeReflectionSystem, UnityEngine";

        static ReflectionSystemBetweenPlays()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Restore();
                }
            };
        }

        private static void Restore()
        {
            Type builtin = Type.GetType(BuiltinSystem);
            if (builtin == null || ScriptableRuntimeReflectionSystemSettings.system?.GetType() == builtin)
            {
                return;
            }

            // The setter disposes the system it replaces.
            ScriptableRuntimeReflectionSystemSettings.system =
                (IScriptableRuntimeReflectionSystem)Activator.CreateInstance(builtin);
        }
    }
}
