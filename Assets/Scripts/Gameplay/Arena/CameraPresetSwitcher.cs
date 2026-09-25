using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Puts the main camera into one of <see cref="CameraFraming.Presets"/>.
    /// It sits on the Game scene's camera and starts every run on
    /// <see cref="CameraFraming.DefaultIndex"/>; the debug menu cycles through
    /// the rest, and adds this to a camera that lacks it.
    ///
    /// It moves only what a preset names: the camera's distance outside the
    /// lane, through its own SnapToOrbit, and its field of view. Round the ring
    /// it keeps station with the ship as it always has, because PlayerMovement
    /// converts the orbit speed through the lane's radius rather than the
    /// camera's. Up and down it either keeps climbing with the ship, or holds
    /// the middle of the band - put back after PlayerMovement and ApplyBounds
    /// have moved it each frame, which is why this is LateUpdate. Holding the
    /// band, it also fits its lens to the band's live height each frame, so
    /// PlayerBounds made taller or shorter is framed whole from the same
    /// distance (<see cref="CameraPreset.FieldOfViewOn"/>), with equal room
    /// above and below the band for the HUD (<see cref="CameraFraming.TopHudShare"/>).
    ///
    /// Nothing here touches what is heard. The audio listener is on the ship,
    /// not the camera, since the default went from 5 out to 10 on 24 September
    /// 2026: four sounds are partly positional - the lance and ram charges, the
    /// boss's shot and an enemy dying - and a listener on a camera further back
    /// made them quieter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraPresetSwitcher : MonoBehaviour
    {
        private Camera view;
        private SnapToOrbit snap;
        private ApplyBounds bounds;
        private int current;

        /// <summary>
        /// Whether a preset has been put on the camera yet. The debug menu adds
        /// this component and selects in the same call, and Start must not then
        /// put the default back over its choice.
        /// </summary>
        private bool applied;

        /// <summary>
        /// The preset in use on the main camera. A camera without this component
        /// counts as the classic one, which is what it was authored at before the
        /// default moved.
        /// </summary>
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

        /// <summary>
        /// Starts the run on <see cref="CameraFraming.DefaultIndex"/>. In Start
        /// rather than Awake, so SnapToOrbit has placed the camera from its own
        /// authored offset first and cannot move it back afterwards.
        /// </summary>
        private void Start()
        {
            if (!applied)
            {
                Apply(CameraFraming.DefaultIndex);
            }
        }

        private void Apply(int index)
        {
            CameraPreset was = CameraFraming.Presets[current];
            CameraPreset preset = CameraFraming.Presets[index];
            current = index;
            applied = true;

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
            CameraPreset preset = CameraFraming.Presets[current];

            if (!preset.HoldsBandMiddle || bounds == null
                || !bounds.TryGetBand(out float floor, out float ceiling))
            {
                return;
            }

            // Level with the band's middle and still looking level at the axis:
            // PlayerMovement's LookAt aims at the camera's own height, so moving
            // straight up or down leaves the view level.
            Vector3 position = transform.position;
            position.y = SpawnBand.Middle(floor, ceiling);
            transform.position = position;

            if (view != null)
            {
                view.fieldOfView = preset.FieldOfViewOn(ceiling - floor);
            }
        }
    }
}
