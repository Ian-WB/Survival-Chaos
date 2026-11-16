using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// Turns the paused view into a close look at the ship, so the build a
    /// run has made can be seen at a size where it can be read.
    ///
    /// In play the ship is a few dozen pixels across, because the camera
    /// frames the whole band it flies in and has to. The parts the upgrades
    /// add (<see cref="ShipParts"/>) are detail at that size. A pause is the
    /// one moment nothing is being dodged, so that is when the view goes in.
    ///
    /// It turns the camera and narrows its lens, and never moves it, for
    /// the reason <see cref="ScreenShake"/> gives: the camera keeps station
    /// by flying the ship's own movement from where it stands. And nothing
    /// else ever reads what this does: it is put on after everything has
    /// moved, and taken off again the moment the frame has been drawn.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class PauseShipView : MonoBehaviour
    {
        /// <summary>World height the lens takes in at the ship, close in. The ship with every part is about one.</summary>
        public const float FrameHeight = 2f;

        /// <summary>
        /// Where the ship sits in the close view, from the middle of the
        /// screen, as a share of its width and height. Left of the middle:
        /// the pause menu's buttons are in the middle.
        /// </summary>
        public static readonly Vector2 Place = new Vector2(-0.28f, 0.04f);

        /// <summary>How long the view takes to go in, in real seconds.</summary>
        public const float Seconds = 0.45f;

        private static PauseShipView instance;

        private Camera view;
        private Transform ship;
        private float shown;
        private bool wanted;

        private bool on;
        private Quaternion restRotation;
        private float restLens;

        /// <summary>Goes in on the ship. Nothing happens under a film, or with no ship or camera to use.</summary>
        public static void Show()
        {
            if (ApplyBounds.CameraHeld)
            {
                return;
            }

            if (instance == null)
            {
                Camera main = Camera.main;

                if (main == null)
                {
                    return;
                }

                if (!main.TryGetComponent(out instance))
                {
                    instance = main.gameObject.AddComponent<PauseShipView>();
                }

                instance.view = main;
            }

            if (instance.ship == null)
            {
                Player player = FindAnyObjectByType<Player>();
                instance.ship = player != null ? player.transform : null;
            }

            instance.wanted = instance.ship != null;
        }

        /// <summary>Back to the game's own view, at once: play resumes on the frame this is called.</summary>
        public static void Hide()
        {
            if (instance != null)
            {
                instance.wanted = false;
                instance.shown = 0f;
            }
        }

        /// <summary>The lens, in degrees, that takes in <paramref name="height"/> world units at a distance.</summary>
        public static float LensFor(float height, float distance)
        {
            return 2f * Mathf.Atan2(height * 0.5f, Mathf.Max(0.01f, distance)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The rotation that puts a point, seen along <paramref name="toward"/>,
        /// at <paramref name="place"/> on the screen: shares of the width and
        /// height from the middle, right and up.
        /// </summary>
        public static Quaternion Aim(Vector3 toward, Vector2 place, float lens, float aspect)
        {
            float halfHeight = Mathf.Tan(lens * 0.5f * Mathf.Deg2Rad);
            var inView = new Vector3(place.x * 2f * halfHeight * aspect, place.y * 2f * halfHeight, 1f);

            return Quaternion.LookRotation(toward, Vector3.up)
                * Quaternion.Inverse(Quaternion.LookRotation(inView, Vector3.up));
        }

        private void OnEnable()
        {
            RenderPipelineManager.endContextRendering += Drawn;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endContextRendering -= Drawn;
            Restore();
            wanted = false;
            shown = 0f;
        }

        /// <summary>After everything has moved and every other view change is on.</summary>
        private void LateUpdate()
        {
            Restore();

            if (!wanted || !PauseMenu.GameIsPaused || ApplyBounds.CameraHeld || ship == null || view == null)
            {
                wanted = wanted && PauseMenu.GameIsPaused;
                shown = 0f;
                return;
            }

            shown = Mathf.Clamp01(shown + Time.unscaledDeltaTime / Seconds);

            // Fast at first and settling, like something being brought in.
            float eased = 1f - (1f - shown) * (1f - shown) * (1f - shown);

            Vector3 toward = ship.position - transform.position;

            if (toward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            restRotation = transform.rotation;
            restLens = view.fieldOfView;

            float closeLens = Mathf.Min(restLens, LensFor(FrameHeight, toward.magnitude));
            float lens = Mathf.Lerp(restLens, closeLens, eased);

            transform.rotation = Quaternion.Slerp(restRotation, Aim(toward, Place, lens, view.aspect), eased);
            view.fieldOfView = lens;
            on = true;
        }

        private void Drawn(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            Restore();
        }

        private void Restore()
        {
            if (!on)
            {
                return;
            }

            transform.rotation = restRotation;

            if (view != null)
            {
                view.fieldOfView = restLens;
            }

            on = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }
    }
}
