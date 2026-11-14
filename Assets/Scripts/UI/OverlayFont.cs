using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The HUD's typeface for the few things drawn outside the canvases: the
    /// arena intro's lines and the title splash's cards.
    ///
    /// Both are drawn by directors that exist only at run time and so have no
    /// inspector to take a font from, and until 12 November 2026 they drew in
    /// the engine's default label font, which is Arial - beside a HUD and menus
    /// set in Chakra Petch. The font file sits in Resources for this; the HUD's
    /// own font asset points at the same file.
    /// </summary>
    public static class OverlayFont
    {
        /// <summary>Where the font file has to live for Resources.Load to find it.</summary>
        public const string ResourcePath = "Fonts/ChakraPetch-Medium";

        private static Font cached;
        private static bool looked;

        /// <summary>The HUD's font, or null when the file is missing; a null font draws in the default.</summary>
        public static Font Get()
        {
            if (!looked)
            {
                looked = true;
                cached = Resources.Load<Font>(ResourcePath);

                if (cached == null)
                {
                    Debug.LogWarning("No font at Resources/" + ResourcePath + ", so the intro and the " +
                                     "title splash draw in the default font.");
                }
            }

            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            looked = false;
        }

        /// <summary>
        /// A label style in the HUD's font at a size that follows the screen:
        /// <paramref name="share"/> of its height, kept between two limits.
        /// Sized on every call, because the resolution can change in the options.
        /// </summary>
        public static void Fit(GUIStyle style, float share, int smallest, int largest)
        {
            style.font = Get();
            style.fontSize = Size(Screen.height, share, smallest, largest);
        }

        /// <summary>The font size for a screen <paramref name="height"/> pixels tall.</summary>
        public static int Size(float height, float share, int smallest, int largest)
        {
            return Mathf.Clamp(Mathf.RoundToInt(height * share), smallest, largest);
        }
    }
}
