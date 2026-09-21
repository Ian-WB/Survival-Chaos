using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The camera presets: that the three meant to differ only in their lens
    /// really do frame the band the same, that the whole-band ones fit the whole
    /// band, and that the far lens does what it is for.
    /// </summary>
    public class CameraFramingTests
    {
        private const float Lane = 18.72f;

        [Test]
        public void TheClassicCamera_ShowsAbout7Point7OfTheBand()
        {
            Assert.AreEqual(7.67f, CameraFraming.ClassicHeight, 0.01f);
        }

        [Test]
        public void FieldOfViewFor_IsTheInverseOfVisibleHeight()
        {
            foreach (float distance in new[] { 3f, 9f, 20f })
            {
                float fov = CameraFraming.FieldOfViewFor(distance, 6f);
                Assert.AreEqual(6f, CameraFraming.VisibleHeight(distance, fov), 1e-3f);
            }
        }

        /// <summary>
        /// Close, Middle and Far show the band exactly as tall as it has always
        /// been shown, so comparing them compares the lens and nothing else.
        /// </summary>
        [Test]
        public void TheLensPresets_AllFrameTheBandTheSame()
        {
            for (int i = 0; i < 3; i++)
            {
                CameraPreset preset = CameraFraming.Presets[i];
                Assert.IsFalse(preset.HoldsBandMiddle, preset.Name);
                Assert.AreEqual(CameraFraming.ClassicHeight,
                    CameraFraming.VisibleHeight(preset.Distance, preset.FieldOfView), 1e-3f, preset.Name);
            }
        }

        [Test]
        public void TheLensPresets_GetTighterAsTheyGetFurther()
        {
            for (int i = 1; i < 3; i++)
            {
                Assert.Greater(CameraFraming.Presets[i].Distance, CameraFraming.Presets[i - 1].Distance);
                Assert.Less(CameraFraming.Presets[i].FieldOfView, CameraFraming.Presets[i - 1].FieldOfView);
            }
        }

        [Test]
        public void EveryWholeBandPreset_FitsTheWholeBandWithRoomToSpare()
        {
            int wholeBand = 0;

            foreach (CameraPreset preset in CameraFraming.Presets)
            {
                if (!preset.HoldsBandMiddle)
                {
                    continue;
                }

                wholeBand++;
                Assert.AreEqual(CameraFraming.WholeBandHeight,
                    CameraFraming.VisibleHeight(preset.Distance, preset.FieldOfView), 1e-3f, preset.Name);
                Assert.Greater(CameraFraming.WholeBandHeight, CameraFraming.BandHeight + 1f);
            }

            Assert.AreEqual(CameraFraming.WholeBandDistances.Length, wholeBand);
        }

        [Test]
        public void TheWholeBandPresets_GetTighterAsTheyGetFurther()
        {
            for (int i = 4; i < CameraFraming.Presets.Length; i++)
            {
                Assert.Greater(CameraFraming.Presets[i].Distance, CameraFraming.Presets[i - 1].Distance);
                Assert.Less(CameraFraming.Presets[i].FieldOfView, CameraFraming.Presets[i - 1].FieldOfView);
            }
        }

        /// <summary>Four presets in a row are told apart by name, so no two may share one.</summary>
        [Test]
        public void EveryPreset_HasItsOwnName()
        {
            var names = new System.Collections.Generic.HashSet<string>();

            foreach (CameraPreset preset in CameraFraming.Presets)
            {
                Assert.IsTrue(names.Add(preset.Name), preset.Name);
            }
        }

        /// <summary>
        /// What the far lens is for: ten units round the ring, the close camera
        /// draws things at two thirds size, and the far one at five sixths.
        /// </summary>
        [Test]
        public void DepthRatio_TenUnitsRoundTheRing()
        {
            Assert.Greater(CameraFraming.DepthRatio(CameraFraming.ClassicDistance, 10f, Lane), 1.5f);
            Assert.Less(CameraFraming.DepthRatio(CameraFraming.FarDistance, 10f, Lane), 1.25f);
            Assert.AreEqual(1f, CameraFraming.DepthRatio(5f, 0f, Lane), 1e-5f);
        }
    }
}
