using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The grade (environment roadmap, item 48). PictureGrade is in the editor
    /// assembly, which this one does not see, so these read what it made: the
    /// default profile, and the two saved scenes for their cameras.
    /// </summary>
    public class PictureGradeTests
    {
        private const string ProfilePath = "Assets/Settings/DefaultSettingsVolumeProfile.asset";
        private const string Rerun = " Run Survival Chaos > Environment > Apply Picture Grade.";

        private static T Override<T>() where T : VolumeComponent
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsNotNull(profile, ProfilePath);
            Assert.IsTrue(profile.TryGet(out T component), ProfilePath + " has no " + typeof(T).Name + ".");
            return component;
        }

        /// <summary>
        /// What HDRP's ACES does to grey: the luminance fit in the core
        /// package's ACES.hlsl, then its gamma for a dim room.
        /// </summary>
        private static float AcesOnGrey(float x)
        {
            const float a = 278.5085f, b = 10.7772f, c = 293.6045f, d = 88.7122f, e = 80.6889f;
            return Mathf.Pow(x * (a * x + b) / (x * (c * x + d) + e), 0.9811f);
        }

        private static float Shown(float linear)
        {
            return Mathf.LinearToGammaSpace(Mathf.Clamp01(linear));
        }

        private static HableCurve Curve(Tonemapping tonemapping)
        {
            HableCurve curve = new HableCurve();
            curve.Init(tonemapping.toeStrength.value, tonemapping.toeLength.value, tonemapping.shoulderStrength.value,
                tonemapping.shoulderLength.value, tonemapping.shoulderAngle.value, tonemapping.gamma.value);
            return curve;
        }

        /// <summary>
        /// The curve has two jobs. It works on each of red, green and blue in
        /// the screen's own gamut, which is what lets a bright pickup keep its
        /// colour, and only the Custom mode does that with a foot. And on grey
        /// it lands where ACES did, which is the dark look the game was tuned
        /// under: a curve that drifts from it changes every scene at once.
        /// </summary>
        [Test]
        public void TheToneCurve_IsTheCustomOne_AndAsDarkAsAcesOnGrey()
        {
            Tonemapping tonemapping = Override<Tonemapping>();
            Assert.IsTrue(tonemapping.mode.overrideState, "the tone curve's mode is not set." + Rerun);
            Assert.AreEqual(TonemappingMode.Custom, tonemapping.mode.value,
                "ACES bleaches a bright colour and Neutral lifts the shadows." + Rerun);

            HableCurve curve = Curve(tonemapping);

            foreach (float scene in new[] { 0.01f, 0.02f, 0.05f, 0.1f, 0.18f, 0.4f, 1f })
            {
                Assert.AreEqual(Shown(AcesOnGrey(scene)), Shown(curve.Eval(scene)), 6f / 255f,
                    "grey at " + scene + " is not where ACES put it." + Rerun);
            }

            // The shadows on their own, since they are what Neutral loses: it
            // shows this grey nearly three times as bright.
            Assert.That(Shown(curve.Eval(0.02f)) * 255f, Is.LessThan(20f), "the curve has lost its foot, and the dark with it");
        }

        /// <summary>
        /// ACES never reaches white: it flattens out a little under it, and a
        /// bright colour's three channels all crowd up against that ceiling.
        /// This curve gets there, a few stops over scene white.
        /// </summary>
        [Test]
        public void TheToneCurve_ReachesWhite_AFewStopsOverSceneWhite()
        {
            HableCurve curve = Curve(Override<Tonemapping>());
            Assert.That(curve.whitePoint, Is.InRange(4f, 16f));
            Assert.That(curve.Eval(curve.whitePoint * 0.999f), Is.GreaterThan(0.99f));
        }

        /// <summary>
        /// Cool shadows, warm highlights, and not much of either. Mid grey is
        /// no tint. The frame is mostly shadow, so a strong shadow colour
        /// would repaint the island: at this one its dark grass already leans
        /// towards teal.
        /// </summary>
        [Test]
        public void SplitToning_CoolsTheShadows_AndWarmsTheHighlights_Lightly()
        {
            SplitToning toning = Override<SplitToning>();
            Color shadows = toning.shadows.value;
            Color highlights = toning.highlights.value;

            Assert.IsTrue(toning.shadows.overrideState && toning.highlights.overrideState, "split toning is not set." + Rerun);
            Assert.That(shadows.b - shadows.r, Is.InRange(0.08f, 0.3f), "the shadows are not cool, or are too blue");
            Assert.That(highlights.r - highlights.b, Is.InRange(0.06f, 0.3f), "the highlights are not warm, or are too orange");

            foreach (Color tint in new[] { shadows, highlights })
            {
                Assert.That(tint.g, Is.InRange(0.45f, 0.55f), "a tint with green or magenta in it");
            }

            Assert.AreEqual(0f, toning.balance.value, 20f, "the balance gives nearly the whole picture to one of the two colours");
        }

        /// <summary>
        /// Enemies come in from every side, so the corners may dim but must
        /// not hide a ship.
        /// </summary>
        [Test]
        public void TheVignette_IsThere_AndLight()
        {
            Vignette vignette = Override<Vignette>();
            Assert.IsTrue(vignette.intensity.overrideState, "the vignette is not set." + Rerun);
            Assert.AreEqual(VignetteMode.Procedural, vignette.mode.value);
            Assert.That(vignette.intensity.value, Is.InRange(0.1f, 0.25f));
            Assert.That(vignette.smoothness.value, Is.InRange(0.2f, 0.6f));
        }

        /// <summary>
        /// The picture is mostly dark gradients, of fog and of sky, and eight
        /// bits show those as bands unless the camera dithers. The tick is on
        /// the camera, so each scene has its own to lose.
        /// </summary>
        [TestCase("Assets/Scenes/Game.unity")]
        [TestCase("Assets/Scenes/Menu.unity")]
        public void EveryCamera_Dithers(string scenePath)
        {
            string text = File.ReadAllText(scenePath);
            Assert.That(Regex.Matches(text, @"\n  dithering: 1\r?\n").Count, Is.GreaterThanOrEqualTo(1), scenePath + " has no camera that dithers");
            Assert.AreEqual(0, Regex.Matches(text, @"\n  dithering: 0\r?\n").Count, "a camera in " + scenePath + " does not dither");
        }
    }
}
