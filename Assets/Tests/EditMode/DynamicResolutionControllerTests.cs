using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The controller's own rationale is that a thousand simulated frames run in
    /// a millisecond here, where the same behaviour takes a build and a stopwatch
    /// to judge by eye. What matters is not that it moves - it is that it settles:
    /// a controller which hunts by two percent forever reads fine in code and is
    /// maddening on screen.
    /// </summary>
    public class DynamicResolutionControllerTests
    {
        private const float Step = 1f / 60f;

        /// <summary>60 FPS, as milliseconds per frame.</summary>
        private const float Target60 = 1000f / 60f;

        /// <summary>
        /// Runs the loop closed: the scene costs what it costs at native, and the
        /// cost falls with the square of the scale because that is how pixels
        /// work. This is what makes the test meaningful - feeding a fixed frame
        /// time would only prove the controller moves in the right direction, not
        /// that it stops.
        /// </summary>
        private static void Simulate(
            DynamicResolutionController controller,
            float costAtNativeMs,
            float targetMs,
            float seconds)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                float frameMs = costAtNativeMs * controller.Scale * controller.Scale;
                controller.Update(frameMs, targetMs, Step);
            }
        }

        /// <summary>The widest swing in scale over a run, after it has had time to settle.</summary>
        private static float SwingAfterSettling(
            DynamicResolutionController controller,
            float costAtNativeMs,
            float targetMs,
            float settleSeconds,
            float measureSeconds)
        {
            Simulate(controller, costAtNativeMs, targetMs, settleSeconds);

            float low = controller.Scale;
            float high = controller.Scale;

            for (float t = 0f; t < measureSeconds; t += Step)
            {
                float frameMs = costAtNativeMs * controller.Scale * controller.Scale;
                controller.Update(frameMs, targetMs, Step);

                if (controller.Scale < low) low = controller.Scale;
                if (controller.Scale > high) high = controller.Scale;
            }

            return high - low;
        }

        [Test]
        public void StartsAtNative()
        {
            Assert.AreEqual(
                DynamicResolutionController.MaxScale,
                new DynamicResolutionController().Scale,
                0.0001f);
        }

        [Test]
        public void MissingTheTarget_DropsTheScale()
        {
            var controller = new DynamicResolutionController();

            controller.Update(frameMs: 40f, targetMs: Target60, deltaSeconds: Step);

            Assert.Less(controller.Scale, DynamicResolutionController.MaxScale);
        }

        [Test]
        public void BeatingTheTarget_ClimbsBackToNative()
        {
            var controller = new DynamicResolutionController();
            Simulate(controller, costAtNativeMs: 40f, targetMs: Target60, seconds: 2f);
            Assert.Less(controller.Scale, 0.9f, "should have dropped first");

            // The scene becomes cheap - the fight ends, the screen empties.
            for (float t = 0f; t < 10f; t += Step)
            {
                controller.Update(4f, Target60, Step);
            }

            Assert.AreEqual(DynamicResolutionController.MaxScale, controller.Scale, 0.0001f);
        }

        [Test]
        public void InsideTheDeadband_NothingMoves()
        {
            var controller = new DynamicResolutionController();

            // Comfortably within Deadband of the target, either side.
            for (float t = 0f; t < 5f; t += Step)
            {
                controller.Update(Target60 * 1.02f, Target60, Step);
                controller.Update(Target60 * 0.98f, Target60, Step);
            }

            Assert.AreEqual(DynamicResolutionController.MaxScale, controller.Scale, 0.0001f,
                "a frame time this close to target is not evidence of anything");
        }

        [Test]
        public void DropsFasterThanItClimbs()
        {
            // Must stay under the half-second stall threshold. A step above it is
            // discarded as a shader compile rather than acted on, which is what
            // LongStallsAreIgnored covers - pass 1f here and both sides measure
            // zero and the comparison is meaningless.
            const float Slice = 0.4f;

            var dropping = new DynamicResolutionController();
            var climbing = new DynamicResolutionController();

            dropping.Update(40f, Target60, Slice);

            // Put the climber somewhere it has room to climb from, then give it
            // the same slice of time going the other way.
            climbing.Update(40f, Target60, Slice);
            float before = climbing.Scale;
            climbing.Update(1f, Target60, Slice);

            float dropped = DynamicResolutionController.MaxScale - dropping.Scale;
            float climbed = climbing.Scale - before;

            Assert.Greater(dropped, 0f, "the drop should have happened at all");
            Assert.Greater(climbed, 0f, "the climb should have happened at all");
            Assert.Greater(dropped, climbed,
                "being slow is what the player feels now; climbing back is a luxury");
        }

        [Test]
        public void NeverGoesBelowTheFloor()
        {
            var controller = new DynamicResolutionController();

            // Hopelessly over budget, for a long time.
            Simulate(controller, costAtNativeMs: 500f, targetMs: Target60, seconds: 30f);

            Assert.AreEqual(DynamicResolutionController.MinScale, controller.Scale, 0.0001f);
        }

        [Test]
        public void NeverGoesAboveNative()
        {
            var controller = new DynamicResolutionController();

            Simulate(controller, costAtNativeMs: 2f, targetMs: Target60, seconds: 30f);

            Assert.AreEqual(DynamicResolutionController.MaxScale, controller.Scale, 0.0001f);
        }

        [Test]
        public void LongStallsAreIgnored()
        {
            var controller = new DynamicResolutionController();

            // A shader compile or a level load. Not evidence the scene is heavy.
            controller.Update(frameMs: 2000f, targetMs: Target60, deltaSeconds: 2f);

            Assert.AreEqual(DynamicResolutionController.MaxScale, controller.Scale, 0.0001f);
        }

        [Test]
        public void NonsenseInputsChangeNothing()
        {
            var controller = new DynamicResolutionController();
            Simulate(controller, costAtNativeMs: 40f, targetMs: Target60, seconds: 1f);
            float settled = controller.Scale;

            controller.Update(40f, 0f, Step);       // no target
            controller.Update(0f, Target60, Step);  // no frame time
            controller.Update(40f, Target60, 0f);   // no time passed

            Assert.AreEqual(settled, controller.Scale, 0.0001f);
        }

        [Test]
        public void Reset_ReturnsToNative()
        {
            var controller = new DynamicResolutionController();
            Simulate(controller, costAtNativeMs: 60f, targetMs: Target60, seconds: 3f);
            Assert.Less(controller.Scale, 0.9f);

            controller.Reset();

            Assert.AreEqual(DynamicResolutionController.MaxScale, controller.Scale, 0.0001f);
        }

        [Test]
        public void SettlesNearTheScaleThatMeetsTheTarget()
        {
            var controller = new DynamicResolutionController();

            // 20 ms at native against a 16.67 ms budget: cost falls with scale
            // squared, so equilibrium sits at sqrt(16.67 / 20) = 0.913.
            Simulate(controller, costAtNativeMs: 20f, targetMs: Target60, seconds: 5f);

            Assert.AreEqual(0.913f, controller.Scale, 0.06f,
                "should land within the deadband of the scale that meets budget");
        }

        [Test]
        public void DoesNotPumpOnceSettled()
        {
            var controller = new DynamicResolutionController();

            float swing = SwingAfterSettling(
                controller, costAtNativeMs: 20f, targetMs: Target60,
                settleSeconds: 5f, measureSeconds: 5f);

            // The failure this guards against is a scale that breathes: a scene
            // briefly cheap pulls the resolution up, the next frame is expensive
            // again, and the image visibly pulses.
            Assert.Less(swing, 0.02f, "the resolution should hold still, not hunt");
        }

        [Test]
        public void SettlesFromAnyStartingCost()
        {
            // Costs from comfortably inside budget to five times over it.
            foreach (float cost in new[] { 8f, 16f, 25f, 40f, 80f })
            {
                var controller = new DynamicResolutionController();

                float swing = SwingAfterSettling(
                    controller, cost, Target60, settleSeconds: 8f, measureSeconds: 3f);

                Assert.Less(swing, 0.02f, $"still hunting at {cost} ms native cost");
            }
        }
    }
}
