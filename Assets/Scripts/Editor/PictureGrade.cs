using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The grade: how a finished frame is turned into what the screen shows
    /// (environment roadmap, item 48), held in one place.
    ///
    /// Until 8 October 2026 the picture went through ACES with every grading
    /// control at rest. ACES does two things at once. It gives the frame its
    /// dark, contrasty look: scene grey lands well under where a plain curve
    /// would put it, and the shadows are pressed down hard. And it does its
    /// work in a wider gamut than the screen's, which is what washed the
    /// colour out of anything bright: a yellow pickup came out white with a
    /// yellow rim, a magenta one pale pink, the ship's green rounds mint.
    ///
    /// The first half is the game's look and the second is a fault, so the
    /// curve here keeps one and drops the other. It is HDRP's Custom curve,
    /// fitted to what ACES does to grey (within 4 steps of 255 from black up
    /// to scene white) and applied to red, green and blue each on their own,
    /// in the screen's own gamut. Measured on one frozen frame, the middle of
    /// the picture is as bright as it was to within 4%, while the bright
    /// pixels of a Dash pickup went from 0.34 saturated to 0.71 and the
    /// ship's rounds from 0.21 to 0.47. HDRP's Neutral curve keeps the colour
    /// too, and was tried first: it lifts the darkest twentieth of the frame
    /// from 9 to 36 and the look is gone.
    ///
    /// Two things change with it that nobody asked for by name. The lava is a
    /// deeper orange, since its hottest parts used to bleach towards cream.
    /// And nothing dim changes at all: the enemies' violet rounds, kept under
    /// a peak of 2 by HostileFireColour because ACES turned them pink above
    /// it, measured the same before and after to within a few steps of 255.
    ///
    /// Split toning leans the shadows blue and the highlights warm. The frame
    /// is mostly shadow, so the shadow colour is the one that shows: the dark
    /// sky goes from teal towards blue, and dark grass with it. The moon sits
    /// between the two and keeps the colour Ian chose for its light.
    ///
    /// The vignette is light on purpose. Enemies come in from every side, and
    /// at 0.2 a corner is about a seventh darker while the middle third of
    /// the frame does not change.
    ///
    /// Dithering is the fourth part and is not set here: it is a tick on each
    /// scene's camera, and PictureGradeTests holds both.
    ///
    /// All of it is in the default profile, so the menu is graded as the game
    /// is. The HUD is drawn over the finished picture and is not touched.
    ///
    /// Re-running is safe and idempotent.
    /// </summary>
    public static class PictureGrade
    {
        public const string ProfilePath = "Assets/Settings/DefaultSettingsVolumeProfile.asset";

        /// <summary>
        /// The curve's foot: how hard the shadows are pressed down, and how far
        /// up the curve the pressing reaches. These two are the dark look. With
        /// both at 0 the curve has no foot, and the picture washes out as it
        /// does under Neutral.
        /// </summary>
        public const float ToeStrength = 0.34f;
        public const float ToeLength = 0.57f;

        /// <summary>
        /// The curve's top: how soon the highlights start to flatten, how many
        /// stops they are given to do it in, and how much they overshoot. The
        /// white point comes out at 8, where ACES never quite reaches white.
        /// </summary>
        public const float ShoulderStrength = 0.91f;
        public const float ShoulderLength = 3f;
        public const float ShoulderAngle = 0.12f;
        public const float Gamma = 1f;

        /// <summary>
        /// Split toning's two colours, where mid grey is no tint at all. The
        /// distance from grey is the strength: half of this could hardly be
        /// seen in a side-by-side.
        /// </summary>
        public static readonly Color Shadows = new Color(0.42f, 0.50f, 0.60f, 1f);
        public static readonly Color Highlights = new Color(0.58f, 0.52f, 0.44f, 1f);
        public const float Balance = 0f;

        /// <summary>
        /// How dark the corners go, and how far in the darkening starts. This
        /// is the one to lower if a ship at the edge of the frame is hard to
        /// pick out.
        /// </summary>
        public const float VignetteIntensity = 0.2f;
        public const float VignetteSmoothness = 0.4f;

        [MenuItem("Survival Chaos/Environment/Apply Picture Grade", priority = 117)]
        public static void Apply()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"Not found: {ProfilePath}");
                return;
            }

            if (!profile.TryGet(out Tonemapping tonemapping) || !profile.TryGet(out SplitToning splitToning) ||
                !profile.TryGet(out Vignette vignette))
            {
                Debug.LogWarning($"{ProfilePath} has lost its Tonemapping, Split Toning or Vignette override.");
                return;
            }

            tonemapping.mode.Override(TonemappingMode.Custom);
            tonemapping.toeStrength.Override(ToeStrength);
            tonemapping.toeLength.Override(ToeLength);
            tonemapping.shoulderStrength.Override(ShoulderStrength);
            tonemapping.shoulderLength.Override(ShoulderLength);
            tonemapping.shoulderAngle.Override(ShoulderAngle);
            tonemapping.gamma.Override(Gamma);

            splitToning.shadows.Override(Shadows);
            splitToning.highlights.Override(Highlights);
            splitToning.balance.Override(Balance);

            vignette.mode.Override(VignetteMode.Procedural);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);

            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(splitToning);
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(profile);

            // Not SaveAssets: the scene's own profile is tuned live in the
            // Inspector, and that would write whatever is half-done in it.
            AssetDatabase.SaveAssetIfDirty(profile);

            // The pipeline keeps its own copy of the default profile's values.
            if (VolumeManager.instance.isInitialized)
            {
                VolumeManager.instance.OnVolumeProfileChanged(profile);
            }

            Debug.Log("Picture grade applied: the custom tone curve, split toning and a vignette of " +
                      $"{VignetteIntensity:0.##}. Dithering is a tick on each scene's camera.");
        }
    }
}
