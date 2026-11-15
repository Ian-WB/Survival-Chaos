using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    public class RunRandomTests
    {
        private static float[] Draw(int stream, int count)
        {
            var drawn = new float[count];
            for (int i = 0; i < count; i++) drawn[i] = RunRandom.Value(stream);
            return drawn;
        }

        [TearDown]
        public void LeaveNoSeedBehind()
        {
            RunRandom.Reseed(0);
        }

        [Test]
        public void SameSeed_GivesAStreamTheSameNumbers()
        {
            RunRandom.Reseed(41);
            float[] first = Draw(3, 20);
            RunRandom.Reseed(41);
            Assert.That(Draw(3, 20), Is.EqualTo(first));
        }

        [Test]
        public void AStreamsNumbers_DoNotDependOnWhatTheOthersDrew()
        {
            // What the seed is for: a wave stream's fortieth enemy arrives where
            // it did last time, whatever the other streams, the offers and the
            // upgrade draws have taken in between.
            RunRandom.Reseed(7);
            float[] alone = Draw(3, 10);

            RunRandom.Reseed(7);
            var among = new float[10];
            for (int i = 0; i < among.Length; i++)
            {
                RunRandom.Value(4);
                RunRandom.Index(RunRandom.Skills, 9);
                RunRandom.Range(RunRandom.Offers, -1f, 1f);
                among[i] = RunRandom.Value(3);
            }

            Assert.That(among, Is.EqualTo(alone));
        }

        [Test]
        public void AnotherSeed_OrAnotherStream_GivesOtherNumbers()
        {
            RunRandom.Reseed(1);
            float[] one = Draw(0, 8);
            float[] neighbour = Draw(1, 8);
            RunRandom.Reseed(2);
            float[] two = Draw(0, 8);

            Assert.That(two, Is.Not.EqualTo(one), "seeds 1 and 2");
            Assert.That(neighbour, Is.Not.EqualTo(one), "streams 0 and 1");
        }

        [Test]
        public void Reseed_RecordsTheSeed()
        {
            RunRandom.Reseed(1234);
            Assert.That(RunRandom.Seed, Is.EqualTo(1234));
        }

        [Test]
        public void Numbers_StayInsideWhatWasAskedFor_AndSpreadOverIt()
        {
            RunRandom.Reseed(99);
            float sum = 0f;
            var chosen = new int[4];
            const int draws = 8000;

            for (int i = 0; i < draws; i++)
            {
                float value = RunRandom.Value(0);
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                sum += value;

                float ranged = RunRandom.Range(1, -180f, 180f);
                Assert.That(ranged, Is.GreaterThanOrEqualTo(-180f).And.LessThan(180f));

                int index = RunRandom.Index(2, 4);
                Assert.That(index, Is.InRange(0, 3));
                chosen[index]++;
            }

            Assert.That(sum / draws, Is.EqualTo(0.5f).Within(0.02f), "the mean of an even spread");
            foreach (int times in chosen)
            {
                Assert.That(times, Is.InRange(draws / 4 - 200, draws / 4 + 200), "each of four, about a quarter");
            }
        }

        [Test]
        public void Index_WithNothingToChooseFrom_IsZero()
        {
            RunRandom.Reseed(5);
            Assert.That(RunRandom.Index(0, 0), Is.EqualTo(0));
            Assert.That(RunRandom.Index(0, -3), Is.EqualTo(0));
            Assert.That(RunRandom.Index(0, 1), Is.EqualTo(0));
        }
    }
}
