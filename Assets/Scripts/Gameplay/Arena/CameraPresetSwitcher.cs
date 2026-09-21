using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Puts the main camera into one of <see cref="CameraFraming.Presets"/>,
    /// for trying them in play. Driven from the debug menu, which adds this to
    /// the camera the first time it is asked.
    ///
    /// It moves only what a preset names: the camera's distance outside the
    /// lane, through its own SnapToOrbit, and its field of view. Round the ring
    /// it keeps station with the ship as it always has, because PlayerMovement
    /// converts the orbit speed through the lane's radius rather than the
    /// camera's. Up and down it either keeps climbing with the ship, or holds
    /// the middle of the band - put back after PlayerMovement and ApplyBounds
    /// have moved it each frame, which is why this is LateUpdate.
    ///
    /// Leaves the audio listener where it is, on the camera. Four sounds are
    /// partly positional - the lance and ram charges, the boss's shot and an
    /// enemy dying - and those are a little quieter from further back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraPresetSwitcher : MonoBehaviour
    {
        private Camera view;
        private SnapToOrbit snap;
        private ApplyBounds bounds;
        private int current;

        /// <summary>The preset in use on the main camera, or the classic one before any switch.</summary>
        public static CameraPreset Current
        {
            get
            {
                Camera main = Camera.main;
                return main != null && main.TryGetComponent(out CameraPresetSwitcher switcher)
                    ? CameraFraming.Presets[switcher.current]
                    : CameraFraming.Presets[0];
            }
        }

        /// <summary>Moves the main camera on to the next preset, wrapping round.</summary>
        public static void Cycle()
        {
            Select(-1);
        }

        /// <summary>
        /// Puts the main camera into preset <paramref name="index"/>, or the next
        /// one after the current when it is negative.
        /// </summary>
        public static void Select(int index)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            if (!main.TryGetComponent(out CameraPresetSwitcher switcher))
            {
                switcher = main.gameObject.AddComponent<CameraPresetSwitcher>();
            }

            int count = CameraFraming.Presets.Length;
            switcher.Apply(index < 0 ? (switcher.current + 1) % count : Mathf.Clamp(index, 0, count - 1));
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            snap = GetComponent<SnapToOrbit>();
            bounds = GetComponent<ApplyBounds>();
        }

        private void Apply(int index)
        {
            CameraPreset was = CameraFraming.Presets[current];
            CameraPreset preset = CameraFraming.Presets[index];
            current = index;

            if (snap != null)
            {
                snap.SetRadiusOffset(preset.Distance);
            }

            if (view != null)
            {
                view.fieldOfView = preset.FieldOfView;
            }

            // Back from holding the band's middle: the camera has to rejoin the
            // ship's height, or it would follow the ship's climbs from wherever it
            // was left and frame it off-centre for the rest of the run.
            if (was.HoldsBandMiddle && !preset.HoldsBandMiddle)
            {
                Player player = FindAnyObjectByType<Player>();
                if (player != null)
                {
                    Vector3 position = transform.position;
                    position.y = player.transform.position.y;
                    transform.position = position;
                }
            }

            LateUpdate();
        }

        private void LateUpdate()
        {
            if (!CameraFraming.Presets[current].HoldsBandMiddle || bounds == null
                || !bounds.TryGetBand(out float floor, out float ceiling))
            {
                return;
            }

            // Level with the band's middle and still looking level at the axis:
            // PlayerMovement's LookAt aims at the camera's own height, so moving
            // straight up or down leaves the view level.
            Vector3 position = transform.position;
            position.y = (floor + ceiling) * 0.5f;
            transform.position = position;
        }
    }
}
