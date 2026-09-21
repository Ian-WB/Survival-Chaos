using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// How a crown torpedo chases the player's height.
    ///
    /// The whole design is that it can miss, so these pin both halves of that:
    /// it arrives on a player who holds a height, and it cannot keep up with one
    /// who changes height - through the delay and through the climb limit - with
    /// the numbers BuildBossRig authors.
    /// </summary>
    public class TorpedoSteerTests
    {
        private const float Floor = 4.42f;
        private const float Ceiling = 13.32f;

        // What BuildBossRig gives the crown's torpedoes.
        private const float Perception = 2.5f;
        private const float Steer = 3f;
        private const float MaxClimb = 3.5f;
        private const float HomeSeconds = 3f;

        /// <summary>The player's climb speed.</summary>
        private const float PlayerClimb = 7f;

        private const float Frame = 1f / 60f;

        [Test]
        public void ReachesAPlayerWhoHoldsTheirHeight()
        {
            float height = 6f;
            float ghost = height;

            for (float t = 0f; t < HomeSeconds; t += Frame)
            {
                TorpedoSteer.Step(ref height, ref ghost, 9f, Perception, Steer, MaxClimb, Floor, Ceiling, Frame);
            }

            Assert.AreEqual(9f, height, 0.1f);
        }

        /// <summary>
        /// The delay: in its first tenth of a second a torpedo has barely begun
        /// to believe the player is anywhere but where it is, so it leaves the
        /// muzzle almost straight.
        /// </summary>
        [Test]
        public void LeavesTheMuzzleNearlyStraight()
        {
            float height = 6f;
            float ghost = height;

            for (float t = 0f; t < 0.1f; t += Frame)
            {
                TorpedoSteer.Step(ref height, ref ghost, 10f, Perception, Steer, MaxClimb, Floor, Ceiling, Frame);
            }

            Assert.Less(height - 6f, 0.15f);
        }

        [Test]
        public void NeverClimbsFasterThanItsLimit()
        {
            float height = Floor;
            float ghost = Ceiling;

            for (int i = 0; i < 200; i++)
            {
                float climb = TorpedoSteer.Step(ref height, ref ghost, Ceiling, Perception, Steer, MaxClimb, Floor, Ceiling, Frame);
                Assert.LessOrEqual(climb, MaxClimb + 1e-3f);
            }
        }

        /// <summary>
        /// A player climbing flat out gains on a torpedo chasing them. This is
        /// what makes it dodgeable rather than only survivable.
        /// </summary>
        [Test]
        public void AClimbingPlayer_PullsAwayFromIt()
        {
            float player = 5f;
            float height = 5f;
            float ghost = height;

            for (float t = 0f; t < 1f; t += Frame)
            {
                player += PlayerClimb * Frame;
                TorpedoSteer.Step(ref height, ref ghost, player, Perception, Steer, MaxClimb, Floor, Ceiling, Frame);
            }

            Assert.Greater(player - height, 3f);
        }

        /// <summary>
        /// The miss. A player who dives just as it closes leaves it heading for
        /// where they were: a quarter second after the dive begins, the torpedo is
        /// still at or above its old height.
        /// </summary>
        [Test]
        public void AChangeOfHeight_LeavesItChasingWhereThePlayerWas()
        {
            float height = 8f;
            float ghost = 8f;
            float player = 8f;

            for (float t = 0f; t < 0.25f; t += Frame)
            {
                player -= PlayerClimb * Frame;
                TorpedoSteer.Step(ref height, ref ghost, player, Perception, Steer, MaxClimb, Floor, Ceiling, Frame);
            }

            Assert.Greater(height - player, 1.2f);
        }

        [Test]
        public void StaysInsideTheBand()
        {
            float height = Ceiling - 0.2f;
            float ghost = Ceiling + 10f;

            for (int i = 0; i < 300; i++)
            {
                TorpedoSteer.Step(ref height, ref ghost, Ceiling + 10f, Perception, Steer, MaxClimb, Floor, Ceiling, Frame);
            }

            Assert.LessOrEqual(height, Ceiling);
        }

        [Test]
        public void TheSameChase_AtAnyFrameRate()
        {
            float fastHeight = 6f, fastGhost = 6f;
            float slowHeight = 6f, slowGhost = 6f;

            for (int i = 0; i < 120; i++)
            {
                TorpedoSteer.Step(ref fastHeight, ref fastGhost, 10f, Perception, Steer, MaxClimb, Floor, Ceiling, 1f / 120f);
            }

            for (int i = 0; i < 30; i++)
            {
                TorpedoSteer.Step(ref slowHeight, ref slowGhost, 10f, Perception, Steer, MaxClimb, Floor, Ceiling, 1f / 30f);
            }

            Assert.AreEqual(fastHeight, slowHeight, 0.1f);
        }

        [Test]
        public void ANoughtFrame_ChangesNothing()
        {
            float height = 6f;
            float ghost = 6f;

            Assert.AreEqual(0f, TorpedoSteer.Step(ref height, ref ghost, 10f, Perception, Steer, MaxClimb, Floor, Ceiling, 0f));
            Assert.AreEqual(6f, height);
        }

        [Test]
        public void ItsClimbLimit_IsUnderThePlayers()
        {
            Assert.Less(MaxClimb, PlayerClimb);
        }

        [Test]
        public void Pitch_NosesUpWhenClimbing_AndLevelWhenNot()
        {
            Assert.Greater(TorpedoSteer.Pitch(3.5f, 19.2f), 0f);
            Assert.Less(TorpedoSteer.Pitch(-3.5f, 19.2f), 0f);
            Assert.AreEqual(0f, TorpedoSteer.Pitch(0f, 19.2f));
        }

        /// <summary>The ring's direction does not decide which way is up.</summary>
        [Test]
        public void Pitch_IsTheSameWhicheverWayRoundItTravels()
        {
            Assert.AreEqual(TorpedoSteer.Pitch(2f, 19.2f), TorpedoSteer.Pitch(2f, -19.2f));
        }
    }
}
