using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The ghost trail is the one part of the new interface with real timing
    /// behaviour, and it fails in ways that are easy to miss by eye: a trail that
    /// appears while healing, or one that never drains and leaves the bar looking
    /// permanently damaged.
    /// </summary>
    public class BarMotionTests
    {
        private const float Step = 1f / 60f;

        private static BarMotion Settled(float value)
        {
            BarMotion motion = new BarMotion();
            motion.SnapTo(value);
            return motion;
        }

        private static void Run(BarMotion motion, float target, float seconds)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                motion.Advance(target, Step);
            }
        }

        [Test]
        public void SnapTo_PlacesBothValues()
        {
            BarMotion motion = Settled(0.5f);

            Assert.AreEqual(0.5f, motion.Fill, 0.0001f);
            Assert.AreEqual(0.5f, motion.Ghost, 0.0001f);
        }

        [Test]
        public void SnapTo_ClampsOutOfRange()
        {
            Assert.AreEqual(1f, Settled(4f).Fill, 0.0001f);
            Assert.AreEqual(0f, Settled(-2f).Fill, 0.0001f);
        }

        [Test]
        public void Damage_DropsTheFillButHoldsTheGhost()
        {
            BarMotion motion = Settled(1f);

            Run(motion, 0.4f, 0.2f);

            Assert.Less(motion.Fill, 0.6f, "the fill should follow the hit promptly");
            Assert.Greater(motion.Ghost, 0.9f, "the ghost should still be showing what was lost");
        }

        [Test]
        public void Ghost_DrainsOnceTheHoldExpires()
        {
            BarMotion motion = Settled(1f);

            Run(motion, 0.4f, 2f);

            Assert.AreEqual(motion.Fill, motion.Ghost, 0.02f, "the ghost should have caught up");
        }

        [Test]
        public void Healing_LeavesNoTrailAheadOfTheFill()
        {
            // A ghost above the fill is drawn as lost health. While healing there
            // is nothing lost, so it must ride along instead.
            BarMotion motion = Settled(0.3f);

            Run(motion, 1f, 1f);

            Assert.AreEqual(motion.Fill, motion.Ghost, 0.0001f);
        }

        [Test]
        public void RepeatedHits_KeepTheTrailUp()
        {
            // Each new hit restarts the hold. Without that, a burst of damage
            // would cut the first trail short and the player would see less than
            // they actually lost.
            BarMotion motion = Settled(1f);

            Run(motion, 0.8f, 0.3f);
            float afterFirst = motion.Ghost;
            Run(motion, 0.6f, 0.3f);

            Assert.Greater(motion.Ghost, 0.9f, "the ghost should still be near the original value");
            Assert.AreEqual(afterFirst, motion.Ghost, 0.05f);
        }

        [Test]
        public void Advance_WithNoTimeElapsed_ChangesNothing()
        {
            BarMotion motion = Settled(1f);

            motion.Advance(0f, 0f);

            Assert.AreEqual(1f, motion.Fill, 0.0001f);
            Assert.AreEqual(1f, motion.Ghost, 0.0001f);
        }

        [Test]
        public void Fill_ConvergesOnTheTarget()
        {
            BarMotion motion = Settled(1f);

            Run(motion, 0.25f, 3f);

            Assert.AreEqual(0.25f, motion.Fill, 0.01f);
        }

        [Test]
        public void EmptyingCompletely_ReachesZero()
        {
            // Death sets health to zero; a bar that stalls just above empty would
            // show a sliver of hull left on the death screen.
            BarMotion motion = Settled(1f);

            Run(motion, 0f, 3f);

            Assert.AreEqual(0f, motion.Fill, 0.01f);
            Assert.AreEqual(0f, motion.Ghost, 0.02f);
        }
    }
}
