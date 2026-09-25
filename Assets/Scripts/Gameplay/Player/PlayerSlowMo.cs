using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Slows the whole game for a few seconds when its button is pressed, then
    /// waits before it can be used again. Nothing until the first Slow Mo pick,
    /// which <see cref="Player.UpgradeSlowMo"/> hands on as <see cref="Grant"/>.
    ///
    /// Everything slows, the ship included, through the time scale: what it
    /// buys is time to see and steer, not speed. Enemies, rounds, the boss's
    /// telegraphs and the dash's own burst all run at the same fraction, so
    /// nothing is dodged that could not have been at full speed by someone
    /// quick enough.
    ///
    /// Timed on its own clock of unpaused real seconds rather than
    /// Time.time. The game's clock is the thing being slowed, so three seconds
    /// of it at 0.4 would last seven and a half; and Time.unscaledTime keeps
    /// running behind the pause menu, which would let a slowdown expire and a
    /// wait run down while the game is stopped.
    ///
    /// The speed itself belongs to <see cref="RunTime"/>, which multiplies it
    /// with the debug menu's and still answers to pause and to the run ending.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSlowMo : MonoBehaviour
    {
        [SerializeField]
        [Range(0.5f, 10f)]
        [Tooltip("How long one slowdown lasts, in real seconds.")]
        private float duration = 3f;

        [SerializeField]
        [Range(0.1f, 0.9f)]
        [Tooltip("The game's speed during a slowdown, as a fraction of normal. 0.4 is two and a " +
                 "half times as long to react to anything.")]
        private float slowScale = 0.4f;

        /// <summary>Null until the first Slow Mo pick.</summary>
        private SlowMoCycle cycle;

        /// <summary>Unpaused real seconds since this component woke. The cycle's clock.</summary>
        private float clock;

        /// <summary>
        /// Whether this object is the one holding the game slowed, so the end
        /// of a slowdown is noticed once and a disabled ship does not leave the
        /// game running slow.
        /// </summary>
        private bool holdingSlow;

        /// <summary>Whether a Slow Mo pick has been taken this run. Read by the HUD.</summary>
        public bool Held => cycle != null;

        /// <summary>True while the game is slowed by this.</summary>
        public bool Slowing => cycle != null && cycle.IsSlowing(clock);

        /// <summary>True when held and a slowdown may begin.</summary>
        public bool Ready => cycle != null && cycle.IsReady(clock);

        /// <summary>What the HUD bar shows - see <see cref="SlowMoCycle.Gauge"/>. 0 when not held.</summary>
        public float Gauge => cycle != null ? cycle.Gauge(clock) : 0f;

        /// <summary>Seconds from one use to the next as things stand, or 0 when not held.</summary>
        public float Cooldown => cycle != null ? cycle.Cooldown : 0f;

        /// <summary>
        /// Grants the ability ready to use, or, once it is held, changes the
        /// wait between uses to <paramref name="cooldown"/>.
        /// </summary>
        public void Grant(float cooldown)
        {
            if (cycle == null)
            {
                cycle = new SlowMoCycle(duration, cooldown);
            }
            else
            {
                cycle.SetCooldown(cooldown);
            }
        }

        /// <summary>
        /// Starts a slowdown if one is held and ready, as the button does.
        /// Public for the button's own path and for tools; returns whether it
        /// started.
        /// </summary>
        public bool TryActivate()
        {
            if (cycle == null || PauseMenu.GameIsPaused || RunOutcome.RunEnded || !cycle.TryBegin(clock))
            {
                return false;
            }

            holdingSlow = true;
            RunTime.SetAbilityScale(slowScale);
            return true;
        }

        /// <summary>Puts the game back to speed if this goes while a slowdown runs.</summary>
        private void OnDisable()
        {
            ReleaseSlow();
        }

        private void Update()
        {
            if (PauseMenu.GameIsPaused || RunOutcome.RunEnded)
            {
                return;
            }

            // Clamped the way Unity clamps game time, so a hitch - a level load,
            // a breakpoint - cannot swallow a slowdown in one frame.
            clock += Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);

            if (holdingSlow && !cycle.IsSlowing(clock))
            {
                ReleaseSlow();
            }

            if (GameInput.SlowMoPressed)
            {
                TryActivate();
            }
        }

        private void ReleaseSlow()
        {
            if (!holdingSlow)
            {
                return;
            }

            holdingSlow = false;
            RunTime.SetAbilityScale(1f);
        }
    }
}
