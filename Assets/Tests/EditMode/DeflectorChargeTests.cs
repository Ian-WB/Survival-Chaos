using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Deflector's one charge. Its rule is the whole design - it comes back
    /// only to a player who stops being hit - and nothing about that is visible
    /// in play except as a hit that should or should not have landed.
    /// </summary>
    public class DeflectorChargeTests
    {
        [Test]
        public void ANewCharge_IsReady()
        {
            var charge = new DeflectorCharge(10f);

            Assert.IsTrue(charge.IsCharged(0f));
            Assert.AreEqual(1f, charge.ReadyFraction(0f));
        }

        [Test]
        public void TryAbsorb_BlocksOneHit_AndNotTheNextStraightAfter()
        {
            var charge = new DeflectorCharge(10f);

            Assert.IsTrue(charge.TryAbsorb(5f));
            Assert.IsFalse(charge.TryAbsorb(5f));
        }

        [Test]
        public void TheCharge_ComesBackAfterTheRechargeWithoutAHit()
        {
            var charge = new DeflectorCharge(10f);
            charge.TryAbsorb(0f);

            Assert.IsFalse(charge.IsCharged(9.9f));
            Assert.IsTrue(charge.IsCharged(10.1f));
        }

        [Test]
        public void AHitItCouldNotBlock_StillRestartsTheWait()
        {
            var charge = new DeflectorCharge(10f);
            charge.TryAbsorb(0f);

            // Spent, so this one lands on the hull - and the ten seconds start over.
            Assert.IsFalse(charge.TryAbsorb(6f));

            Assert.IsFalse(charge.IsCharged(12f));
            Assert.IsTrue(charge.IsCharged(16.1f));
        }

        [Test]
        public void ReadyFraction_ClimbsOverTheRecharge()
        {
            var charge = new DeflectorCharge(10f);
            charge.TryAbsorb(0f);

            Assert.AreEqual(0f, charge.ReadyFraction(0f), 1e-5f);
            Assert.AreEqual(0.5f, charge.ReadyFraction(5f), 1e-5f);
            Assert.AreEqual(1f, charge.ReadyFraction(20f), 1e-5f);
        }

        [Test]
        public void SetRecharge_AppliesToAWaitAlreadyRunning()
        {
            var charge = new DeflectorCharge(10f);
            charge.TryAbsorb(0f);

            charge.SetRecharge(6f);

            Assert.IsTrue(charge.IsCharged(6.1f));
        }

        [Test]
        public void SetRecharge_NeverReachesZero()
        {
            // At zero the charge would be back the instant it was spent and
            // block every hit there is.
            var charge = new DeflectorCharge(0f);
            charge.TryAbsorb(1f);

            Assert.IsFalse(charge.TryAbsorb(1f));
            Assert.Greater(charge.Recharge, 0f);
        }
    }
}
