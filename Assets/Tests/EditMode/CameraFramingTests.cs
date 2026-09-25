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

        /// <summary>
        /// The camera a run starts on, picked by playing on 24 September 2026.
        /// Pinned so that reordering the list cannot quietly move the default to
        /// a neighbour.
        /// </summary>
        [Test]
        public void TheDefault_IsTheWholeBandFromTenOut()
        {
            CameraPreset preset = CameraFraming.Default;

            Assert.AreEqual(10f, preset.Distance);
            Assert.IsTrue(preset.HoldsBandMiddle);
            Assert.AreEqual(54.04f, preset.FieldOfView, 0.01f);
            Assert.AreEqual("Whole band, 10 out", preset.Name);
        }

        /// <summary>PlayerBounds in the Game scene on 25 September 2026, 2.72 to 11.62.</summary>
        private const float LiveBandHeight = 11.618166f - 2.721233f;

        /// <summary>
        /// Since 25 September 2026 the whole-band presets frame the band's live
        /// height, so a taller or shorter PlayerBounds is still framed whole,
        /// margin and all, from the same distance.
        /// </summary>
        [TestCase(6f)]
        [TestCase(8.9f)]
        [TestCase(12f)]
        public void EveryWholeBandPreset_FramesTheBandItIsGiven(float bandHeight)
        {
            foreach (CameraPreset preset in CameraFraming.Presets)
            {
                if (!preset.HoldsBandMiddle)
                {
                    continue;
                }

                Assert.AreEqual(bandHeight + 2f * CameraFraming.BandMargin,
                    CameraFraming.VisibleHeight(preset.Distance, preset.FieldOfViewOn(bandHeight)), 1e-3f, preset.Name);
            }
        }

        [Test]
        public void ATallerBand_WidensTheLens()
        {
            CameraPreset preset = CameraFraming.Default;

            Assert.Greater(preset.FieldOfViewOn(12f), preset.FieldOfViewOn(8.9f));
            Assert.Less(preset.FieldOfViewOn(6f), preset.FieldOfViewOn(8.9f));
        }

        /// <summary>
        /// On the band as it is the default keeps the lens it was picked at: the
        /// live band is 8.897 against the table's 8.9, a fiftieth of a degree.
        /// </summary>
        [Test]
        public void OnTheBandAsItIs_TheDefaultKeepsItsLens()
        {
            Assert.AreEqual(CameraFraming.Default.FieldOfView,
                CameraFraming.Default.FieldOfViewOn(LiveBandHeight), 0.05f);
        }

        /// <summary>
        /// The lens presets frame the ship as the classic camera did, not the
        /// band, so the band's height leaves them alone.
        /// </summary>
        [Test]
        public void TheLensPresets_KeepTheirLens_WhateverTheBand()
        {
            for (int i = 0; i < 3; i++)
            {
                CameraPreset preset = CameraFraming.Presets[i];
                Assert.AreEqual(preset.FieldOfView, preset.FieldOfViewOn(12f), preset.Name);
                Assert.AreEqual(preset.FieldOfView, preset.FieldOfViewOn(6f), preset.Name);
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
