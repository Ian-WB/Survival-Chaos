using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The first ending stands.
    ///
    /// Stopping time does not stop the physics step an ending happens in, so hits
    /// already queued in it still arrive afterwards - and the death and victory
    /// screens are sibling screens, so whichever landed second would close the
    /// first. Both endings are reached only through a hit, so these drive the
    /// hit handlers directly with the run already over.
    ///
    /// Awake does not run in edit mode, so none of these objects is set up. A
    /// guard that let a hit through would reach that missing state and throw,
    /// which fails the test as surely as the assertion would.
    /// </summary>
    public class RunEndingTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
            RunStats.Clear();
            root = new GameObject("Run ending test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);

            // ReportRunEnded stops time; ResetForNewRun starts it again.
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
            RunStats.Clear();
        }

        private GameObject Child(string name, string tag = "Untagged")
        {
            GameObject child = new GameObject(name) { tag = tag };
            child.transform.SetParent(root.transform);
            return child;
        }

        private static void Enter(Component receiver, Collider other)
        {
            receiver.GetType().GetMethod("OnTriggerEnter", Private).Invoke(receiver, new object[] { other });
        }

        private Player PlayerOnLastPoint()
        {
            Player player = Child("Player").AddComponent<Player>();
            typeof(Player).GetField("health", Private).SetValue(player, new HealthState(1));
            return player;
        }

        [Test]
        public void AfterTheBossFalls_NoHitReachesThePlayer()
        {
            Player player = PlayerOnLastPoint();
            Collider round = Child("Boss round", "enemy_Shoot").AddComponent<BoxCollider>();
            Collider boss = Child("Boss hull", "Boss").AddComponent<BoxCollider>();

            // The first thing BossEmitter.Death does.
            RunOutcome.ReportRunEnded();

            Enter(player, round);
            Enter(player, boss);
            player.TakeBeamHit();

            Assert.That(player.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void AfterTheBossFalls_RammingAnEnemyNeitherHurtsNorConsumesIt()
        {
            Player player = PlayerOnLastPoint();
            GameObject enemy = Child("Enemy", "Enemy");
            Collider hull = enemy.AddComponent<BoxCollider>();

            RunOutcome.ReportRunEnded();
            Enter(player, hull);

            Assert.That(player.CurrentHealth, Is.EqualTo(1));
            Assert.That(enemy.activeSelf, Is.True);
        }

        [Test]
        public void AfterThePlayerDies_ARoundOnTheHullCannotWinTheRun()
        {
            BossEmitter boss = Child("Boss").AddComponent<BossEmitter>();
            GameObject round = Child("Player round", "Shoot");
            Collider hit = round.AddComponent<BoxCollider>();

            int victories = 0;
            RunOutcome.BossDefeated += () => victories++;

            // What DeathMenu reports as the death screen goes up.
            RunOutcome.ReportRunEnded();
            Enter(boss, hit);

            Assert.That(victories, Is.Zero);
            Assert.That(round.activeSelf, Is.True, "the round was taken, so the hit counted");
        }

        [Test]
        public void AfterThePlayerDies_ARoundOnAnEmplacementCountsForNothing()
        {
            BossWeakPoint pod = Child("Emplacement").AddComponent<BossWeakPoint>();
            GameObject round = Child("Player round", "Shoot");
            Collider hit = round.AddComponent<BoxCollider>();

            RunOutcome.ReportRunEnded();
            Enter(pod, hit);

            Assert.That(pod.Destroyed, Is.False);
            Assert.That(round.activeSelf, Is.True, "the round was taken, so the hit counted");
        }
    }
}
