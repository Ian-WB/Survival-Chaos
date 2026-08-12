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

        // ---------- healing and raising the ceiling ----------
        //
        // Added when Player stopped tracking a raw int and moved onto this class,
        // so the Heal and Max Health skills go through the same rules everything
        // else does.

        [Test]
        public void Heal_RestoresHealth()
        {
            var health = new HealthState(10);
            health.TakeDamage(6);

            health.Heal(3);

            Assert.AreEqual(7, health.Current);
        }

        [Test]
        public void Heal_StopsAtTheCeiling()
        {
            var health = new HealthState(10);
            health.TakeDamage(2);

            health.Heal(99);

            Assert.AreEqual(10, health.Current);
            Assert.AreEqual(10, health.Max);
        }

        [Test]
        public void Heal_IgnoresNonPositiveAmounts()
        {
            var health = new HealthState(10);
            health.TakeDamage(4);

            health.Heal(0);
            health.Heal(-5);

            Assert.AreEqual(6, health.Current);
        }

        [Test]
        public void Heal_IsRefusedOnceDead()
        {
            var health = new HealthState(3);
            Assert.IsTrue(health.TakeDamage(3));

            health.Heal(10);

            Assert.IsTrue(health.IsDead, "a heal landing after the killing hit must not revive");
            Assert.AreEqual(0, health.Current);
        }

        [Test]
        public void RaiseMax_LiftsTheCeilingAndCurrentTogether()
        {
            var health = new HealthState(10);
            health.TakeDamage(4);

            health.RaiseMax(20);

            Assert.AreEqual(30, health.Max);
            Assert.AreEqual(26, health.Current,
                "raising the ceiling alone would be a longer bar and no more survivability");
        }

        [Test]
        public void RaiseMax_IgnoresNonPositiveAmounts()
        {
            var health = new HealthState(10);

            health.RaiseMax(0);
            health.RaiseMax(-20);

            Assert.AreEqual(10, health.Max);
            Assert.AreEqual(10, health.Current);
        }

        [Test]
        public void RaiseMax_IsRefusedOnceDead()
        {
            var health = new HealthState(2);
            health.TakeDamage(2);

            health.RaiseMax(20);

            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(2, health.Max);
        }

        [Test]
        public void Heal_CanReachTheRaisedCeiling()
        {
            var health = new HealthState(10);
            health.TakeDamage(9);
            health.RaiseMax(10);

            health.Heal(99);

            Assert.AreEqual(20, health.Current);
        }
    }
}
