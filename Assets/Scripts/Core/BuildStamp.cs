using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Which build this is: the build folder's name and the commit it was made
    /// from, as in "26_0929-1431_newDrivers  ·  6fd5d30". A "+" after the
    /// commit means the build had changes nobody had committed yet.
    ///
    /// On 29 September 2026 a build was credited with a change committed 14
    /// minutes after it was made, and nothing on screen could have said so.
    /// The title screen and the pause menu show this now, so every screenshot
    /// says which build it came from.
    ///
    /// Written into Resources by a build step (BuildStampWriter) for the length
    /// of the build, and read once. The editor has no stamp: it is whatever is
    /// in the working tree.
    /// </summary>
    public static class BuildStamp
    {
        /// <summary>Where the build step writes it, for Resources.Load.</summary>
        public const string ResourcePath = "BuildStamp";

        private static string cached;

        public static string Text
        {
            get
            {
                if (cached != null)
                {
                    return cached;
                }

#if UNITY_EDITOR
                // A failed build can leave a stamp behind, and the editor showing
                // the name of a build it is not would be the very mistake this is for.
                cached = "EDITOR";
#else
                TextAsset stamp = Resources.Load<TextAsset>(ResourcePath);
                cached = stamp != null ? stamp.text.Trim() : "UNSTAMPED BUILD";
#endif
                return cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            cached = null;
        }
    }
}
