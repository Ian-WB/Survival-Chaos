using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Shakes the view when something lands: the ship is hit, the ship is
    /// lost, the Leviathan loses a part.
    ///
    /// Left out of the game until 16 November 2026, when Ian allowed it. Kept
    /// small for that reason: under a degree at its worst, and gone in half a
    /// second.
    ///
    /// It turns the camera and never moves it. The camera keeps station by
    /// running the ship's own movement from wherever it stands, so a shake
    /// that moved it would be flown from: every knock would leave the camera
    /// a little further round the ring than the ship. A turn is put on after
    /// everything has moved and taken off before anything moves again, so
    /// nothing else ever reads it.
    ///
    /// The amount is "trauma": hits add to it, it drains on its own, and the
    /// shake is its square, so a small knock is barely there and two at once
    /// are more than twice one.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class ScreenShake : MonoBehaviour
    {
        /// <summary>The most the view turns, in degrees, at full trauma.</summary>
        public const float MaxDegrees = 0.9f;

        /// <summary>Trauma lost a second: a full shake is over in about half of one.</summary>
        public const float Drain = 2.2f;

        /// <summary>How fast the shake wanders, in noise cells a second.</summary>
        private const float Speed = 28f;

        /// <summary>Scales every shake, 0 for none. For a setting, when there is one.</summary>
        public static float Strength { get; set; } = 1f;

        private static ScreenShake instance;

        private float trauma;
        private Quaternion applied = Quaternion.identity;
        private bool on;

        /// <summary>
        /// Adds a knock, 0 to 1. Does nothing while a film holds the camera,
        /// under the pause menu, or with no camera to shake.
        /// </summary>
        public static void Add(float amount)
        {
            if (amount <= 0f || Strength <= 0f || ApplyBounds.CameraHeld || PauseMenu.GameIsPaused)
            {
                return;
            }

            if (instance == null)
            {
                Camera view = Camera.main;

                if (view == null)
                {
                    return;
                }

                if (!view.TryGetComponent(out instance))
                {
                    instance = view.gameObject.AddComponent<ScreenShake>();
                }
            }

            instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        /// <summary>How far the view turns for a trauma, in degrees.</summary>
        public static float Degrees(float trauma)
        {
            float t = Mathf.Clamp01(trauma);
            return t * t * MaxDegrees * Mathf.Max(0f, Strength);
        }

        /// <summary>Before anything moves: the last frame's turn comes off.</summary>
        private void Update()
        {
            Remove();
        }

        /// <summary>After everything has moved, the camera included.</summary>
        private void LateUpdate()
        {
            // Real time, so the hit-stop that comes with a hit does not hold
            // the shake still for its length.
            trauma = Mathf.Max(0f, trauma - Drain * Time.unscaledDeltaTime);

            if (trauma <= 0f || Time.timeScale <= 0f || ApplyBounds.CameraHeld)
            {
                return;
            }

            float degrees = Degrees(trauma);
            float t = Time.unscaledTime * Speed;

            applied = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * degrees,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * degrees,
                (Mathf.PerlinNoise(t, t + 5.1f) - 0.5f) * degrees);

            transform.localRotation *= applied;
            on = true;
        }

        private void Remove()
        {
            if (on)
            {
                transform.localRotation *= Quaternion.Inverse(applied);
                on = false;
            }
        }

        private void OnDisable()
        {
            Remove();
            trauma = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Strength = 1f;
        }
    }
}
