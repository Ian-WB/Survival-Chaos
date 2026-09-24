using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Piercing Rounds bookkeeping. The case worth pinning is the repeat:
    /// a round that flies on is still inside the ship it went through, and a
    /// second report of that contact must not be a second hit.
    /// </summary>
    public class RoundPierceTests
    {
        private static RoundPierce Round(int pierces)
        {
            var round = new RoundPierce();
            round.Reset(pierces);
            return round;
        }

        [Test]
        public void WithNoPasses_TheFirstTargetStopsIt()
        {
            RoundPierce round = Round(0);

            Assert.AreEqual(StrikeResult.Stop, round.Strike("a"));
        }

        [Test]
        public void OnePass_GoesThroughOneTarget_AndStopsAtTheNext()
        {
            RoundPierce round = Round(1);

            Assert.AreEqual(StrikeResult.PassThrough, round.Strike("a"));
            Assert.AreEqual(StrikeResult.Stop, round.Strike("b"));
        }

        [Test]
        public void StrikingTheSameTargetAgain_IsIgnored_AndSpendsNothing()
        {
            RoundPierce round = Round(1);
            round.Strike("a");

            Assert.AreEqual(StrikeResult.Ignore, round.Strike("a"));
            Assert.AreEqual(0, round.Left);
            Assert.AreEqual(StrikeResult.Stop, round.Strike("b"));
        }

        [Test]
        public void OnceStopped_NothingElseCounts()
        {
            // A contact from the physics step the round was spent in, arriving
            // after it has gone back to the pool.
            RoundPierce round = Round(0);
            round.Strike("a");

            Assert.AreEqual(StrikeResult.Ignore, round.Strike("b"));
        }

        [Test]
        public void Reset_StartsANewLife()
        {
            RoundPierce round = Round(0);
            round.Strike("a");

            round.Reset(2);

            Assert.AreEqual(2, round.Left);
            Assert.AreEqual(StrikeResult.PassThrough, round.Strike("a"));
        }
    }
}
