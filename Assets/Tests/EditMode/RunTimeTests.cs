using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class RunTimeTests
    {
        [SetUp]
        public void SetUp()
        {
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
        }

        [TearDown]
        public void TearDown()
        {
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
        }

        [Test]
        public void DebugSpeedDuringPause_DoesNotResumeSimulation()
        {
            PauseMenu.GameIsPaused = true;
            RunTime.Apply();
            for (int i = 0; i < 4; i++)
            {
                RunTime.CycleSlowMotion();
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(PauseMenu.GameIsPaused, Is.True);
            }
        }

        [Test]
        public void Resume_RestoresSpeedChosenWhilePaused()
        {
            RunTime.SetSpeed(0.5f);
            PauseMenu.GameIsPaused = true;
            RunTime.Apply();
            RunTime.CycleSlowMotion();
            PauseMenu.GameIsPaused = false;
            RunTime.Apply();
            Assert.That(Time.timeScale, Is.EqualTo(0.25f));
        }

        [Test]
        public void Ending_CannotBeRestartedBySpeedOrResume()
        {
            RunTime.SetSpeed(0.5f);
            RunOutcome.ReportRunEnded();
            Assert.That(Time.timeScale, Is.Zero);
            RunTime.CycleSlowMotion();
            RunTime.SetSpeed(1f);
            PauseMenu.GameIsPaused = false;
            RunTime.Apply();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(RunTime.RequestedSpeed, Is.EqualTo(0.5f));
        }

        [Test]
        public void NewRun_DropsPreviousPauseAndSlowMotion()
        {
            RunTime.SetSpeed(0.25f);
            PauseMenu.GameIsPaused = true;
            RunTime.ResetForNewRun();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(RunTime.RequestedSpeed, Is.EqualTo(1f));
            Assert.That(PauseMenu.GameIsPaused, Is.False);
        }

        [Test]
        public void SlowMo_MultipliesWithTheDebugSpeed()
        {
            RunTime.SetSpeed(0.5f);
            RunTime.SetAbilityScale(0.4f);

            Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void SlowMoEnding_KeepsTheDebugSpeed()
        {
            RunTime.SetSpeed(0.5f);
            RunTime.SetAbilityScale(0.4f);

            RunTime.SetAbilityScale(1f);

            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void SlowMo_DoesNotResumeAPause()
        {
            PauseMenu.GameIsPaused = true;
            RunTime.Apply();

            RunTime.SetAbilityScale(0.4f);
            Assert.That(Time.timeScale, Is.Zero);

            PauseMenu.GameIsPaused = false;
            RunTime.Apply();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void SlowMoEnding_CannotRestartAFinishedRun()
        {
            RunTime.SetAbilityScale(0.4f);
            RunOutcome.ReportRunEnded();

            RunTime.SetAbilityScale(1f);

            Assert.That(Time.timeScale, Is.Zero);
        }

        [Test]
        public void NewRun_DropsSlowMo()
        {
            RunTime.SetAbilityScale(0.4f);

            RunTime.ResetForNewRun();

            Assert.That(RunTime.AbilityScale, Is.EqualTo(1f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void InvalidSlowMoScale_IsIgnored(float scale)
        {
            RunTime.SetAbilityScale(scale);

            Assert.That(RunTime.AbilityScale, Is.EqualTo(1f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSpeed_DoesNotCorruptSimulation(float speed)
        {
            RunTime.SetSpeed(speed);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(RunTime.RequestedSpeed, Is.EqualTo(1f));
        }
    }
}
