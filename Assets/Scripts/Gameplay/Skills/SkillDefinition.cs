using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Base type for a level-up reward. Concrete skills are authored as assets,
    /// so adding or retuning a skill is a content change rather than a code change.
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Shown on the level-up banner, after the skill has been taken.")]
        private string displayName = "Skill";

        [SerializeField]
        [Tooltip("Shown on the pickup out on the ring, before it has been taken. Left empty, " +
                 "the banner text is used instead.")]
        private string pickupName = string.Empty;

        [SerializeField]
        [Tooltip("How many times this skill can be picked in a run. 0 means unlimited.")]
        private int maxPicks = 1;

        [SerializeField]
        [Tooltip("The level the player must have reached before each pick can be offered, in pick " +
                 "order: the first entry gates the first pick, the second the second, and the last " +
                 "holds for any pick after it. Left empty, every pick can be offered from the first " +
                 "level-up, which is what the stat upgrades want.")]
        private int[] availableFromLevel = new int[0];

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("Colour of this skill's pickup on the ring. HDR - values above 1 bloom, " +
                 "which is what makes it read as a glowing object rather than a painted one.")]
        private Color pickupColor = new Color(2f, 1.2f, 0.2f);

        [SerializeField]
        [Tooltip("What each pick does, one short line in pick order, shown under the name on the " +
                 "banner when it is taken; the last line holds for any pick after it. The numbers " +
                 "in it are the Player's tuning, and a test holds them to it.")]
        private string[] effectLines = new string[0];

        public string DisplayName => displayName;

        /// <summary>
        /// What a pick did, for the banner's second line: "+30% fire rate".
        /// Empty when none is authored. Clamped like the stage names, so a
        /// skill with one line says it for every pick.
        /// </summary>
        public string GetEffectLine(int picksTaken)
        {
            if (effectLines == null || effectLines.Length == 0)
            {
                return string.Empty;
            }

            string line = effectLines[Mathf.Clamp(picksTaken - 1, 0, effectLines.Length - 1)];
            return line ?? string.Empty;
        }

        public int MaxPicks => maxPicks;

        /// <summary>
        /// What colour this skill's pickup glows, and its caption with it.
        ///
        /// Colour is what tells one pickup from another at a glance, mid-dodge.
        /// It used to be the only thing, on the argument that an object the size
        /// of a bullet has no room for a label - but a level-up offer is a choice
        /// that forfeits the other two, so offers are captioned now (PickupLabel,
        /// drawn by PickupLabelBoard) and the caption is drawn in this colour.
        /// Authored per skill asset so retuning the palette stays a content change.
        /// </summary>
        public Color PickupColor => pickupColor;

        /// <summary>A skill with no pick limit never leaves the pool.</summary>
        public bool IsUnlimited => maxPicks <= 0;

        /// <summary>
        /// Whether any pick waits on a level. The gated skills are the ones
        /// that arrive mid-run, and the pool makes sure each is shown as it
        /// opens - see SkillPool.DrawOffer.
        /// </summary>
        public bool IsGated => availableFromLevel != null && availableFromLevel.Length > 0;

        /// <summary>
        /// Whether the next pick can be offered to a player at
        /// <paramref name="level"/>, with <paramref name="picksTaken"/> taken so
        /// far. Says nothing about the pick limit, which the pool checks itself.
        ///
        /// Per pick rather than once per skill, so a skill can arrive early and
        /// hold its stronger stages back: the first Deflector at one level, the
        /// faster one several levels later.
        /// </summary>
        public bool IsAvailableAt(int picksTaken, int level)
        {
            if (availableFromLevel == null || availableFromLevel.Length == 0)
            {
                return true;
            }

            int gate = availableFromLevel[Mathf.Clamp(picksTaken, 0, availableFromLevel.Length - 1)];
            return level >= gate;
        }

        /// <summary>
        /// Banner text for a pick. Takes the number of times this skill has now
        /// been taken so multi-stage skills can name each stage, without the
        /// asset holding run state - ScriptableObjects outlive a play session.
        ///
        /// An upgrade's banner is its pickup's name with the numeral, so the
        /// two agree: the "Attack Speed II" the player flew into is announced
        /// as "Attack Speed II!". Heal has no pick limit and keeps its own
        /// line - "Heal XIV" would count something nobody is counting.
        /// </summary>
        public virtual string GetDisplayName(int picksTaken)
        {
            return IsUnlimited ? displayName : GetPickupName(picksTaken) + "!";
        }

        /// <summary>
        /// Appends the pick's Roman numeral to a name: the first pick of any
        /// upgrade is I, the second II (Ian, 15 November 2026). Before this
        /// some numerals counted picks (Deflector II) and one counted levels
        /// of an ability the ship starts with (Dash II), and the stat upgrades
        /// had none at all.
        ///
        /// Clamped to the pick limit, as the stage names are, so a spent
        /// upgrade keeps naming the stage the player has.
        /// </summary>
        protected string Numbered(string name, int picksTaken)
        {
            if (IsUnlimited || string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return name.TrimEnd('!', ' ') + " " + Roman(Mathf.Clamp(picksTaken, 1, maxPicks));
        }

        /// <summary>A whole number from 1 to 3999 in Roman numerals; anything else as digits.</summary>
        public static string Roman(int number)
        {
            if (number < 1 || number > 3999)
            {
                return number.ToString();
            }

            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] numerals = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < values.Length; i++)
            {
                while (number >= values[i])
                {
                    text.Append(numerals[i]);
                    number -= values[i];
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// What the pickup on the ring calls this, given the number of picks the
        /// player would have after taking it.
        ///
        /// Separate from the banner because the two are spoken in different
        /// tenses. The banner fires after the fact and announces what happened -
        /// "Attack Speed Increased!" - which reads oddly floating above something
        /// nobody has touched yet. The label names a thing on offer.
        ///
        /// Falls back to the display name when no pickup name is authored, so a
        /// skill asset that has not been given one still labels its pickup rather
        /// than showing nothing. An upgrade's label carries its pick's numeral.
        /// </summary>
        public virtual string GetPickupName(int picksTaken)
        {
            string name = string.IsNullOrWhiteSpace(pickupName) ? displayName : pickupName;
            return Numbered(name, picksTaken);
        }

        public abstract void Apply(ISkillTarget target);
    }
}
