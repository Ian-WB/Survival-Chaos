namespace SurvivalChaos
{
    /// <summary>
    /// The lines the loading screen shows under its bar, one a load.
    ///
    /// The control hints teach the buttons. These are the rules nothing else
    /// states: what a dash passes through, what ramming costs, why salvage
    /// turns up when it does. Each is one sentence, because the screen is up
    /// for about a second and a half.
    ///
    /// A tip has to stay true. Every one of them names something the code does
    /// today, so a change to the rule wants the line changed with it.
    /// </summary>
    public static class LoadingTips
    {
        public static readonly string[] All =
        {
            "A dash passes through rounds and the Leviathan's hull untouched.",
            "Ramming an enemy costs a hit point and earns nothing.",
            "Salvage turns up more often the more damaged you are.",
            "The Leviathan's hull cannot be hurt while an emplacement stands.",
            "Your rounds fly at your own height. Climb to what you want to hit.",
            "Fly into one upgrade and the others in that offer vanish.",
            "The Deflector blocks one hit, then recharges while nothing hits you.",
            "Slow Mo slows everything, your ship included. It buys time to see.",
            "A muzzle on the Leviathan glows just before it fires.",
            "Destroy an emplacement and its bank of guns falls silent.",
            "Reverse turns the ship to fire the other way round the ring."
        };

        /// <summary>
        /// Which tip to show next: any but the last one shown, so two loads in
        /// a row never repeat a line. <paramref name="roll"/> is any
        /// non-negative number; the caller supplies the randomness, which keeps
        /// this testable.
        /// </summary>
        public static int Next(int last, int roll)
        {
            int count = All.Length;
            if (count <= 1)
            {
                return 0;
            }

            if (roll < 0)
            {
                roll = -(roll + 1);
            }

            if (last < 0 || last >= count)
            {
                return roll % count;
            }

            // One of the count - 1 others, counted on from the last.
            return (last + 1 + roll % (count - 1)) % count;
        }
    }
}
