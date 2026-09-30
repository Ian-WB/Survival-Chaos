using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The polish of 30 September 2026 that is logic rather than feel: the
    /// hit-stop and the death beat's hold on the game's speed, the beat as an
    /// ending nothing can undo, the best-run records, the control hints, and
    /// every new sound slot being filled.
    /// </summary>
    public class RunMomentsTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
            RunStats.Clear();
            root = new GameObject("Run moments test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
            RunStats.Clear();
        }

        // ---------- hit-stop ----------

        [Test]
        public void AHitStop_AllButStopsTheGame_ThenGivesItBack()
        {
            RunTime.SetAbilityScale(0.4f);
            RunTime.Freeze(0.07f);

            Assert.That(RunTime.Frozen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f * RunTime.FreezeScale).Within(1e-6f),
                "a freeze under Slow Mo holds the slowed game, not the full-speed one");
            Assert.That(Time.timeScale, Is.GreaterThan(0f),
                "never quite zero, which several scripts read as the pause menu");

            RunTime.Tick(Time.unscaledTime + 0.05f);
            Assert.That(RunTime.Frozen, Is.True, "not over before its time");

            RunTime.Tick(Time.unscaledTime + 1f);
            Assert.That(RunTime.Frozen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void AHitStop_LeavesTheSoundsAtTheirSpeed()
        {
            RunTime.SetAbilityScale(0.4f);
            RunTime.Freeze(0.15f);

            Assert.That(RunTime.SoundSpeed, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void NoHitStop_OnceTheRunIsDecided_OrUnderThePauseMenu()
        {
            PauseMenu.GameIsPaused = true;
            RunTime.Freeze(0.1f);
            Assert.That(RunTime.Frozen, Is.False);

            PauseMenu.GameIsPaused = false;
            RunOutcome.ReportEnding(0.25f);
            RunTime.Freeze(0.1f);
            Assert.That(RunTime.Frozen, Is.False);
        }

        // ---------- the death beat ----------

        [Test]
        public void TheDeathBeat_RunsSlowly_ThenTheCardStopsTime()
        {
            RunTime.SetSpeed(2f);
            RunOutcome.ReportEnding(0.25f);

            Assert.That(RunOutcome.Ending, Is.True);
            Assert.That(RunOutcome.Decided, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f),
                "the beat's own speed, whatever the debug menu asked for");

            RunOutcome.ReportRunEnded();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(RunOutcome.Ending, Is.False);
            Assert.That(RunOutcome.Decided, Is.True);
        }

        [Test]
        public void TheDeathBeat_StopsTheClockAtTheKillingHit()
        {
            RunOutcome.ReportEnding(0.25f);
            float atHit = RunStats.Seconds;

            // Stop latches, so the card's own stop cannot move it on.
            RunStats.Stop();
            Assert.That(RunStats.Seconds, Is.EqualTo(atHit));
        }

        [Test]
        public void DuringTheDeathBeat_ARoundOnTheHullCannotWinTheRun()
        {
            BossEmitter boss = Child("Boss").AddComponent<BossEmitter>();
            GameObject round = Child("Player round", "Shoot");
            Collider hit = round.AddComponent<BoxCollider>();

            int victories = 0;
            RunOutcome.BossDefeated += () => victories++;

            RunOutcome.ReportEnding(0.25f);
            Enter(boss, hit);

            Assert.That(victories, Is.Zero);
        }

        [Test]
        public void DuringTheDeathBeat_ARoundOnAnEmplacementCountsForNothing()
        {
            BossWeakPoint pod = Child("Emplacement").AddComponent<BossWeakPoint>();
            Collider hit = Child("Player round", "Shoot").AddComponent<BoxCollider>();

            RunOutcome.ReportEnding(0.25f);
            Enter(pod, hit);

            Assert.That(pod.Destroyed, Is.False);
        }

        [Test]
        public void DuringTheDeathBeat_NothingHurtsTheShipAgain()
        {
            Player player = Child("Player").AddComponent<Player>();
            typeof(Player).GetField("health", Private).SetValue(player, new HealthState(1));
            Collider round = Child("Boss round", "enemy_Shoot").AddComponent<BoxCollider>();

            RunOutcome.ReportEnding(0.25f);
            Enter(player, round);
            player.TakeBeamHit();

            Assert.That(player.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void TheDeathBeat_CannotBeStartedTwice_OrAfterAWin()
        {
            RunOutcome.ReportRunEnded();
            RunOutcome.ReportEnding(0.25f);

            Assert.That(RunOutcome.Ending, Is.False, "the first ending stands");
            Assert.That(Time.timeScale, Is.Zero);
        }

        // ---------- records ----------

        [Test]
        public void TheFirstRun_SetsEveryRecordItReaches()
        {
            RunRecords.Beaten beaten = RunRecords.Compare(312f, 14, won: false, 0f, 0, 0f);

            Assert.That(beaten, Is.EqualTo(RunRecords.Beaten.Longest | RunRecords.Beaten.Level),
                "a loss is never a fastest win");
        }

        [Test]
        public void ARecord_IsBeatenByAWholeSecond_NotAFraction()
        {
            Assert.That(RunRecords.Compare(312.9f, 1, false, 312.1f, 99, 0f), Is.EqualTo(RunRecords.Beaten.None),
                "the card shows 5:12 both times, so it cannot claim a new best");
            Assert.That(RunRecords.Compare(313.0f, 1, false, 312.9f, 99, 0f), Is.EqualTo(RunRecords.Beaten.Longest));
        }

        [Test]
        public void AWin_IsFastestOnlyWhenItIsQuicker()
        {
            Assert.That(RunRecords.Compare(700f, 1, true, 900f, 99, 650f) & RunRecords.Beaten.Fastest,
                Is.EqualTo(RunRecords.Beaten.None));
            Assert.That(RunRecords.Compare(640f, 1, true, 900f, 99, 650f) & RunRecords.Beaten.Fastest,
                Is.EqualTo(RunRecords.Beaten.Fastest));
            Assert.That(RunRecords.Compare(640f, 1, true, 900f, 99, 0f) & RunRecords.Beaten.Fastest,
                Is.EqualTo(RunRecords.Beaten.Fastest), "the first win sets it");
        }

        [Test]
        public void TheCard_NamesWhatWasBeaten()
        {
            Assert.That(RunSummary.Bests(RunRecords.Beaten.None), Is.Empty);
            Assert.That(RunSummary.Bests(RunRecords.Beaten.Longest | RunRecords.Beaten.Fastest),
                Is.EqualTo("LONGEST RUN, FASTEST WIN"));
        }

        [Test]
        public void TheClock_ReadsInMinutesAndSeconds()
        {
            Assert.That(RunRecords.Clock(0f), Is.EqualTo("0:00"));
            Assert.That(RunRecords.Clock(605.9f), Is.EqualTo("10:05"));
        }

        // ---------- hints ----------

        [Test]
        public void EveryHint_HasALine_AndTheButtonsFollowTheDevice()
        {
            foreach (ControlHint hint in System.Enum.GetValues(typeof(ControlHint)))
            {
                Assert.That(Tutorial.Line(hint, pad: false), Is.Not.Empty, hint.ToString());
                Assert.That(Tutorial.Line(hint, pad: true), Is.Not.Empty, hint.ToString());
            }

            Assert.That(Tutorial.Line(ControlHint.Reverse, pad: false), Does.StartWith("Shift"));
            Assert.That(Tutorial.Line(ControlHint.Reverse, pad: true), Does.StartWith("LB"));
            Assert.That(Tutorial.Line(ControlHint.Dash, pad: false), Does.StartWith("Space"));
            Assert.That(Tutorial.Line(ControlHint.SlowMo, pad: false), Does.StartWith("E "));
        }

        // ---------- sounds ----------

        /// <summary>
        /// An empty slot is a silence nobody notices, which is the reason
        /// GameSounds exists as one asset. Every slot, the nine new ones too.
        /// </summary>
        [Test]
        public void EverySoundSlot_HasASoundWithAClip()
        {
            GameSounds sounds = AssetDatabase.LoadAssetAtPath<GameSounds>("Assets/Resources/GameSounds.asset");
            Assert.That(sounds, Is.Not.Null);

            foreach (PropertyInfo slot in typeof(GameSounds).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (slot.PropertyType != typeof(SoundDefinition))
                {
                    continue;
                }

                SoundDefinition sound = (SoundDefinition)slot.GetValue(sounds);
                Assert.That(sound, Is.Not.Null, slot.Name + " is empty");
                Assert.That(sound.HasClips, Is.True, slot.Name + " has no clip");
            }
        }

        [Test]
        public void TheSlowMoCues_AreNotSlowedByTheSlowMo()
        {
            GameSounds sounds = AssetDatabase.LoadAssetAtPath<GameSounds>("Assets/Resources/GameSounds.asset");

            // Gameplay sounds follow the game's speed; the interface's do not.
            Assert.That(sounds.SlowMoStart.Channel, Is.EqualTo(AudioChannel.Ui));
            Assert.That(sounds.SlowMoEnd.Channel, Is.EqualTo(AudioChannel.Ui));
        }

        // ---------- helpers ----------

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
    }
}
