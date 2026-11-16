using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class SpringTests
    {
        private static float Run(float frequency, float damping, float seconds, float step,
            out float velocity, float start = 0f, float target = 10f, float kick = 0f)
        {
            var spring = new Spring(start);
            spring.Kick(kick);

            int steps = Mathf.RoundToInt(seconds / step);
            for (int i = 0; i < steps; i++)
            {
                spring.Step(target, frequency, damping, step);
            }

            velocity = spring.Velocity;
            return spring.Value;
        }

        [Test]
        public void ASpring_SettlesOnItsTarget([Values(0.4f, 1f, 1.8f)] float damping)
        {
            float value = Run(3f, damping, 6f, 1f / 60f, out float velocity);

            Assert.AreEqual(10f, value, 0.01f);
            Assert.AreEqual(0f, velocity, 0.05f);
        }

        [Test]
        public void ASpring_LandsInTheSamePlace_WhateverTheFrameRate([Values(0.4f, 1f, 1.8f)] float damping)
        {
            // The reason it is solved and not stepped: one long frame and many
            // short ones covering the same time are the same spring.
            float coarse = Run(3f, damping, 0.3f, 0.3f, out float coarseVelocity);
            float fine = Run(3f, damping, 0.3f, 0.001f, out float fineVelocity);

            Assert.AreEqual(fine, coarse, 0.01f);
            Assert.AreEqual(fineVelocity, coarseVelocity, 0.2f);
        }

        [Test]
        public void ASpring_StaysPut_ThroughAStallOfAWholeSecond()
        {
            // A stepped spring at 6 swings a second gains energy on a step
            // this long and leaves the screen.
            var spring = new Spring(0f);
            spring.Kick(50f);
            spring.Step(0f, 6f, 0.5f, 1f);

            Assert.Less(Mathf.Abs(spring.Value), 0.01f);
            Assert.Less(Mathf.Abs(spring.Velocity), 0.01f);
        }

        [Test]
        public void AnUnderdampedSpring_PassesItsTarget_AndACriticalOneDoesNot()
        {
            float loosePeak = 0f;
            float tightPeak = 0f;
            var loose = new Spring(0f);
            var tight = new Spring(0f);

            for (int i = 0; i < 600; i++)
            {
                loose.Step(10f, 3f, 0.4f, 1f / 120f);
                tight.Step(10f, 3f, 1f, 1f / 120f);
                loosePeak = Mathf.Max(loosePeak, loose.Value);
                tightPeak = Mathf.Max(tightPeak, tight.Value);
            }

            Assert.Greater(loosePeak, 10.5f);
            Assert.LessOrEqual(tightPeak, 10.0001f);
        }

        [Test]
        public void AKick_MovesTheValue_AndItComesBack()
        {
            var spring = new Spring(0f);
            spring.Kick(4f);

            float furthest = 0f;
            for (int i = 0; i < 240; i++)
            {
                spring.Step(0f, 5f, 0.5f, 1f / 120f);
                furthest = Mathf.Max(furthest, spring.Value);
            }

            // 0.546 * speed / (2 pi * frequency) at a damping of a half.
            Assert.AreEqual(0.0695f, furthest, 0.004f);
            Assert.AreEqual(0f, spring.Value, 0.001f);
        }

        [Test]
        public void NoFrequency_SnapsToTheTarget()
        {
            var spring = new Spring(0f);
            spring.Kick(9f);
            spring.Step(10f, 0f, 0.5f, 0.016f);

            Assert.AreEqual(10f, spring.Value);
            Assert.AreEqual(0f, spring.Velocity);
        }

        [Test]
        public void NoTime_ChangesNothing()
        {
            var spring = new Spring(3f);
            spring.Kick(2f);
            spring.Step(10f, 3f, 0.5f, 0f);

            Assert.AreEqual(3f, spring.Value);
            Assert.AreEqual(2f, spring.Velocity);
        }
    }
}
