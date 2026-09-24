using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The Deflector upgrade's one charge: it takes a hit in place of the hull,
    /// and comes back once the player has gone a while without being hit.
    ///
    /// Recharged by a clean stretch rather than on a fixed timer, because that is
    /// what makes it different from more health. Max HP pays out however badly
    /// the player is flying; this pays out only to someone who has stopped being
    /// hit, so it rewards the flying rather than padding it. Every hit restarts
    /// the wait, the one it blocked included, and so does every hit it could not.
    ///
    /// No Unity object attached, so the timing can be tested directly - the same
    /// split <see cref="DashCycle"/> makes.
    /// </summary>
    public sealed class DeflectorCharge
    {
        /// <summary>
        /// When the player was last hit. Negative infinity puts that infinitely
        /// far in the past, which is exactly a charge granted full.
        /// </summary>
        private float lastHitAt = float.NegativeInfinity;

        public DeflectorCharge(float rechargeSeconds)
        {
            SetRecharge(rechargeSeconds);
        }

        /// <summary>Seconds without being hit before the charge is back.</summary>
        public float Recharge { get; private set; }

        /// <summary>
        /// Changes how long the charge takes to come back, for a later pick.
        /// Applies to a wait already running, the way the dash's cooldown does.
        /// </summary>
        public void SetRecharge(float seconds)
        {
            // Not zero: a charge that is back the instant it is spent blocks
            // every hit, which is invulnerability with extra steps.
            Recharge = Mathf.Max(0.1f, seconds);
        }

        /// <summary>True when the next hit will be blocked.</summary>
        public bool IsCharged(float now)
        {
            return now >= lastHitAt + Recharge;
        }

        /// <summary>
        /// How far the charge has come back, 0 to 1. For the HUD bar, which
        /// should not have to know the recharge length to draw it.
        /// </summary>
        public float ReadyFraction(float now)
        {
            return Mathf.Clamp01((now - lastHitAt) / Recharge);
        }

        /// <summary>
        /// Reports a hit, and whether the charge took it. Either way the wait
        /// starts again from now.
        ///
        /// One call for both cases rather than a check and a spend, so there is
        /// no way to block a hit without restarting the wait, or to take one
        /// without the charge hearing about it.
        /// </summary>
        public bool TryAbsorb(float now)
        {
            bool charged = IsCharged(now);
            lastHitAt = now;
            return charged;
        }
    }
}
