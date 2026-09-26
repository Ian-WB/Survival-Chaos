using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// How fast gameplay sounds play against game time. A telegraph that runs
    /// at its own speed while the attack it announces is slowed or paused says
    /// the attack is over before it is.
    /// </summary>
    public class GameSoundRateTests
    {
        [Test]
        public void AtFullSpeed_SoundsPlayAsRecorded()
        {
            Assert.AreEqual(1f, AudioDirector.GameSoundRate(false, false, 1f));
        }

        /// <summary>
        /// Under Slow Mo's 0.4 a 1.2 s lance charge takes 3 s, and its sound has
        /// to take 3 s with it.
        /// </summary>
        [Test]
        public void UnderSlowMo_SoundsSlowWithTheGame()
        {
            Assert.AreEqual(0.4f, AudioDirector.GameSoundRate(false, false, 0.4f), 1e-6f);
            Assert.AreEqual(2f, AudioDirector.GameSoundRate(false, false, 2f), 1e-6f, "a debug speed-up too");
        }

        [Test]
        public void UnderThePauseMenu_SoundsStop()
        {
            Assert.AreEqual(0f, AudioDirector.GameSoundRate(true, false, 0f));
        }

        /// <summary>
        /// The death and victory screens stop time too, and open over the last
        /// explosion of the run. Freezing it would cut it off.
        /// </summary>
        [Test]
        public void AfterTheRunEnds_TheLastSoundsPlayOut()
        {
            Assert.AreEqual(1f, AudioDirector.GameSoundRate(false, true, 0f));
            Assert.AreEqual(1f, AudioDirector.GameSoundRate(true, true, 0f), "paused and then ended");
        }

        [Test]
        public void StoppedTimeWithNoPause_PlaysAsRecorded()
        {
            Assert.AreEqual(1f, AudioDirector.GameSoundRate(false, false, 0f));
        }
    }
}
