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
        /// The camera a run starts on: 9 out at 65 degrees, asked for as those
        /// two numbers on 5 October 2026. Pinned so that reordering the list
        /// cannot quietly move the default to a neighbour, and so that the
        /// margin or the table's band cannot move the lens off what was asked.
        /// </summary>
        [Test]
        public void TheDefault_IsTheWholeBandFromNineOut_At65Degrees()
        {
            CameraPreset preset = CameraFraming.Default;

            Assert.AreEqual(9f, preset.Distance);
            Assert.IsTrue(preset.HoldsBandMiddle);
            Assert.AreEqual(65f, preset.FieldOfView, 0.05f);
            Assert.AreEqual("Whole band, 9 out", preset.Name);
        }

        /// <summary>
        /// The lens follows the band's live height, so the 65 degrees is only
        /// true while PlayerBounds in the Game scene is the band the table is
        /// written for. Resizing the box moves the lens, and this says so.
        /// </summary>
        [Test]
        public void OnTheGameScenesBand_TheDefaultIs65Degrees()
        {
            SavedScene.Load("Assets/Scenes/Game.unity").Band("Player", out float floor, out float ceiling);

            Assert.AreEqual(CameraFraming.BandHeight, ceiling - floor, 0.01f);
            Assert.AreEqual(65f, CameraFraming.Default.FieldOfViewOn(ceiling - floor), 0.05f);
        }

        /// <summary>PlayerBounds when the default was picked, 3.4225 to 12.6525.</summary>
        private const float PickedBandHeight = 12.6525f - 3.4225f;

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

                Assert.AreEqual(CameraFraming.WholeBandHeightOf(bandHeight),
                    CameraFraming.VisibleHeight(preset.Distance, preset.FieldOfViewOn(bandHeight)), 1e-3f, preset.Name);
            }
        }

        /// <summary>The boss bar's lower edge, down from the top of a 16:9 screen, measured 25 September 2026.</summary>
        private const float BossBarShare = 0.081f;

        /// <summary>
        /// Half the ship's drawn height: its renderers span 0.33, measured on
        /// the prefab on 5 October 2026. The hull alone is 0.26.
        /// </summary>
        private const float ShipHalfHeight = 0.165f;

        /// <summary>
        /// Where a whole-band preset draws a height, as a share of the screen
        /// from the bottom, for a band from 0 to <paramref name="bandHeight"/>.
        /// </summary>
        private static float OnScreen(float height, float bandHeight)
        {
            float camera = 0.5f * bandHeight;
            return 0.5f + ((height - camera) / CameraFraming.WholeBandHeightOf(bandHeight));
        }

        /// <summary>
        /// 25 September 2026: a ship on the ceiling was drawn 6.4% down the
        /// screen, under the countdown and the boss bar, which sit across the
        /// top at the same place left to right as the ship. The frame now leaves
        /// the HUD its share and a ship on the ceiling clears the lower of them.
        /// 8.615 is the band Ian had in the scene when he saw it, 10.5 the one
        /// he gave the bigger ships and boss that evening, and 9.23 the one
        /// the 9-out camera has had since 5 October.
        /// </summary>
        [TestCase(6f)]
        [TestCase(8.615f)]
        [TestCase(8.9f)]
        [TestCase(9.23f)]
        [TestCase(10.5f)]
        [TestCase(12f)]
        public void AShipOnTheCeiling_IsDrawnBelowTheTopHud(float bandHeight)
        {
            float ceiling = OnScreen(bandHeight, bandHeight);
            float frame = CameraFraming.WholeBandHeightOf(bandHeight);

            Assert.Less(ceiling + ShipHalfHeight / frame, 1f - BossBarShare);
            // The same clearance under the HUD as the floor has over the screen's edge.
            Assert.AreEqual(CameraFraming.BandMargin / frame, 1f - CameraFraming.TopHudShare - ceiling, 1e-4f);
        }

        /// <summary>
        /// Asked for the same day: the room above the ceiling and below the
        /// floor are equal, so the band sits in the middle of the screen.
        /// </summary>
        [TestCase(6f)]
        [TestCase(8.615f)]
        [TestCase(8.9f)]
        [TestCase(10.5f)]
        [TestCase(12f)]
        public void TheRoomAboveAndBelowTheBand_IsEqual(float bandHeight)
        {
            float floor = OnScreen(0f, bandHeight);
            float ceiling = OnScreen(bandHeight, bandHeight);

            Assert.AreEqual(floor, 1f - ceiling, 1e-4f);
            Assert.AreEqual(CameraFraming.TopHudShare + CameraFraming.BandMargin / CameraFraming.WholeBandHeightOf(bandHeight),
                floor, 1e-4f);
        }

        [Test]
        public void ATallerBand_WidensTheLens()
        {
            CameraPreset preset = CameraFraming.Default;

            Assert.Greater(preset.FieldOfViewOn(12f), preset.FieldOfViewOn(8.9f));
            Assert.Less(preset.FieldOfViewOn(6f), preset.FieldOfViewOn(8.9f));
        }

        /// <summary>
        /// On the band it was picked on the default keeps the lens it was picked
        /// at.
        /// </summary>
        [Test]
        public void OnTheBandItWasPickedOn_TheDefaultKeepsItsLens()
        {
            Assert.AreEqual(CameraFraming.Default.FieldOfView,
                CameraFraming.Default.FieldOfViewOn(PickedBandHeight), 0.05f);
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
        /// The debug menu's distance slider, left alone by the lens slider: a
        /// whole-band preset still shows the whole band from wherever it is
        /// put, and at its own distance it is the preset.
        /// </summary>
        [Test]
        public void MovedByTheSlider_AWholeBandPresetStillFitsTheBand()
        {
            CameraPreset preset = CameraFraming.Default;

            foreach (float distance in new[] { CameraFraming.MinDistance, 8.5f, 17f, CameraFraming.MaxDistance })
            {
                float lens = CameraFraming.Lens(preset, distance, 10.5f, 0f);
                Assert.AreEqual(CameraFraming.WholeBandHeightOf(10.5f),
                    CameraFraming.VisibleHeight(distance, lens), 1e-3f, distance.ToString());
            }

            Assert.AreEqual(preset.FieldOfViewOn(10.5f),
                CameraFraming.Lens(preset, preset.Distance, 10.5f, 0f), 1e-4f);
        }

        /// <summary>
        /// The lens slider wins over the preset and over the band, inside its
        /// own range; a lens preset moved by the distance slider keeps its lens.
        /// </summary>
        [Test]
        public void ALensChosenOnTheSlider_IsTheLens()
        {
            foreach (CameraPreset preset in CameraFraming.Presets)
            {
                Assert.AreEqual(40f, CameraFraming.Lens(preset, 12f, 10.5f, 40f), preset.Name);
                Assert.AreEqual(CameraFraming.MaxFieldOfView, CameraFraming.Lens(preset, 12f, 10.5f, 170f), preset.Name);

                if (!preset.HoldsBandMiddle)
                {
                    Assert.AreEqual(preset.FieldOfView, CameraFraming.Lens(preset, 12f, 10.5f, 0f), preset.Name);
                }
            }
        }

        /// <summary>
        /// Every preset is somewhere both sliders can reach, so opening the
        /// menu never moves the camera by clamping it.
        /// </summary>
        [Test]
        public void EveryPreset_IsInsideTheSlidersRange()
        {
            foreach (CameraPreset preset in CameraFraming.Presets)
            {
                Assert.That(preset.Distance, Is.InRange(CameraFraming.MinDistance, CameraFraming.MaxDistance), preset.Name);
                Assert.That(preset.FieldOfViewOn(10.5f), Is.InRange(CameraFraming.MinFieldOfView, CameraFraming.MaxFieldOfView), preset.Name);
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
