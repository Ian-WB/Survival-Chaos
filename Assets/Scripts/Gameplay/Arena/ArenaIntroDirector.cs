using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalChaos
{
    /// <summary>
    /// Coordinates the in-engine cinematic intro when entering a run in Game.unity.
    ///
    /// It sweeps the camera across the volcanic caldera and storm wall with a dynamic
    /// orbital dive and Dutch banking roll, drops the ship into its orbital lane with
    /// supersonic plunge, thruster retro-burn rumble and screen shake, displays high-tech
    /// HUD calibration telemetry with reticle lock-on, boots up the holographic HUD,
    /// and hands control seamlessly to the player as the wave clock begins.
    ///
    /// Space, Enter or the dash button (A on a pad) skips the intro. Escape and
    /// Start are left to the pause menu, which holds the intro where it is.
    ///
    /// While it runs it owns the camera: the camera's ApplyBounds lets go (see
    /// <see cref="ApplyBounds.CameraHeld"/>), and the preset the camera was on is
    /// handed back at the end. It also restarts both clocks at the end - the
    /// waves' and the HUD's countdown - so the boss arrives when the bar says.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class ArenaIntroDirector : MonoBehaviour
    {
        private const float TotalDuration = 3.5f;
        private const float ShipDropStart = 0.7f;
        private const float ShipDropLand = 2.15f;
        private const float ShipSettleEnd = 2.35f;
        private const float HudFadeStart = 2.45f;
        private const float LetterboxSlideStart = 2.7f;

        private static ArenaIntroDirector instance;
        private static bool isPlaying;

        /// <summary>True while the opening cinematic sequence is running.</summary>
        public static bool IsPlaying => isPlaying;

        /// <summary>Master switch to enable or disable the opening intro.</summary>
        public static bool Enabled { get; set; } = true;

        private float timer;

        /// <summary>
        /// False from the load until the opening pose is set with the scene's
        /// Starts behind it. Until then the intro counts as playing - the HUD is
        /// hidden and the camera held - but its clock does not run.
        /// </summary>
        private bool prepared;

        /// <summary>The frame the Game scene loaded on. See <see cref="LateUpdate"/>.</summary>
        private int loadFrame;

        /// <summary>The camera preset to hand back, read as the intro takes the camera.</summary>
        private int heldPreset = CameraFraming.DefaultIndex;

        private bool thrusterFired;
        private bool systemsArmedFired;
        private bool finished;

        private Camera cam;
        private Player player;
        private WaveDirector waveDirector;
        private Timer runTimer;
        private CanvasGroup hudCanvasGroup;

        private Vector3 startCamPos;
        private Vector3 midCamPos;
        private Vector3 targetCamPos;
        private Vector3 startLookTarget;
        private Vector3 targetLookTarget;
        private float startCamFov;
        private float targetCamFov;

        private Vector3 shipRestPos;
        private const float ShipDropHeight = 12f;

        private Texture2D blackTexture;
        private GUIStyle titleStyle;
        private GUIStyle subStyle;
        private GUIStyle telemetryStyle;
        private GUIStyle reticleStyle;
        private GUIStyle hintStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null || !Application.isPlaying)
            {
                return;
            }

            GameObject host = new GameObject("Arena Intro Director");
            instance = host.AddComponent<ArenaIntroDirector>();
            DontDestroyOnLoad(host);

            // sceneLoaded has already fired for the scene the game started in, so a run
            // entered directly (the editor, or a build booted into Game) is begun by hand.
            instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance = null;
            isPlaying = false;

            // The editor keeps the domain between plays, so a switch turned off
            // in one would otherwise still be off in the next.
            Enabled = true;
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

            if (blackTexture != null)
            {
                Destroy(blackTexture);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            if (scene.name == "Game" && Enabled)
            {
                // Taken now rather than a frame later, so the first frame drawn
                // shows neither the gameplay camera nor the HUD: the HUD goes
                // here, and the camera's clamp and preset hold off on IsPlaying.
                // The pose itself is set in LateUpdate.
                cam = null;
                player = null;
                waveDirector = null;
                runTimer = null;
                HideHud();
                timer = 0f;
                finished = false;
                prepared = false;
                loadFrame = Time.frameCount;
                isPlaying = true;
            }
            else
            {
                isPlaying = false;
                finished = true;
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "Game")
            {
                StopAllCoroutines();
                isPlaying = false;
                finished = true;
                cam = null;
                player = null;
                waveDirector = null;
                runTimer = null;
                hudCanvasGroup = null;
            }
        }

        private void HideHud()
        {
            GameObject hudGo = GameObject.Find("HUD (Holo)");
            if (hudGo != null)
            {
                if (!hudGo.TryGetComponent(out hudCanvasGroup))
                {
                    hudCanvasGroup = hudGo.AddComponent<CanvasGroup>();
                }
                hudCanvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Sets the opening pose before the frame is drawn. Run in the load's own
        /// frame, which may come before the scene's Starts have placed the ship
        /// and the camera, so it puts the camera up and leaves the ship alone;
        /// and again on the next frame, with every Start behind it, for good -
        /// reading the ship's rest there and lifting it.
        /// </summary>
        private void LateUpdate()
        {
            if (!isPlaying || finished || prepared)
            {
                return;
            }

            bool final = Time.frameCount > loadFrame;
            if (!Prepare(final))
            {
                if (final)
                {
                    // Nothing to film: hand the scene straight back.
                    isPlaying = false;
                    finished = true;
                    if (hudCanvasGroup != null)
                    {
                        hudCanvasGroup.alpha = 1f;
                    }
                }
                return;
            }

            prepared = final;
        }

        private bool Prepare(bool final)
        {
            cam = Camera.main;
            player = FindAnyObjectByType<Player>();
            waveDirector = FindAnyObjectByType<WaveDirector>();
            runTimer = FindAnyObjectByType<Timer>();

            if (cam == null || player == null)
            {
                return false;
            }

            if (hudCanvasGroup == null)
            {
                HideHud();
            }

            // The first pass may come before the ship's Start; the rest read
            // there only aims the camera, and is read again on the second.
            shipRestPos = player.transform.position;

            // The station the intro lands on is the preset the camera is on, so
            // the hand-back at the end is not a jump.
            heldPreset = CameraPresetSwitcher.CurrentIndex;
            CameraPreset preset = CameraFraming.Presets[CameraFraming.RestoreIndex(heldPreset)];
            float laneRadius = ArenaGeometry.LaneRadius;
            float targetRadius = laneRadius + preset.Distance;

            ApplyBounds bounds = player.GetComponent<ApplyBounds>();
            if (bounds == null)
            {
                bounds = cam.GetComponent<ApplyBounds>();
            }
            float floor = 3.42f, ceiling = 12.65f;
            if (bounds != null && bounds.TryGetBand(out float bFloor, out float bCeil))
            {
                floor = bFloor;
                ceiling = bCeil;
            }
            float bandMiddleY = SpawnBand.Middle(floor, ceiling);
            float bandHeight = ceiling - floor;
            float stationY = CameraFraming.StationHeight(preset, shipRestPos.y, floor, ceiling);

            float bearing = Mathf.Atan2(player.transform.position.x, player.transform.position.z);
            targetCamPos = new Vector3(Mathf.Sin(bearing) * targetRadius, stationY, Mathf.Cos(bearing) * targetRadius);
            targetLookTarget = new Vector3(0f, stationY, 0f);
            targetCamFov = CameraFraming.Lens(preset, preset.Distance, bandHeight, 0f);

            // Starting establishing camera shot: high over caldera rim, wider FOV, looking into crater
            float startAngle = bearing + 35f * Mathf.Deg2Rad;
            float startRadius = targetRadius + 11f;
            float startHeight = bandMiddleY + 15f;

            startCamPos = new Vector3(Mathf.Sin(startAngle) * startRadius, startHeight, Mathf.Cos(startAngle) * startRadius);
            startLookTarget = new Vector3(0f, 2f, 0f);
            startCamFov = 86f;

            // Midpoint waypoint: swooping dive near ship trajectory
            float midAngle = bearing + 15f * Mathf.Deg2Rad;
            float midRadius = targetRadius + 4f;
            float midHeight = bandMiddleY + 4f;
            midCamPos = new Vector3(Mathf.Sin(midAngle) * midRadius, midHeight, Mathf.Cos(midAngle) * midRadius);

            // Set initial camera pose and elevate ship into high orbit
            cam.transform.position = startCamPos;
            cam.transform.LookAt(startLookTarget);
            cam.fieldOfView = startCamFov;

            if (final)
            {
                player.transform.position = shipRestPos + Vector3.up * ShipDropHeight;
            }

            timer = 0f;
            thrusterFired = false;
            systemsArmedFired = false;
            return true;
        }

        private void Update()
        {
            if (!isPlaying || finished || !prepared)
            {
                return;
            }

            if (CinematicTrailerDirector.IsRunning)
            {
                FinishIntro();
                return;
            }

            if (PauseMenu.GameIsPaused || Time.timeScale <= 0f)
            {
                return;
            }

            // Check skip keys (Space, Enter, Gamepad A)
            bool skipRequested = GameInput.DashPressed;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                skipRequested = true;
            }
#endif
            if (skipRequested)
            {
                FinishIntro();
                return;
            }

            timer += Time.deltaTime;

            if (timer >= TotalDuration)
            {
                FinishIntro();
                return;
            }

            // 1. Camera Trajectory along multi-stage path with dynamic banking Dutch tilt
            float camProgress = Mathf.Clamp01(timer / TotalDuration);
            float camEase = Mathf.SmoothStep(0f, 1f, camProgress);

            if (cam != null)
            {
                // Quadratic bezier curve through midCamPos for natural swooping dive
                Vector3 p0 = startCamPos;
                Vector3 p1 = midCamPos;
                Vector3 p2 = targetCamPos;
                Vector3 camPos = (1f - camEase) * (1f - camEase) * p0 + 2f * (1f - camEase) * camEase * p1 + camEase * camEase * p2;

                // Subtle retro-burn landing screen shake
                if (timer >= ShipDropLand && timer < ShipDropLand + 0.35f)
                {
                    float shakeIntensity = (1f - ((timer - ShipDropLand) / 0.35f)) * 0.18f;
                    camPos += new Vector3(
                        (Mathf.PerlinNoise(timer * 40f, 0f) - 0.5f) * shakeIntensity,
                        (Mathf.PerlinNoise(0f, timer * 40f) - 0.5f) * shakeIntensity,
                        (Mathf.PerlinNoise(timer * 35f, timer * 35f) - 0.5f) * shakeIntensity
                    );
                }

                cam.transform.position = camPos;

                Vector3 currentLook = Vector3.Lerp(startLookTarget, targetLookTarget, camEase);
                Vector3 forward = (currentLook - camPos).normalized;

                // Dynamic Dutch banking roll (-8° at entry dive smoothly leveling out to 0°)
                float rollAngle = Mathf.Sin(camProgress * Mathf.PI) * -8.5f * (1f - camProgress * 0.7f);
                Quaternion baseRot = Quaternion.LookRotation(forward, Vector3.up);
                cam.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, rollAngle);

                cam.fieldOfView = Mathf.Lerp(startCamFov, targetCamFov, camEase);
            }

            // 2. Supersonic Ship Entry & Retro-Burn Cushion
            if (player != null)
            {
                if (timer < ShipDropStart)
                {
                    player.transform.position = shipRestPos + Vector3.up * ShipDropHeight;
                }
                else if (timer < ShipDropLand)
                {
                    // Snappy cubic deceleration curve
                    float t = (timer - ShipDropStart) / (ShipDropLand - ShipDropStart);
                    float dropT = 1f - Mathf.Pow(1f - t, 3f);
                    player.transform.position = Vector3.Lerp(shipRestPos + Vector3.up * ShipDropHeight, shipRestPos, dropT);
                }
                else if (timer < ShipSettleEnd)
                {
                    // Thruster cushion bounce: dips slightly below and settles smoothly into lane
                    float bounceT = (timer - ShipDropLand) / (ShipSettleEnd - ShipDropLand);
                    float dip = Mathf.Sin(bounceT * Mathf.PI) * -0.16f;
                    player.transform.position = shipRestPos + Vector3.up * dip;

                    if (!thrusterFired)
                    {
                        thrusterFired = true;
                        TriggerThrusters();
                    }
                }
                else
                {
                    player.transform.position = shipRestPos;

                    if (!systemsArmedFired)
                    {
                        systemsArmedFired = true;
                        TriggerSystemsArmed();
                    }
                }
            }

            // 3. HUD fade-in
            if (hudCanvasGroup != null)
            {
                if (timer < HudFadeStart)
                {
                    hudCanvasGroup.alpha = 0f;
                }
                else
                {
                    float hudT = Mathf.Clamp01((timer - HudFadeStart) / (TotalDuration - HudFadeStart));
                    hudCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, hudT);
                }
            }
        }

        private void TriggerThrusters()
        {
            if (GameSounds.Instance != null && GameSounds.Instance.PlayerDash != null)
            {
                GameSounds.Play(GameSounds.Instance.PlayerDash);
            }

            Rumble.Pulse(Rumble.Strength.Block);
        }

        private void TriggerSystemsArmed()
        {
            if (GameSounds.Instance != null && GameSounds.Instance.SkillPicked != null)
            {
                GameSounds.Play(GameSounds.Instance.SkillPicked);
            }

            Rumble.Pulse(Rumble.Strength.Light);
        }

        private void FinishIntro()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            isPlaying = false;

            // Ensure ship is at its resting lane position
            if (player != null)
            {
                player.transform.position = shipRestPos;
            }

            // Ensure camera is at exact gameplay framing
            if (cam != null)
            {
                cam.transform.position = targetCamPos;
                cam.transform.LookAt(targetLookTarget);
                cam.fieldOfView = targetCamFov;

                CameraPresetSwitcher.Restore(heldPreset);
            }

            // Restore HUD
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = 1f;
            }

            // Both clocks start from here, together. The waves' alone used to,
            // and the HUD's countdown - which reveals the boss - had run on
            // through the intro, so the bar reached the boss 3.5 s before the
            // wave sent it. The volcano and the storm wall read the waves' clock,
            // so they keep step with both.
            if (waveDirector != null)
            {
                waveDirector.ResetStartTime();
            }

            if (runTimer != null)
            {
                runTimer.RestartClock();
            }
        }

        private void OnGUI()
        {
            if (!isPlaying || finished)
            {
                return;
            }

            InitStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. Cinematic letterbox bars
            float maxBarH = screenH * 0.10f;
            float barH = maxBarH;

            if (timer > LetterboxSlideStart)
            {
                float slideOutT = Mathf.Clamp01((timer - LetterboxSlideStart) / (TotalDuration - LetterboxSlideStart));
                barH = Mathf.Lerp(maxBarH, 0f, slideOutT * slideOutT);
            }

            if (barH > 0.5f && blackTexture != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, screenW, barH), blackTexture);
                GUI.DrawTexture(new Rect(0f, screenH - barH, screenW, barH), blackTexture);
            }

            // 2. High-Tech Corner HUD Viewport Brackets
            float cornerAlpha = Mathf.Clamp01(timer / 0.5f) * (1f - Mathf.Clamp01((timer - LetterboxSlideStart) / 0.6f));
            if (cornerAlpha > 0.01f)
            {
                Color prevTc = telemetryStyle.normal.textColor;
                telemetryStyle.normal.textColor = new Color(prevTc.r, prevTc.g, prevTc.b, cornerAlpha * 0.75f);

                float marginX = 28f;
                float topY = barH + 12f;
                float botY = screenH - barH - 30f;

                GUI.Label(new Rect(marginX, topY, 360f, 22f), "┌─ [ ORBITAL INGRESS // VECTOR: 342.8° ]", telemetryStyle);
                GUI.Label(new Rect(screenW - marginX - 360f, topY, 360f, 22f), "[ SECTOR 01: CALDERA EYE ] ─┐", telemetryStyle);

                string engineStatus = timer < ShipDropLand ? "IGNITION PENDING" : "RETRO-BURN LOCKED [OK]";
                GUI.Label(new Rect(marginX, botY, 360f, 22f), $"└─ [ THRUSTERS: {engineStatus} ]", telemetryStyle);
                GUI.Label(new Rect(screenW - marginX - 360f, botY, 360f, 22f), "[ SYSTEMS: ONLINE // WEAPONS: ARMED ] ─┘", telemetryStyle);
            }

            // 3. Dynamic Screen-Space Ship Reticle Bracket
            if (cam != null && player != null && timer >= 1.2f && timer < 2.9f)
            {
                Vector3 shipScreen = cam.WorldToScreenPoint(player.transform.position);
                if (shipScreen.z > 0f)
                {
                    float reticleAlpha = timer < 1.7f ? (timer - 1.2f) / 0.5f : (2.9f - timer) / 0.5f;
                    reticleAlpha = Mathf.Clamp01(reticleAlpha);

                    Color rc = reticleStyle.normal.textColor;
                    reticleStyle.normal.textColor = new Color(rc.r, rc.g, rc.b, reticleAlpha * 0.9f);

                    float rx = shipScreen.x;
                    float ry = Screen.height - shipScreen.y;
                    float rw = 160f;
                    float rh = 28f;

                    string lockLabel = timer < ShipDropLand ? "◈ ACQUIRING ◈" : "◈ ORBIT LOCKED ◈";
                    GUI.Label(new Rect(rx - rw * 0.5f, ry - rh * 0.5f, rw, rh), $"[ {lockLabel} ]", reticleStyle);
                }
            }

            // 4. Holographic Text Banners
            float centerY = screenH * 0.75f;
            Rect titleRect = new Rect(0f, centerY, screenW, 38f);
            Rect subRect = new Rect(0f, centerY + 34f, screenW, 24f);

            if (timer >= 0.2f && timer < 1.9f)
            {
                float alpha = timer < 0.6f ? (timer - 0.2f) / 0.4f : (1.9f - timer) / 0.4f;
                alpha = Mathf.Clamp01(alpha);

                Color c = titleStyle.normal.textColor;
                titleStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                Color sc = subStyle.normal.textColor;
                subStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha * 0.85f);

                GUI.Label(titleRect, "// SECTOR 01: CALDERA EYE //", titleStyle);
                GUI.Label(subRect, "VOLCANIC CORE DETECTED • ORBITAL COMBAT ZONE", subStyle);
            }
            else if (timer >= 2.05f && timer < 3.4f)
            {
                float alpha = timer < 2.45f ? (timer - 2.05f) / 0.4f : (3.4f - timer) / 0.4f;
                alpha = Mathf.Clamp01(alpha);

                Color c = titleStyle.normal.textColor;
                titleStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                Color sc = subStyle.normal.textColor;
                subStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha * 0.85f);

                GUI.Label(titleRect, "// ALL SYSTEMS ONLINE //", titleStyle);
                GUI.Label(subRect, "ENGAGE COMBAT ORBIT • DEFENSES ARMED", subStyle);
            }

            // 5. Skip prompt
            if (timer >= 0.5f)
            {
                float skipAlpha = Mathf.Clamp01((timer - 0.5f) / 0.5f);
                Color hc = hintStyle.normal.textColor;
                hintStyle.normal.textColor = new Color(hc.r, hc.g, hc.b, skipAlpha * 0.7f);

                Rect hintRect = new Rect(screenW - 240f, screenH - barH - 32f, 220f, 24f);
                GUI.Label(hintRect, "[SPACE / A] SKIP INTRO", hintStyle);
            }
        }

        private void InitStyles()
        {
            if (blackTexture == null)
            {
                blackTexture = new Texture2D(1, 1);
                blackTexture.SetPixel(0, 0, Color.black);
                blackTexture.Apply();
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.034f), 18, 34),
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = new Color(0.35f, 0.88f, 1f, 1f);
            }

            if (subStyle == null)
            {
                subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.018f), 12, 19),
                    fontStyle = FontStyle.Normal
                };
                subStyle.normal.textColor = new Color(0.72f, 0.92f, 1f, 0.85f);
            }

            if (telemetryStyle == null)
            {
                telemetryStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.015f), 11, 15),
                    fontStyle = FontStyle.Bold
                };
                telemetryStyle.normal.textColor = new Color(0.28f, 0.82f, 1f, 0.75f);
            }

            if (reticleStyle == null)
            {
                reticleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 11, 16),
                    fontStyle = FontStyle.Bold
                };
                reticleStyle.normal.textColor = new Color(0.3f, 0.95f, 1f, 0.9f);
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 11, 16),
                    fontStyle = FontStyle.Normal
                };
                hintStyle.normal.textColor = new Color(0.6f, 0.75f, 0.85f, 0.6f);
            }
        }
    }
}
