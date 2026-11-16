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
    /// It opens close behind the ship as it hangs above its lane with the moon behind
    /// it, follows the ship down as it drops into the lane, and pulls back to the
    /// gameplay camera as it lands, with a retro-burn rumble and shake. On the way it
    /// shows two short lines in the HUD's font, brings the HUD up, and hands control
    /// to the player as the wave clock begins.
    ///
    /// The opening used to look down on the island from high over the rim while the
    /// ship fell somewhere in front of it, a few pixels against the lava, with the
    /// moon out of frame (16 November 2026). The moon stands
    /// <see cref="OpeningTilt"/> degrees up, straight across the island from where
    /// the ship starts, so looking inward and that far up from behind the ship frames
    /// both; ArenaIntroDirectorTests fails if the scene's moonlight is turned away.
    ///
    /// It plays on the first run of a session only: Try Again, Restart and a second
    /// Play go straight to the run (12 November 2026).
    ///
    /// Space, Enter or the dash button (A on a pad) skips the intro. Escape and
    /// Start are left to the pause menu, which holds the intro where it is.
    ///
    /// While it runs it owns the camera: the camera's ApplyBounds lets go (see
    /// <see cref="ApplyBounds.CameraHeld"/>), and the preset the camera was on is
    /// handed back at the end. The ship's ApplyBounds lets go too, so the drop
    /// starts above the band. It also restarts the clocks at the end - the waves',
    /// the HUD's countdown and the run's own - so the boss arrives when the bar
    /// says and a watched intro costs the run no time.
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

        /// <summary>Whether the intro has played since the game started. It plays once.</summary>
        private static bool playedThisSession;

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
        private Vector3 targetCamPos;
        private Vector3 targetLookTarget;
        private float targetCamFov;

        /// <summary>The opening shot's line of sight, and where the ship hangs before its drop.</summary>
        private Vector3 openingView;
        private Vector3 hangPos;

        private Vector3 shipRestPos;
        private const float ShipDropHeight = 12f;

        /// <summary>
        /// Degrees above level the opening shot looks: the moon's height in the
        /// sky, read from the Game scene's Directional Light on 16 November 2026.
        /// </summary>
        public const float OpeningTilt = 15f;

        /// <summary>How far behind the hanging ship the camera starts, along the line it looks down.</summary>
        public const float OpeningBack = 6f;

        /// <summary>
        /// How far below that line the camera starts, so the ship sits a little
        /// above the middle of the moon and well clear of the line of text low
        /// in the frame.
        /// </summary>
        public const float OpeningBelow = 0.5f;

        /// <summary>
        /// The opening lens, tighter than the gameplay camera's so the ship and
        /// the moon are large. It opens out to the gameplay lens on the way back.
        /// </summary>
        public const float OpeningFov = 50f;

        /// <summary>
        /// How much of the ship's fall the camera rides down with it. A little
        /// short of all of it, so the ship sinks in the frame as it falls; it
        /// was left above once, and the ship fell away to a speck.
        /// </summary>
        public const float FollowShare = 0.9f;

        /// <summary>When the camera starts over from riding with the ship to the gameplay station.</summary>
        private const float CameraMoveStart = 1.2f;

        /// <summary>When the look starts over from the opening's line to the station's.</summary>
        private const float LookHandBackStart = 2.0f;

        /// <summary>How far down the line the opening shot looks, to place its aim.</summary>
        private const float OpeningAim = 10f;

        private Texture2D blackTexture;
        private GUIStyle titleStyle;
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
            playedThisSession = false;

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
            if (ShouldPlay(scene.name, Enabled, playedThisSession))
            {
                playedThisSession = true;

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

        /// <summary>
        /// Whether a scene's load starts the intro: the Game scene, with the
        /// switch on, and only the first time in a session.
        /// </summary>
        public static bool ShouldPlay(string scene, bool enabled, bool alreadyPlayed)
        {
            return scene == "Game" && enabled && !alreadyPlayed;
        }

        /// <summary>
        /// Whether the intro's bars and lines are drawn this frame. Not under
        /// the pause menu or the loading screen: they are drawn over every
        /// canvas, and paused mid-intro the banner sat across the pause card.
        /// </summary>
        public static bool OverlayShown(bool playing, bool finished, bool paused, bool loading)
        {
            return playing && !finished && !paused && !loading;
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
            float bandHeight = ceiling - floor;
            float stationY = CameraFraming.StationHeight(preset, shipRestPos.y, floor, ceiling);

            float bearing = Mathf.Atan2(player.transform.position.x, player.transform.position.z);
            targetCamPos = new Vector3(Mathf.Sin(bearing) * targetRadius, stationY, Mathf.Cos(bearing) * targetRadius);
            targetLookTarget = new Vector3(0f, stationY, 0f);
            targetCamFov = CameraFraming.Lens(preset, preset.Distance, bandHeight, 0f);

            // The opening: close behind the ship where it hangs, looking inward
            // and up at the moon, the ship a little above the moon's middle.
            hangPos = shipRestPos + Vector3.up * ShipDropHeight;
            openingView = OpeningView(shipRestPos);
            startCamPos = OpeningCamera(hangPos, openingView);

            cam.transform.position = startCamPos;
            cam.transform.rotation = Quaternion.LookRotation(openingView, Vector3.up);
            cam.fieldOfView = OpeningFov;

            if (final)
            {
                player.transform.position = shipRestPos + Vector3.up * ShipDropHeight;
            }

            timer = 0f;
            thrusterFired = false;
            systemsArmedFired = false;
            return true;
        }

        /// <summary>
        /// The opening shot's line of sight for a ship resting at
        /// <paramref name="shipRest"/>: inward, towards the island's middle
        /// and the moon beyond it, tilted <see cref="OpeningTilt"/> up.
        /// </summary>
        public static Vector3 OpeningView(Vector3 shipRest)
        {
            Vector3 inward = new Vector3(-shipRest.x, 0f, -shipRest.z);
            inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.forward;

            float tilt = OpeningTilt * Mathf.Deg2Rad;
            return (inward * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt)).normalized;
        }

        /// <summary>
        /// Where the camera opens: <see cref="OpeningBack"/> behind the hanging
        /// ship along <paramref name="view"/>, and <see cref="OpeningBelow"/> under it.
        /// </summary>
        public static Vector3 OpeningCamera(Vector3 hangPos, Vector3 view)
        {
            return hangPos - view * OpeningBack - Vector3.up * OpeningBelow;
        }

        /// <summary>
        /// How much of its drop the ship has made, <paramref name="t"/> of the
        /// way through it: from a hover it falls faster and faster, then brakes
        /// on its thrusters into the lane. It used to start at full speed,
        /// which was a plunge from out of frame; seen hanging first, with the
        /// camera following, that start whipped the camera down (16 November 2026).
        /// </summary>
        public static float DropShare(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>A smoothstep of <paramref name="t"/> from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static float Ease(float from, float to, float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));
        }

        /// <summary>
        /// How far the camera has gone over from riding down with the ship to
        /// the gameplay station, <paramref name="t"/> seconds in. Riding with
        /// the ship brings it most of the way, so this is the last of it.
        /// </summary>
        public static float CameraTravel(float t)
        {
            return Ease(CameraMoveStart, TotalDuration, t);
        }

        /// <summary>
        /// How far the look has gone over from the opening's line, up at the
        /// moon, to the station's own, <paramref name="t"/> seconds in.
        /// </summary>
        public static float LookHandBack(float t)
        {
            return Ease(LookHandBackStart, TotalDuration, t);
        }

        /// <summary>How far the lens has opened from the opening's to the gameplay camera's.</summary>
        public static float LensOpen(float t)
        {
            return Ease(ShipDropStart, TotalDuration, t);
        }

        /// <summary>
        /// Where the camera rides as the ship falls from <paramref name="hang"/>
        /// to <paramref name="ship"/>: down with it by <see cref="FollowShare"/>
        /// of the fall, from where it opened.
        /// </summary>
        public static Vector3 RideDown(Vector3 openingCamera, Vector3 hang, Vector3 ship)
        {
            return openingCamera + Vector3.up * ((ship.y - hang.y) * FollowShare);
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

            // 1. The ship: hangs, drops into its lane, and settles on its thrusters.
            if (player != null)
            {
                if (timer < ShipDropStart)
                {
                    player.transform.position = shipRestPos + Vector3.up * ShipDropHeight;
                }
                else if (timer < ShipDropLand)
                {
                    float t = (timer - ShipDropStart) / (ShipDropLand - ShipDropStart);
                    player.transform.position = Vector3.Lerp(shipRestPos + Vector3.up * ShipDropHeight, shipRestPos, DropShare(t));
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

            // 2. The camera, after the ship so it frames where the ship is this
            // frame: it rides down with the ship, still looking up the opening's
            // line, so the island rises in front of the moon; then it settles on
            // the gameplay station and takes the station's look and lens.
            if (cam != null && player != null)
            {
                Vector3 ride = RideDown(startCamPos, hangPos, player.transform.position);
                Vector3 camPos = Vector3.Lerp(ride, targetCamPos, CameraTravel(timer));

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

                Vector3 openingLook = ride + openingView * OpeningAim;
                Vector3 look = Vector3.Lerp(openingLook, targetLookTarget, LookHandBack(timer));
                Vector3 forward = (look - camPos).normalized;

                // A light bank into the dive, level again by the hand-back.
                float progress = Mathf.Clamp01(timer / TotalDuration);
                float rollAngle = Mathf.Sin(progress * Mathf.PI) * -5f * (1f - progress * 0.7f);
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, rollAngle);

                cam.fieldOfView = Mathf.Lerp(OpeningFov, targetCamFov, LensOpen(timer));
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

            RunStats.RestartClock();
        }

        private void OnGUI()
        {
            if (!OverlayShown(isPlaying, finished, PauseMenu.GameIsPaused, LoadingScreen.Busy))
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

            // 2. One line at a time, low in the frame. The corner read-outs
            // and the bracket that followed the ship went on 12 November
            // 2026: at 1440p the bracket's label wrapped and sat on the ship,
            // and the corner lines ran into the HUD and the skip prompt.
            float titleH = titleStyle.fontSize * 1.5f;
            Rect titleRect = new Rect(0f, screenH * 0.75f, screenW, titleH);

            if (timer >= 0.2f && timer < 1.9f)
            {
                float alpha = timer < 0.6f ? (timer - 0.2f) / 0.4f : (1.9f - timer) / 0.4f;
                DrawLine(titleRect, "ENTERING ORBIT", titleStyle, TitleColour, Mathf.Clamp01(alpha));
            }
            else if (timer >= 2.05f && timer < 3.4f)
            {
                float alpha = timer < 2.45f ? (timer - 2.05f) / 0.4f : (3.4f - timer) / 0.4f;
                DrawLine(titleRect, "WEAPONS ARMED", titleStyle, TitleColour, Mathf.Clamp01(alpha));
            }

            // 3. Skip prompt, inside the bottom bar and gone with it.
            if (timer >= 0.5f && barH > hintStyle.fontSize * 1.6f)
            {
                float skipAlpha = Mathf.Clamp01((timer - 0.5f) / 0.5f) * 0.7f;
                float hintH = hintStyle.fontSize * 1.6f;
                float margin = screenH * 0.03f;
                Rect hintRect = new Rect(0f, screenH - (barH + hintH) * 0.5f, screenW - margin, hintH);
                DrawLine(hintRect, "SPACE / A   SKIP", hintStyle, HintColour, skipAlpha);
            }
        }

        private static readonly Color TitleColour = new Color(0.35f, 0.88f, 1f, 1f);
        private static readonly Color HintColour = new Color(0.6f, 0.75f, 0.85f, 1f);

        private static void DrawLine(Rect rect, string text, GUIStyle style, Color colour, float alpha)
        {
            style.normal.textColor = new Color(colour.r, colour.g, colour.b, colour.a * alpha);
            GUI.Label(rect, text, style);
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
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            }

            // Every frame: the resolution can change in the options.
            OverlayFont.Fit(titleStyle, 0.034f, 18, 48);
            OverlayFont.Fit(hintStyle, 0.016f, 11, 22);
        }
    }
}
