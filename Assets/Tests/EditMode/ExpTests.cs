using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Pins the EXP event contract that Player and every Death() call depend on.
    /// EXP.Instance is normally assigned by EXP.Awake(), which does not run in
    /// edit mode, so these tests wire it up by hand.
    /// </summary>
    public class ExpTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("EXP");
            EXP.Instance = host.AddComponent<EXP>();
        }

        [TearDown]
        public void TearDown()
        {
            EXP.Instance = null;
            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void AddEXP_ForwardsAmountToSubscriber()
        {
            int received = 0;
            EXP.Instance.OnEXPChange += amount => received = amount;

            EXP.Instance.AddEXP(5);

            Assert.AreEqual(5, received);
        }

        [Test]
        public void AddEXP_NotifiesEverySubscriber()
        {
            int first = 0;
            int second = 0;
            EXP.Instance.OnEXPChange += amount => first = amount;
            EXP.Instance.OnEXPChange += amount => second = amount;

            EXP.Instance.AddEXP(15);

            Assert.AreEqual(15, first);
            Assert.AreEqual(15, second);
        }

        [Test]
        public void AddEXP_AfterUnsubscribe_DoesNotNotify()
        {
            int received = 0;
            EXP.EXPChangeHandler handler = amount => received = amount;

            EXP.Instance.OnEXPChange += handler;
            EXP.Instance.OnEXPChange -= handler;
            EXP.Instance.AddEXP(20);

            Assert.AreEqual(0, received);
        }

        [Test]
        public void AddEXP_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EXP.Instance.AddEXP(1));
        }
    }
}
