using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The boss is spawned from a prefab and cannot reference the scene, so this
    /// event is the only link between it dying and the run ending. A dropped or
    /// duplicated subscription here means no victory screen, or two.
    /// </summary>
    public class RunOutcomeTests
    {
        [SetUp]
        public void SetUp()
        {
            RunOutcome.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RunOutcome.Clear();
        }

        [Test]
        public void ReportBossDefeated_NotifiesSubscriber()
        {
            int calls = 0;
            RunOutcome.BossDefeated += () => calls++;

            RunOutcome.ReportBossDefeated();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ReportBossDefeated_NotifiesEverySubscriber()
        {
            int first = 0;
            int second = 0;
            RunOutcome.BossDefeated += () => first++;
            RunOutcome.BossDefeated += () => second++;

            RunOutcome.ReportBossDefeated();

            Assert.AreEqual(1, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void ReportBossDefeated_WithNoSubscribers_DoesNotThrow()
        {
            // Also logs a warning, which is the point: a listener on an inactive
            // object never subscribes, and that used to fail silently.
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("nothing is listening"));

            Assert.DoesNotThrow(() => RunOutcome.ReportBossDefeated());
        }

        [Test]
        public void Unsubscribing_StopsNotifications()
        {
            int calls = 0;
            System.Action handler = () => calls++;

            RunOutcome.BossDefeated += handler;
            RunOutcome.BossDefeated -= handler;
            RunOutcome.ReportBossDefeated();

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Clear_DropsEverySubscriber()
        {
            int calls = 0;
            RunOutcome.BossDefeated += () => calls++;

            RunOutcome.Clear();
            RunOutcome.ReportBossDefeated();

            Assert.AreEqual(0, calls, "a listener from a previous run must not survive into the next");
        }

        [Test]
        public void SubscribingTwice_NotifiesTwice()
        {
            // Documents why VictoryMenu pairs its subscribe with an unsubscribe:
            // an unbalanced OnEnable would stack handlers.
            int calls = 0;
            System.Action handler = () => calls++;

            RunOutcome.BossDefeated += handler;
            RunOutcome.BossDefeated += handler;
            RunOutcome.ReportBossDefeated();

            Assert.AreEqual(2, calls);
        }
    }
}
