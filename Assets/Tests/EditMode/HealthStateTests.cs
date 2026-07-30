using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The kill-reporting rule matters most: the original enemy scripts could
    /// award experience from Update after already destroying the object in
    /// OnTriggerEnter, which only worked by accident of Destroy being deferred.
    /// </summary>
    public class HealthStateTests
    {
        [Test]
        public void StartsAtFullHealth()
        {
            var health = new HealthState(7);

            Assert.AreEqual(7, health.Max);
            Assert.AreEqual(7, health.Current);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void MaxHealthIsAtLeastOne()
        {
            var health = new HealthState(0);

            Assert.AreEqual(1, health.Max);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TakeDamage_ReducesCurrentHealth()
        {
            var health = new HealthState(5);

            health.TakeDamage(2);

            Assert.AreEqual(3, health.Current);
        }

        [Test]
        public void TakeDamage_ReturnsFalse_WhenTheHitIsNotFatal()
        {
            var health = new HealthState(2);

            Assert.IsFalse(health.TakeDamage(1));
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TakeDamage_ReturnsTrue_OnTheFatalHit()
        {
            var health = new HealthState(2);

            health.TakeDamage(1);

            Assert.IsTrue(health.TakeDamage(1));
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void TakeDamage_ReportsTheKillOnlyOnce()
        {
            var health = new HealthState(1);

            Assert.IsTrue(health.TakeDamage(1));
            Assert.IsFalse(health.TakeDamage(1), "a second hit must not re-award the kill");
            Assert.IsFalse(health.TakeDamage(99), "overkill must not re-award the kill either");
        }

        [Test]
        public void TakeDamage_ReportsTheKillOnce_EvenWhenOverkilledInOneHit()
        {
            var health = new HealthState(3);

            Assert.IsTrue(health.TakeDamage(10));
            Assert.IsFalse(health.TakeDamage(10));
        }

        [Test]
        public void TakeDamage_IgnoresNegativeAmounts()
        {
            var health = new HealthState(3);

            health.TakeDamage(-5);

            Assert.AreEqual(3, health.Current);
            Assert.IsFalse(health.IsDead);
        }
    }
}
