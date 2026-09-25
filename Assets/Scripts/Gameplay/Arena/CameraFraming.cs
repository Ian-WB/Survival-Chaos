using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One way of placing the camera: how far outside the lane it sits, how
    /// wide it sees, and whether it follows the ship up and down.
    /// </summary>
    public readonly struct CameraPreset
    {
        public readonly string Name;

        /// <summary>How far outside the lane the camera sits, in world units.</summary>
        public readonly float Distance;

        /// <summary>Vertical field of view, in degrees.</summary>
        public readonly float FieldOfView;

        /// <summary>
        /// Whether the camera holds the middle of the band instead of climbing
        /// and diving with the ship.
        /// </summary>
        public readonly bool HoldsBandMiddle;

        public CameraPreset(string name, float distance, float fieldOfView, bool holdsBandMiddle)
        {
            Name = name;
            Distance = distance;
            FieldOfView = fieldOfView;
            HoldsBandMiddle = holdsBandMiddle;
        }

        /// <summary>
        /// The field of view to use on a band <paramref name="bandHeight"/> tall.
        /// A whole-band preset frames the band it is given, so a taller band
        /// widens its lens and a shorter one tightens it; <see cref="FieldOfView"/>
        /// is that lens on <see cref="CameraFraming.BandHeight"/>. The lens
        /// presets frame the ship as the classic camera did and keep theirs.
        /// </summary>
        /// <remarks>
        /// The lens changes rather than the distance because the distance is
        /// what each preset was picked for - how flat the ring looks, and how
        /// early things coming round it can be read - and a band that changes
        /// height should not change that.
        /// </remarks>
        public float FieldOfViewOn(float bandHeight)
        {
            return HoldsBandMiddle
                ? CameraFraming.FieldOfViewFor(Distance, CameraFraming.WholeBandHeightOf(bandHeight))
                : FieldOfView;
        }
    }

    /// <summary>
    /// The camera presets and the arithmetic behind them, with no Unity object
    /// attached so the framing can be tested directly.
    ///
    /// Asked for on 21 September 2026, to settle by playing rather than by
    /// argument whether the camera should sit close with a wide lens, as it
    /// always has, or further back with a tight one. The trade is perspective.
    /// Close and wide, something ten units round the ring is half as far again
    /// from the camera as the ship, so it is drawn two thirds the size and its
    /// height above or below the ship looks two thirds what it is - and nearly
    /// every attack in the boss fight is dodged by reading heights. From 14
    /// out it is a fifth further than the ship, so heights read nearly the
    /// same everywhere on screen; the price is the ring's curve, the boss's looming, and warning
    /// of things coming round the ring from a long way off.
    ///
    /// The first three presets show the band exactly as tall as the close
    /// camera always has, so comparing them compares the lens and nothing else.
    /// The rest frame the whole band and stop following the ship up and down -
    /// a fixed screen the ship moves around, as a side-scrolling shooter has -
    /// from four distances, 7 to 20 outside the lane, each at the field of
    /// view that fits the band - the band as it is live, since 25 September
    /// 2026, so resizing PlayerBounds resizes the view. Closest keeps nearly
    /// the classic lens and its sense of scale; furthest flattens the ring
    /// almost to a corridor.
    ///
    /// Playing settled it on 24 September 2026, on the whole band from 10 out -
    /// see <see cref="DefaultIndex"/>. The other six stay on the debug menu.
    /// </summary>
    public static class CameraFraming
    {
        /// <summary>
        /// The camera as it was until 24 September 2026: 5 outside the lane, 75
        /// degrees. Still the first preset, and the one the lens presets match.
        /// </summary>
        public const float ClassicDistance = 5f;
        public const float ClassicFieldOfView = 75f;

        /// <summary>How much of the band's height the close camera shows at the ship.</summary>
        public static float ClassicHeight => VisibleHeight(ClassicDistance, ClassicFieldOfView);

        /// <summary>
        /// Room shown above the ceiling and below the floor by the whole-band
        /// preset, so a ship on an edge is not drawn on the edge of the screen.
        /// </summary>
        public const float BandMargin = 0.65f;

        /// <summary>
        /// The band's height the preset table is written for: 8.9, the height of
        /// the player's bounds box (4.42 to 13.32, and 2.72 to 11.62 since it
        /// came down on 21 September 2026). The stored lenses
        /// use it; the camera itself frames the live band, through
        /// <see cref="CameraPreset.FieldOfViewOn"/>, so resizing PlayerBounds
        /// resizes the view with it.
        /// </summary>
        public const float BandHeight = 8.9f;

        /// <summary>
        /// How far out the far presets sit. 14 puts something ten units round
        /// the ring only a fifth further away than the ship.
        /// </summary>
        public const float FarDistance = 14f;

        /// <summary>How tall a whole-band preset frames on <see cref="BandHeight"/>.</summary>
        public static float WholeBandHeight => WholeBandHeightOf(BandHeight);

        /// <summary>
        /// How tall a whole-band preset frames, at the ship, on a band
        /// <paramref name="bandHeight"/> tall: the band and a margin each side.
        /// The margin stays the same whatever the band, because it is room for
        /// a ship on the edge and the ship does not change size.
        /// </summary>
        public static float WholeBandHeightOf(float bandHeight)
        {
            return Mathf.Max(0f, bandHeight) + 2f * BandMargin;
        }

        /// <summary>How far out the whole-band presets sit, closest first.</summary>
        public static readonly float[] WholeBandDistances = { 7f, 10f, FarDistance, 20f };

        public static readonly CameraPreset[] Presets =
        {
            new CameraPreset("Close, wide", ClassicDistance, ClassicFieldOfView, false),
            new CameraPreset("Middle", 9f, FieldOfViewFor(9f, ClassicHeight), false),
            new CameraPreset("Far, tight", FarDistance, FieldOfViewFor(FarDistance, ClassicHeight), false),
            WholeBand(WholeBandDistances[0]),
            WholeBand(WholeBandDistances[1]),
            WholeBand(WholeBandDistances[2]),
            WholeBand(WholeBandDistances[3]),
        };

        /// <summary>
        /// The preset every run starts on: the whole band from 10 out, at 54
        /// degrees on the band as it is, picked by playing on 24 September 2026.
        /// CameraPresetSwitcher applies it as a run starts, and the Game scene's
        /// camera is authored at the same distance and lens, so the editor
        /// frames the arena the way the game does.
        /// </summary>
        public const int DefaultIndex = 4;

        /// <summary>The preset every run starts on - see <see cref="DefaultIndex"/>.</summary>
        public static CameraPreset Default => Presets[DefaultIndex];

        /// <summary>
        /// A preset that frames the whole band from <paramref name="distance"/>
        /// outside the lane and holds its middle. Named with its distance,
        /// because four of them in a row are otherwise told apart only by
        /// looking. The name carried the lens as well until 25 September 2026,
        /// when the lens began following the band's live height and a number
        /// fixed in the name could disagree with it; the debug menu shows the
        /// camera's own lens beside the name instead.
        /// </summary>
        private static CameraPreset WholeBand(float distance)
        {
            return new CameraPreset(
                "Whole band, " + distance.ToString("0") + " out",
                distance, FieldOfViewFor(distance, WholeBandHeight), true);
        }

        /// <summary>
        /// World height a camera sees at <paramref name="distance"/> in front of
        /// it with a vertical field of view of <paramref name="fieldOfView"/>.
        /// </summary>
        public static float VisibleHeight(float distance, float fieldOfView)
        {
            return 2f * Mathf.Max(0f, distance) * Mathf.Tan(Mathf.Clamp(fieldOfView, 0f, 179f) * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>The vertical field of view that shows <paramref name="height"/> at <paramref name="distance"/>.</summary>
        public static float FieldOfViewFor(float distance, float height)
        {
            if (distance <= 0f)
            {
                return 179f;
            }

            return 2f * Mathf.Atan(Mathf.Max(0f, height) * 0.5f / distance) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// How many times further from the camera than the ship something is
        /// when it sits <paramref name="alongRing"/> units round the lane from
        /// the ship, for a camera <paramref name="distance"/> outside a lane of
        /// radius <paramref name="lane"/>. Heights and sizes there are drawn
        /// this many times smaller than at the ship.
        /// </summary>
        public static float DepthRatio(float distance, float alongRing, float lane)
        {
            if (distance <= 0f || lane <= 0f)
            {
                return 1f;
            }

            float angle = alongRing / lane;
            float depth = distance + lane * (1f - Mathf.Cos(angle));
            return depth / distance;
        }
    }
}
