using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The player's side of experience: binding to EXP whatever order the two
    /// wake in, scaling a reward once for the bar and the run's total alike,
    /// and carrying what crosses a threshold into the next level.
    ///
    /// Awake, OnEnable and Start do not run in edit mode, so the lifecycle is
    /// driven by hand: BindExperience is what OnEnable and Start both call.
    /// </summary>
    public class PlayerExperienceTests
    {
        private GameObject playerHost;
        private GameObject expHost;
        private Player player;

        [SetUp]
        public void SetUp()
        {
            RunStats.Clear();
            EXP.Instance = null;

            playerHost = new GameObject("Player");
            player = playerHost.AddComponent<Player>();

            // The scene's curve: 50 for the first level, 35 more for each after,
            // and every reward tripled.
            player.currentExperience = 0;
            player.maxExperience = 50;
            player.currentLevel = 1;

            var serialized = new SerializedObject(player);
            serialized.FindProperty("levelCostIncrease").intValue = 35;
            serialized.FindProperty("experienceMultiplier").floatValue = 3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            EXP.Instance = null;
            UnityEngine.Object.DestroyImmediate(playerHost);

            if (expHost != null)
            {
                UnityEngine.Object.DestroyImmediate(expHost);
            }

            RunStats.Clear();
        }

        private EXP NewExp()
        {
            if (expHost != null)
            {
                UnityEngine.Object.DestroyImmediate(expHost);
            }

            expHost = new GameObject("EXP");
            return expHost.AddComponent<EXP>();
        }

        private void Call(string method)
        {
            typeof(Player).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, null);
        }

        private static int Subscribers(EXP exp)
        {
            var handlers = (Delegate)typeof(EXP)
                .GetField("OnEXPChange", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(exp);

            return handlers != null ? handlers.GetInvocationList().Length : 0;
        }

        /// <summary>EXP up and the player bound to it, as a scene has them by the first frame.</summary>
        private EXP Bound()
        {
            EXP exp = NewExp();
            EXP.Instance = exp;
            Call("BindExperience");
            return exp;
        }

        [Test]
        public void APlayerThatWokeBeforeEXP_StillEarnsExperience()
        {
            // OnEnable with no EXP yet: the old code skipped its only attempt,
            // and every kill of the run earned nothing.
            Call("BindExperience");

            EXP exp = NewExp();
            EXP.Instance = exp;

            // Start, which tries again.
            Call("BindExperience");
            exp.AddEXP(15, Vector3.zero);

            Assert.AreEqual(45, player.currentExperience);
        }

        [Test]
        public void BindingAgain_DoesNotSubscribeTwice()
        {
            EXP exp = Bound();

            Call("BindExperience");

            Assert.AreEqual(1, Subscribers(exp));
        }

        [Test]
        public void AReplacedEXP_IsSwappedIn_NotAdded()
        {
            EXP first = Bound();
            GameObject firstHost = expHost;
            expHost = null;

            EXP second = NewExp();
            EXP.Instance = second;
            Call("BindExperience");

            Assert.AreEqual(0, Subscribers(first));
            Assert.AreEqual(1, Subscribers(second));

            UnityEngine.Object.DestroyImmediate(firstHost);
        }

        [Test]
        public void Disabling_Unsubscribes_EvenFromAnEXPAlreadyGone()
        {
            EXP exp = Bound();
            EXP.Instance = null;

            Call("OnDisable");

            Assert.AreEqual(0, Subscribers(exp));
        }

        [Test]
        public void AKill_IsScaledOnce_ForTheBarAndTheRunTotal()
        {
            EXP exp = Bound();

            exp.AddEXP(15, Vector3.zero);

            Assert.AreEqual(45, player.currentExperience);
            Assert.AreEqual(45, RunStats.ExperienceEarned,
                "the run's total took the reward before the multiplier");
        }

        [Test]
        public void ALevelUp_CarriesTheRestIntoTheNextLevel()
        {
            // The audit's case: at 45 of 50, a Fighter's 45 used to level up and
            // leave the bar empty, dropping the 40 past the line.
            EXP exp = Bound();
            player.currentExperience = 45;

            exp.AddEXP(15, Vector3.zero);

            Assert.AreEqual(2, player.currentLevel);
            Assert.AreEqual(40, player.currentExperience);
            Assert.AreEqual(85, player.maxExperience);
        }

        [Test]
        public void ARewardCoveringTwoLevels_PaysForBoth()
        {
            // 150 is the boss's 50 tripled: 50 for the first level, 85 for the
            // second, 15 over.
            EXP exp = Bound();

            exp.AddEXP(50, Vector3.zero);

            Assert.AreEqual(3, player.currentLevel);
            Assert.AreEqual(15, player.currentExperience);
            Assert.AreEqual(120, player.maxExperience);
            Assert.AreEqual(3, RunStats.LevelReached);
        }
    }
}
