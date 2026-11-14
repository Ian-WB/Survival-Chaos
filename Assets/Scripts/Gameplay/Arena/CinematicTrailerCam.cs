#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
// Trailer tooling: compiled only where the debug menu is (the editor and Checked builds), so no
// release build carries its hotkeys. The stub at the bottom keeps the callers compiling.
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalChaos
{
    /// <summary>
    /// Cinematic camera and recording director for capturing trailers, b-roll, and promotional footage.
    ///
    /// Features:
    /// - Dynamic Dutch tilt / banking camera roll during high-speed orbital flight.
    /// - Live on-screen holographic camera HUD badges when switching modes or adjusting time scale.
    /// - Hotkeys (active in Game scene during play mode):
    ///     - F9: Toggle HUD visibility on/off (clean recording with no UI clutter).
    ///     - F10: Step through the cinematic camera perspectives:
    ///         [0] Gameplay Camera (standard preset station)
    ///         [1] Low-Angle Chase Cam (tight rear angle close to engines)
    ///         [2] Side-Profile Orbit Cam (dramatic sideways angle across lane)
    ///         [3] High Caldera Sweeper (slow 360° majestic orbit overlooking volcano and storm)
    ///         [4] Forward Target Cam (looking ahead towards incoming enemy streams and boss)
    ///     - F5: Step the speed 1x, 0.5x, 0.25x (marks the run assisted)
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class CinematicTrailerCam : MonoBehaviour
    {
        public enum CamMode
        {
            StandardGameplay = 0,
            LowAngleChase = 1,
            SideProfileOrbit = 2,
            CalderaSweeper = 3,
            ForwardTarget = 4
        }

        private static CinematicTrailerCam instance;
        private static bool isActive;

        /// <summary>True when a custom cinematic camera mode is active.</summary>
        public static bool IsActive => isActive;

        /// <summary>Current active camera mode.</summary>
        public static CamMode CurrentMode { get; private set; } = CamMode.StandardGameplay;

        private Camera cam;
        private Player player;
        private Canvas hudCanvas;
        private bool hudHidden;

        /// <summary>The camera preset to hand back, read as a free mode takes the camera.</summary>
        private int heldPreset = CameraFraming.DefaultIndex;

        private float sweeperAngle;
        private float originalFov = 78.6f;

        // On-screen notification badge state
        private string badgeText = "";
        private float badgeTimer = 0f;
        private const float BadgeDuration = 2.0f;
        private GUIStyle badgeStyle;
        private Texture2D badgeBgTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null || !Application.isPlaying)
            {
                return;
            }

            GameObject host = new GameObject("Cinematic Trailer Cam");
            instance = host.AddComponent<CinematicTrailerCam>();
            DontDestroyOnLoad(host);

            // sceneLoaded has already fired for the scene the game started in.
            instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance = null;
            isActive = false;
            CurrentMode = CamMode.StandardGameplay;
        }

        private void Awake()
        {
            useGUILayout = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            if (badgeBgTexture != null)
            {
                Destroy(badgeBgTexture);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Game")
            {
                isActive = false;
                CurrentMode = CamMode.StandardGameplay;
                badgeTimer = 0f;
                return;
            }

            cam = Camera.main;
            player = FindAnyObjectByType<Player>();

            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                hudCanvas = canvasGo.GetComponent<Canvas>();
            }

            if (cam != null)
            {
                originalFov = cam.fieldOfView;
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "Game")
            {
                isActive = false;
                CurrentMode = CamMode.StandardGameplay;
                badgeTimer = 0f;
                cam = null;
                player = null;
                hudCanvas = null;
            }
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "Game")
            {
                return;
            }

            if (badgeTimer > 0f)
            {
                badgeTimer -= CaptureClock.UnscaledDelta;
            }

            // Ensure references
            if (cam == null)
            {
                cam = Camera.main;
            }
            if (player == null)
            {
                player = FindAnyObjectByType<Player>();
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            // HUD Toggle (F9)
            if (kb.f9Key.wasPressedThisFrame)
            {
                ToggleHud();
            }

            // Speed (F5 steps 1x, 0.5x, 0.25x). F7, F8 and the digits belong to the debug menu.
            if (kb.f5Key.wasPressedThisFrame)
            {
                RunRecords.MarkAssisted();
                RunTime.CycleSlowMotion();
                ShowBadge($"[ SPEED: {RunTime.RequestedSpeed:0.##}x ]");
            }

            // Camera (F10 steps through the five modes)
            if (kb.f10Key.wasPressedThisFrame)
            {
                int next = ((int)CurrentMode + 1) % 5;
                SetMode((CamMode)next);
            }
#endif
        }

        private void LateUpdate()
        {
            if (!isActive || cam == null || player == null || CinematicTrailerDirector.IsRunning)
            {
                return;
            }

            Vector3 playerPos = player.transform.position;
            float laneRadius = ArenaGeometry.LaneRadius;
            float playerBearing = Mathf.Atan2(playerPos.x, playerPos.z);
            float dt = CaptureClock.UnscaledDelta;

            switch (CurrentMode)
            {
                case CamMode.LowAngleChase:
                {
                    // Sits close behind and slightly below the ship, banking dynamically into turns
                    float chaseRadius = laneRadius + 3.8f;
                    float chaseHeight = playerPos.y - 0.35f;
                    Vector3 chasePos = new Vector3(
                        Mathf.Sin(playerBearing) * chaseRadius,
                        chaseHeight,
                        Mathf.Cos(playerBearing) * chaseRadius);

                    cam.transform.position = Vector3.Lerp(cam.transform.position, chasePos, dt * 10f);
                    Vector3 lookTarget = playerPos + player.transform.forward * 4.2f + Vector3.up * 0.25f;
                    Vector3 forward = (lookTarget - cam.transform.position).normalized;

                    // Dynamic banking roll: -6° into the orbital turn
                    Quaternion baseRot = Quaternion.LookRotation(forward, Vector3.up);
                    cam.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, -6.5f);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 65f, dt * 5f);
                    break;
                }

                case CamMode.SideProfileOrbit:
                {
                    // Sits 40 degrees to the side of the ship, looking across its flight profile
                    float sideAngle = playerBearing + 42f * Mathf.Deg2Rad;
                    float sideRadius = laneRadius + 5.2f;
                    Vector3 sidePos = new Vector3(
                        Mathf.Sin(sideAngle) * sideRadius,
                        playerPos.y + 0.45f,
                        Mathf.Cos(sideAngle) * sideRadius);

                    cam.transform.position = Vector3.Lerp(cam.transform.position, sidePos, dt * 8f);
                    Vector3 forward = (playerPos - cam.transform.position).normalized;
                    Quaternion baseRot = Quaternion.LookRotation(forward, Vector3.up);
                    cam.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, 4.0f);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, dt * 5f);
                    break;
                }

                case CamMode.CalderaSweeper:
                {
                    // High altitude, slowly revolving around the volcanic caldera and storm wall
                    sweeperAngle += dt * 0.15f;
                    float sweepRadius = laneRadius + 16f;
                    float sweepHeight = playerPos.y + 16f;
                    Vector3 sweepPos = new Vector3(
                        Mathf.Sin(sweeperAngle) * sweepRadius,
                        sweepHeight,
                        Mathf.Cos(sweeperAngle) * sweepRadius);

                    cam.transform.position = Vector3.Lerp(cam.transform.position, sweepPos, dt * 3f);
                    Vector3 forward = (new Vector3(0f, 2.5f, 0f) - cam.transform.position).normalized;
                    Quaternion baseRot = Quaternion.LookRotation(forward, Vector3.up);
                    cam.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, -3.5f);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 82f, dt * 3f);
                    break;
                }

                case CamMode.ForwardTarget:
                {
                    // Frames from ahead of the ship, looking back as the ship advances towards camera
                    float fwdAngle = playerBearing - 36f * Mathf.Deg2Rad;
                    float fwdRadius = laneRadius + 5.2f;
                    Vector3 fwdPos = new Vector3(
                        Mathf.Sin(fwdAngle) * fwdRadius,
                        playerPos.y + 0.25f,
                        Mathf.Cos(fwdAngle) * fwdRadius);

                    cam.transform.position = Vector3.Lerp(cam.transform.position, fwdPos, dt * 7f);
                    Vector3 forward = (playerPos - cam.transform.position).normalized;
                    Quaternion baseRot = Quaternion.LookRotation(forward, Vector3.up);
                    cam.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, 5.0f);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 68f, dt * 5f);
                    break;
                }
            }
        }

        public void SetMode(CamMode mode)
        {
            // Taking the camera from the game: note its preset first, while the
            // switcher still reports it as its own.
            if (!isActive && mode != CamMode.StandardGameplay)
            {
                heldPreset = CameraPresetSwitcher.CurrentIndex;
            }

            CurrentMode = mode;
            isActive = (mode != CamMode.StandardGameplay);

            string modeName = mode switch
            {
                CamMode.StandardGameplay => "STANDARD GAMEPLAY",
                CamMode.LowAngleChase => "LOW-ANGLE CHASE",
                CamMode.SideProfileOrbit => "SIDE-PROFILE ORBIT",
                CamMode.CalderaSweeper => "HIGH CALDERA SWEEPER",
                CamMode.ForwardTarget => "FORWARD TARGET CAM",
                _ => mode.ToString()
            };

            ShowBadge($"[ CAM: {modeName} ]");

            // Back to the game on the preset the player was on, not the default.
            // The camera's clamp came back with isActive.
            if (!isActive && cam != null)
            {
                CameraPresetSwitcher.Restore(heldPreset);
            }
        }

        public void ToggleHud()
        {
            hudHidden = !hudHidden;

            if (hudCanvas != null)
            {
                hudCanvas.enabled = !hudHidden;
            }
            else
            {
                GameObject hud = GameObject.Find("HUD (Holo)");
                if (hud != null)
                {
                    hud.SetActive(!hudHidden);
                }
            }

            ShowBadge(hudHidden ? "[ HUD: HIDDEN (CLEAN RECORDING) ]" : "[ HUD: VISIBLE ]");
        }

        private void ShowBadge(string text)
        {
            badgeText = text;
            badgeTimer = BadgeDuration;
        }

        private void OnGUI()
        {
            if (badgeTimer <= 0f || string.IsNullOrEmpty(badgeText) || CinematicTrailerDirector.IsRunning)
            {
                return;
            }

            InitStyles();

            float alpha = badgeTimer < 0.4f ? badgeTimer / 0.4f : 1f;
            Color prevColor = GUI.color;

            float badgeW = 340f;
            float badgeH = 34f;
            float badgeX = Screen.width - badgeW - 24f;
            float badgeY = 24f;

            if (badgeBgTexture != null)
            {
                GUI.color = new Color(0.04f, 0.08f, 0.12f, alpha * 0.85f);
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), badgeBgTexture);
            }

            Color tc = badgeStyle.normal.textColor;
            badgeStyle.normal.textColor = new Color(tc.r, tc.g, tc.b, alpha);
            GUI.Label(new Rect(badgeX, badgeY, badgeW, badgeH), badgeText, badgeStyle);

            GUI.color = prevColor;
        }

        private void InitStyles()
        {
            if (badgeBgTexture == null)
            {
                badgeBgTexture = new Texture2D(1, 1);
                badgeBgTexture.SetPixel(0, 0, Color.white);
                badgeBgTexture.Apply();
            }

            if (badgeStyle == null)
            {
                badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 12, 16),
                    fontStyle = FontStyle.Bold
                };
                badgeStyle.normal.textColor = new Color(0.35f, 0.90f, 1f, 1f);
            }
        }
    }
}
#else
namespace SurvivalChaos
{
    /// <summary>Release builds carry no trailer tooling; this answers the callers.</summary>
    public static class CinematicTrailerCam
    {
        public static bool IsActive => false;
    }
}
#endif
