using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The boss laser had no timer at all - it was called straight from Update,
    /// so it emitted a volley every frame. These pin both that behaviour, which
    /// an interval of 0 still reproduces, and the throttled case.
    /// </summary>
    public class VolleyTimerTests
    {
        [Test]
        public void DoesNotFire_BeforeTheInitialDelay()
        {
            var timer = new VolleyTimer(startTime: 0f, initialDelay: 5f);

            Assert.IsFalse(timer.TryFire(0f, 1f));
            Assert.IsFalse(timer.TryFire(4.9f, 1f));
        }

        [Test]
        public void Fires_OnceTheInitialDelayElapses()
        {
            var timer = new VolleyTimer(0f, 5f);

            Assert.IsTrue(timer.TryFire(5f, 1f));
        }

        [Test]
        public void InitialDelayIsRelativeToStartTime()
        {
            var timer = new VolleyTimer(startTime: 100f, initialDelay: 2f);

            Assert.IsFalse(timer.TryFire(101f, 1f));
            Assert.IsTrue(timer.TryFire(102f, 1f));
        }

        [Test]
        public void DoesNotFireTwice_WithinOneInterval()
        {
            var timer = new VolleyTimer(0f, 0f);

            Assert.IsTrue(timer.TryFire(0f, 2f));
            Assert.IsFalse(timer.TryFire(1f, 2f));
            Assert.IsFalse(timer.TryFire(1.9f, 2f));
        }

        [Test]
        public void FiresAgain_AfterTheIntervalElapses()
        {
            var timer = new VolleyTimer(0f, 0f);

            timer.TryFire(0f, 2f);

            Assert.IsTrue(timer.TryFire(2f, 2f));
        }

        [Test]
        public void ZeroInterval_FiresEveryCall()
        {
            var timer = new VolleyTimer(0f, 0f);

            // Reproduces the original laser: one volley per frame.
            Assert.IsTrue(timer.TryFire(0f, 0f));
            Assert.IsTrue(timer.TryFire(0.016f, 0f));
            Assert.IsTrue(timer.TryFire(0.032f, 0f));
        }

        [Test]
        public void NegativeInterval_IsTreatedAsZero()
        {
            var timer = new VolleyTimer(0f, 0f);

            Assert.IsTrue(timer.TryFire(0f, -5f));
            Assert.IsTrue(timer.TryFire(0f, -5f));
        }

        [Test]
        public void ALongStall_DoesNotProduceACatchUpBurst()
        {
            var timer = new VolleyTimer(0f, 0f);

            timer.TryFire(0f, 1f);

            // Ten seconds pass in one frame: one volley, not ten.
            Assert.IsTrue(timer.TryFire(10f, 1f));
            Assert.IsFalse(timer.TryFire(10.5f, 1f));
            Assert.AreEqual(11f, timer.NextFireTime, 0.0001f);
        }
    }
}
