using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Pins the substitution seam that lets the input backend be replaced.
    /// Deliberately never reads through the legacy backend - UnityEngine.Input
    /// is not meaningful outside play mode - so these assert on the installed
    /// source's type and on pass-through via a fake.
    /// </summary>
    public class GameInputTests
    {
        private sealed class FakeGameInput : IGameInput
        {
            public float Horizontal { get; set; }
            public float Vertical { get; set; }
            public bool ToggleDirectionReleased { get; set; }
            public bool PausePressed { get; set; }
            public bool DebugLevelUpPressed { get; set; }
        }

        [TearDown]
        public void TearDown()
        {
            GameInput.Source = null;
        }

        [Test]
        public void Source_HasABackendByDefault()
        {
            Assert.IsNotNull(GameInput.Source);
        }

        [Test]
        public void DefaultBackend_MatchesActiveInputHandling()
        {
#if ENABLE_INPUT_SYSTEM
            Assert.IsInstanceOf<InputSystemGameInput>(GameInput.Source);
#else
            Assert.IsInstanceOf<LegacyGameInput>(GameInput.Source);
#endif
        }

        [Test]
        public void Axes_ReadFromInstalledSource()
        {
            GameInput.Source = new FakeGameInput { Horizontal = 0.5f, Vertical = -0.25f };

            Assert.AreEqual(0.5f, GameInput.Horizontal);
            Assert.AreEqual(-0.25f, GameInput.Vertical);
        }

        [Test]
        public void Buttons_ReadFromInstalledSource()
        {
            GameInput.Source = new FakeGameInput
            {
                ToggleDirectionReleased = true,
                PausePressed = true,
                DebugLevelUpPressed = true
            };

            Assert.IsTrue(GameInput.ToggleDirectionReleased);
            Assert.IsTrue(GameInput.PausePressed);
            Assert.IsTrue(GameInput.DebugLevelUpPressed);
        }

        [Test]
        public void Reads_TrackTheInstalledSourceAfterItChanges()
        {
            var fake = new FakeGameInput { Horizontal = 1f };
            GameInput.Source = fake;

            fake.Horizontal = -1f;

            Assert.AreEqual(-1f, GameInput.Horizontal);
        }

        [Test]
        public void Source_SetToNull_RestoresTheDefaultBackend()
        {
            GameInput.Source = new FakeGameInput();

            GameInput.Source = null;

            Assert.IsNotNull(GameInput.Source);
            Assert.IsNotInstanceOf<FakeGameInput>(GameInput.Source);
        }
    }
}
