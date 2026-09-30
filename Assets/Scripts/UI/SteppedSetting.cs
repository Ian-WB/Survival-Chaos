using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A setting a row can step through with left and right: what
    /// <see cref="OptionRow"/> drives. The graphics settings are one kind, and
    /// the Controls tab's on/off switches another.
    /// </summary>
    public abstract class SteppedSetting : MonoBehaviour
    {
        /// <summary>
        /// One step either way. False when nothing changed, so the row stays
        /// silent rather than clicking as if it had.
        /// </summary>
        public abstract bool Step(int direction);

        /// <summary>For the row's right arrow.</summary>
        public void Next() => Step(1);

        /// <summary>For the row's left arrow.</summary>
        public void Previous() => Step(-1);
    }
}
