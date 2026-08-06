using System.Collections.Generic;

namespace SurvivalChaos
{
    /// <summary>One selectable screen size.</summary>
    public readonly struct DisplaySize
    {
        public readonly int Width;
        public readonly int Height;

        public DisplaySize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return Width + " x " + Height;
        }

        public bool Equals(DisplaySize other)
        {
            return Width == other.Width && Height == other.Height;
        }
    }

    /// <summary>
    /// Turns the raw list of modes a monitor reports into the one a person should
    /// be offered.
    ///
    /// Unity reports every refresh rate separately, so a 165 Hz monitor returns
    /// the same handful of sizes eight or nine times over. Presented unfiltered
    /// that is a list of forty entries where four are distinct — the player has to
    /// scroll past duplicates to find anything.
    ///
    /// Kept free of UnityEngine so the filtering can be tested against awkward
    /// input rather than against whatever monitor happens to be attached.
    /// </summary>
    public static class DisplayOptions
    {
        /// <summary>Below this, the interface stops being usable at all.</summary>
        public const int MinimumWidth = 1024;
        public const int MinimumHeight = 576;

        /// <summary>
        /// Distinct sizes, smallest first, with anything unusably small dropped.
        /// Refresh rate is deliberately not offered: it belongs to the display,
        /// and VSync plus the frame cap already cover what a player wants from it.
        /// </summary>
        public static List<DisplaySize> Distinct(IEnumerable<DisplaySize> reported)
        {
            List<DisplaySize> result = new List<DisplaySize>();

            if (reported == null)
            {
                return result;
            }

            foreach (DisplaySize size in reported)
            {
                if (size.Width < MinimumWidth || size.Height < MinimumHeight)
                {
                    continue;
                }

                if (Contains(result, size))
                {
                    continue;
                }

                result.Add(size);
            }

            result.Sort((a, b) =>
            {
                int byWidth = a.Width.CompareTo(b.Width);
                return byWidth != 0 ? byWidth : a.Height.CompareTo(b.Height);
            });

            return result;
        }

        private static bool Contains(List<DisplaySize> list, DisplaySize size)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(size))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The index of <paramref name="wanted"/>, or the closest smaller entry.
        ///
        /// A saved resolution can vanish — a different monitor, a driver change —
        /// and falling back to the nearest size the display actually supports is
        /// better than either failing or resetting to the largest available.
        /// </summary>
        public static int IndexOf(List<DisplaySize> sizes, DisplaySize wanted)
        {
            if (sizes == null || sizes.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < sizes.Count; i++)
            {
                if (sizes[i].Equals(wanted))
                {
                    return i;
                }
            }

            int best = 0;
            long target = (long)wanted.Width * wanted.Height;

            for (int i = 0; i < sizes.Count; i++)
            {
                long area = (long)sizes[i].Width * sizes[i].Height;
                if (area <= target)
                {
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Frame rate caps offered, with 0 meaning uncapped.
        ///
        /// A cap below the display's rate is the cheapest way to stop a laptop
        /// cooking itself on a menu screen, which is why 30 stays on the list even
        /// though nobody chooses it for play.
        /// </summary>
        public static readonly int[] FrameRateCaps = { 0, 30, 60, 90, 120, 144, 165, 240 };

        public static string DescribeCap(int cap)
        {
            return cap <= 0 ? "Uncapped" : cap + " FPS";
        }
    }
}
