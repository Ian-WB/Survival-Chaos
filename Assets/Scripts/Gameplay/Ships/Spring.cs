using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One value on a damped spring: it chases a target, carries its own speed,
    /// and can be knocked.
    ///
    /// <see cref="ShipMotion.Approach"/> eases a value toward a target and
    /// forgets how fast it was going, which is why everything the ships did
    /// with it arrived the same way: quickly at first, slowly at the end, never
    /// past the mark. A spring keeps the speed. So a ship can lean a little too
    /// far and come back, a recoil is a knock to the speed and not a second
    /// animation to blend, and two things asking for the same axis add up
    /// without either knowing about the other.
    ///
    /// Solved exactly for the step, not integrated. A stepped spring gains
    /// energy when the step is long against its period, and a hitch or a
    /// capture hands this a step of a third of a second: the stepped form
    /// would throw the ship off the screen on the frame after a stall. The
    /// closed form lands where the real spring would be, whatever the step.
    /// </summary>
    [System.Serializable]
    public struct Spring
    {
        public float Value;
        public float Velocity;

        public Spring(float value)
        {
            Value = value;
            Velocity = 0f;
        }

        /// <summary>Moves one step toward <paramref name="target"/>.</summary>
        /// <param name="frequency">
        /// Cycles a second with no damping. Higher is stiffer. 0 or less snaps
        /// to the target, the way a response of 0 does in ShipMotion.
        /// </param>
        /// <param name="damping">
        /// 1 arrives as fast as it can without passing the target. Below 1 it
        /// overshoots and settles, above 1 it creeps.
        /// </param>
        public void Step(float target, float frequency, float damping, float deltaTime)
        {
            Step(ref Value, ref Velocity, target, frequency, damping, deltaTime);
        }

        /// <summary>A knock: added to the speed, in units a second.</summary>
        public void Kick(float impulse)
        {
            Velocity += impulse;
        }

        /// <summary>Puts the spring at rest on <paramref name="value"/>.</summary>
        public void Reset(float value)
        {
            Value = value;
            Velocity = 0f;
        }

        public static void Step(ref float value, ref float velocity, float target,
            float frequency, float damping, float deltaTime)
        {
            if (frequency <= 0f)
            {
                value = target;
                velocity = 0f;
                return;
            }

            if (deltaTime <= 0f)
            {
                return;
            }

            float omega = frequency * 2f * Mathf.PI;
            float zeta = Mathf.Max(0f, damping);
            float offset = value - target;

            if (zeta < 0.9999f)
            {
                // Underdamped: a decaying swing about the target.
                float decay = zeta * omega;
                float swing = omega * Mathf.Sqrt(1f - zeta * zeta);
                float envelope = Mathf.Exp(-decay * deltaTime);
                float cos = Mathf.Cos(swing * deltaTime);
                float sin = Mathf.Sin(swing * deltaTime);
                float lead = (velocity + decay * offset) / swing;

                value = target + envelope * (offset * cos + lead * sin);
                velocity = envelope * ((lead * swing - decay * offset) * cos
                                       - (decay * lead + offset * swing) * sin);
            }
            else if (zeta <= 1.0001f)
            {
                // Critical: the fastest arrival that never passes the target.
                float envelope = Mathf.Exp(-omega * deltaTime);
                float lead = velocity + omega * offset;

                value = target + envelope * (offset + lead * deltaTime);
                velocity = envelope * (velocity - omega * lead * deltaTime);
            }
            else
            {
                // Overdamped: two decays, a fast one and a slow one.
                float root = omega * Mathf.Sqrt(zeta * zeta - 1f);
                float slow = -omega * zeta + root;
                float fast = -omega * zeta - root;
                float fastShare = (velocity - slow * offset) / (fast - slow);
                float slowShare = offset - fastShare;
                float slowDecay = Mathf.Exp(slow * deltaTime);
                float fastDecay = Mathf.Exp(fast * deltaTime);

                value = target + slowShare * slowDecay + fastShare * fastDecay;
                velocity = slowShare * slow * slowDecay + fastShare * fast * fastDecay;
            }
        }
    }
}
