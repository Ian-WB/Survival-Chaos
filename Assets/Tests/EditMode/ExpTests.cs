using System.Reflection;
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
            EXP.Instance.OnEXPChange += (amount, where) => received = amount;

            EXP.Instance.AddEXP(5, Vector3.zero);

            Assert.AreEqual(5, received);
        }

        [Test]
        public void AddEXP_ForwardsWhereTheKillWas()
        {
            // The player shows the scaled reward there, so it has to arrive.
            Vector3 received = Vector3.zero;
            EXP.Instance.OnEXPChange += (amount, where) => received = where;

            EXP.Instance.AddEXP(5, new Vector3(1f, 2f, 3f));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), received);
        }

        [Test]
        public void AddEXP_NotifiesEverySubscriber()
        {
            int first = 0;
            int second = 0;
            EXP.Instance.OnEXPChange += (amount, where) => first = amount;
            EXP.Instance.OnEXPChange += (amount, where) => second = amount;

            EXP.Instance.AddEXP(15, Vector3.zero);

            Assert.AreEqual(15, first);
            Assert.AreEqual(15, second);
        }

        [Test]
        public void AddEXP_AfterUnsubscribe_DoesNotNotify()
        {
            int received = 0;
            EXP.EXPChangeHandler handler = (amount, where) => received = amount;

            EXP.Instance.OnEXPChange += handler;
            EXP.Instance.OnEXPChange -= handler;
            EXP.Instance.AddEXP(20, Vector3.zero);

            Assert.AreEqual(0, received);
        }

        [Test]
        public void AddEXP_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EXP.Instance.AddEXP(1, Vector3.zero));
        }

        [Test]
        public void EXP_WakesBeforeThePlayer()
        {
            // Player subscribes from OnEnable, and Unity orders Awake and OnEnable
            // across objects only by execution order. At 0 each, it came down to
            // the order the scene loaded them in.
            var exp = typeof(EXP).GetCustomAttribute<DefaultExecutionOrder>();
            var player = typeof(Player).GetCustomAttribute<DefaultExecutionOrder>();

            Assert.IsNotNull(exp, "EXP has lost its DefaultExecutionOrder.");
            Assert.Less(exp.order, player != null ? player.order : 0);
        }
    }
}
