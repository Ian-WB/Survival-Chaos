#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
// Trailer tooling: compiled only where the debug menu is (the editor and Checked builds), so no
// release build carries its hotkeys. The stub at the bottom keeps the callers compiling.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalChaos
{
    /// <summary>
    /// Automated in-engine cinematic trailer director.
    ///
    /// Choreographs a thrilling 60-second broadcast-ready cinematic gameplay trailer in Game.unity:
    /// - 00:00 - 00:07: High Caldera Rim Panoramic Sweeper ("AN ORBITAL ARENA / NO ESCAPE FROM THE RING")
    /// - 00:07 - 00:16: Low-Angle Ingress & Wave Influx ("RELENTLESS WAVES / SURVIVE THE CURVE")
    /// - 00:16 - 00:26: Side-Profile Orbit & Slow-Mo Deflection ("DEFLECT. DASH. CONTROL TIME.")
    /// - 00:26 - 00:42: The Leviathan Boss Arrival & Horn ("THE LEVIATHAN AWAKENS / ORBITAL BOSS CLASH")
    /// - 00:42 - 00:52: Climax Dogfight Montage ("FIGHT TO THE CORE / UPGRADE YOUR SHIP AND BREAK THE RUN")
    /// - 00:52 - 01:00: Deep Orbit Pull-out & "SURVIVAL CHAOS / PLAY IT ON ITCH.IO" reveal.
    ///
    /// Can be launched with F11 in Game.unity or from the Unity menu: Survival Chaos > Play Cinematic Trailer (60s).
    /// Pressing Escape at any time ends the trailer.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class CinematicTrailerDirector : MonoBehaviour
    {
        private static CinematicTrailerDirector instance;
        private static bool isRunning;

        /// <summary>True while the automated 60s trailer is playing.</summary>
        public static bool IsRunning => isRunning;

        private float trailerTimer;
        private Camera cam;
        private Player player;
        private WaveDirector waveDirector;
        private Canvas hudCanvas;

        private bool waveAdvanced;
        private bool slowMoStarted;
        private bool slowMoEnded;
        private bool bossSpawned;
        private bool bossHornPlayed;
        private float shakeIntensity;

        /// <summary>The camera preset to hand back, read as the trailer takes the camera.</summary>
        private int heldPreset = CameraFraming.DefaultIndex;

        private bool originalInvulnerable;
        private float originalSpeed = 1f;

        private Texture2D blackTexture;
        private Texture2D whiteTexture;
        private GUIStyle recStyle;
        private GUIStyle titleCardStyle;
        private GUIStyle titleSubStyle;
        private GUIStyle logoTitleStyle;
        private GUIStyle logoSubStyle;
        private GUIStyle logoWishlistStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null || !Application.isPlaying)
            {
                return;
            }

            GameObject host = new GameObject("Cinematic Trailer Director");
            instance = host.AddComponent<CinematicTrailerDirector>();
            DontDestroyOnLoad(host);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance = null;
            isRunning = false;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Survival Chaos/Play Cinematic Trailer (60s)", priority = 210)]
        public static void MenuStartTrailer()
        {
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                Debug.LogWarning("Start Play Mode in Game.unity before playing the cinematic trailer.");
                return;
            }

            StartTrailer();
        }
#endif

        /// <summary>Starts the automated 60-second trailer.</summary>
        public static void StartTrailer()
        {
            if (instance == null)
            {
                Create();
            }

            if (instance != null)
            {
                instance.BeginSequence();
            }
        }

        /// <summary>Stops the trailer and restores normal gameplay.</summary>
        public static void StopTrailer()
        {
            if (instance != null)
            {
                instance.EndSequence();
            }
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
            if (whiteTexture != null)
            {
                Destroy(whiteTexture);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Game")
            {
                isRunning = false;
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "Game")
            {
                if (isRunning)
                {
                    EndSequence();
                }
                cam = null;
                player = null;
                waveDirector = null;
                hudCanvas = null;
            }
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "Game")
            {
                return;
            }

            // F11 toggles trailer mode
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f11Key.wasPressedThisFrame)
            {
                if (isRunning)
                {
                    EndSequence();
                }
                else
                {
                    BeginSequence();
                }
                return;
            }

            // Escape cancels trailer
            if (isRunning && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                EndSequence();
                return;
            }
#endif

            if (!isRunning)
            {
                return;
            }

            float dt = CaptureClock.UnscaledDelta;
            trailerTimer += dt;

            if (cam == null)
            {
                cam = Camera.main;
            }
            if (player == null)
            {
                player = FindAnyObjectByType<Player>();
            }
            if (waveDirector == null)
            {
                waveDirector = FindAnyObjectByType<WaveDirector>();
            }

            // Decay camera shake
            if (shakeIntensity > 0f)
            {
                shakeIntensity = Mathf.Max(0f, shakeIntensity - dt * 1.5f);
            }

            // Orchestrate shots, camera banking, and game events across the 60-second timeline
            UpdateTrailerTimeline(trailerTimer, dt);

            if (trailerTimer >= 60f)
            {
                EndSequence();
            }
        }

        private void BeginSequence()
        {
            cam = Camera.main;
            player = FindAnyObjectByType<Player>();
            waveDirector = FindAnyObjectByType<WaveDirector>();

            // Read before isRunning goes up, while the switcher still reports the
            // camera as its own. A trailer started over a free camera hands back
            // the preset under it all the same: neither changes the switcher.
            if (!isRunning)
            {
                heldPreset = CameraPresetSwitcher.CurrentIndex;
            }

            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                hudCanvas = canvasGo.GetComponent<Canvas>();
                hudCanvas.enabled = false;
            }

            // An invulnerable ship on a wound-on clock is not a run to set a record in.
            RunRecords.MarkAssisted();

            if (player != null)
            {
                originalInvulnerable = player.Invulnerable;
                player.Invulnerable = true;
            }

            originalSpeed = RunTime.RequestedSpeed;
            bossSpawned = false;
            bossHornPlayed = false;
            waveAdvanced = false;
            slowMoStarted = false;
            slowMoEnded = false;
            shakeIntensity = 0f;
            trailerTimer = 0f;
            isRunning = true;
        }

        private void EndSequence()
        {
            isRunning = false;

            if (hudCanvas != null)
            {
                hudCanvas.enabled = true;
            }

            if (player != null)
            {
                player.Invulnerable = originalInvulnerable;
            }

            RunTime.SetSpeed(originalSpeed);

            // The preset the player was on, not the default. The camera's clamp
            // came back with isRunning, so the height is the band's again.
            if (cam != null)
            {
                CameraPresetSwitcher.Restore(heldPreset);
            }
        }

        private void UpdateTrailerTimeline(float t, float dt)
        {
            if (player == null || cam == null)
            {
                return;
            }

            Vector3 playerPos = player.transform.position;
            float laneRadius = ArenaGeometry.LaneRadius;
            float bearing = Mathf.Atan2(playerPos.x, playerPos.z);

            // Keep ship anchored in the hero sector where the arena lighting and volcano visuals are brightest

            // Shot 1 (0s - 7s): High Caldera Panoramic Sweeper
            if (t < 7f)
            {
                float sweepAngle = bearing + (t * 0.12f) + 32f * Mathf.Deg2Rad;
                Vector3 sweepPos = new Vector3(
                    Mathf.Sin(sweepAngle) * (laneRadius + 18f),
                    playerPos.y + 16f,
                    Mathf.Cos(sweepAngle) * (laneRadius + 18f));

                cam.transform.position = Vector3.Lerp(cam.transform.position, sweepPos, dt * 4f);
                Vector3 forward = (new Vector3(0f, 2f, 0f) - cam.transform.position).normalized;
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -4f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 82f, dt * 3f);
            }
            // Shot 2 (7s - 16s): Low-Angle Thruster Chase (Combat Ingress & Enemy Wave)
            else if (t < 16f)
            {
                // Advance wave clock to bring enemies onto the ring
                if (!waveAdvanced && waveDirector != null)
                {
                    waveAdvanced = true;
                    waveDirector.AdvanceBy(25f);
                }

                float chaseRadius = laneRadius + 3.8f;
                float chaseHeight = playerPos.y - 0.35f;
                Vector3 chasePos = new Vector3(
                    Mathf.Sin(bearing) * chaseRadius,
                    chaseHeight,
                    Mathf.Cos(bearing) * chaseRadius);

                cam.transform.position = Vector3.Lerp(cam.transform.position, chasePos, dt * 12f);
                Vector3 lookTarget = playerPos + player.transform.forward * 4.5f + Vector3.up * 0.2f;
                Vector3 forward = (lookTarget - cam.transform.position).normalized;

                // Dynamic banking roll: -8.5° into the turn
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -8.5f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 65f, dt * 5f);
            }
            // Shot 3 (16s - 26s): Side-Profile Orbit & Super Slow-Mo Bullet Time
            else if (t < 26f)
            {
                // Slow motion trigger between 20.5s and 24.5s
                if (t >= 20.5f && t < 24.5f)
                {
                    if (!slowMoStarted)
                    {
                        slowMoStarted = true;
                        RunTime.SetSpeed(0.25f);
                        if (GameSounds.Instance != null && GameSounds.Instance.SlowMoStart != null)
                        {
                            GameSounds.Play(GameSounds.Instance.SlowMoStart);
                        }
                        if (GameSounds.Instance != null && GameSounds.Instance.DeflectorBlock != null)
                        {
                            GameSounds.Play(GameSounds.Instance.DeflectorBlock);
                        }
                    }
                }
                else
                {
                    if (slowMoStarted && !slowMoEnded)
                    {
                        slowMoEnded = true;
                        RunTime.SetSpeed(1.0f);
                        if (GameSounds.Instance != null && GameSounds.Instance.SlowMoEnd != null)
                        {
                            GameSounds.Play(GameSounds.Instance.SlowMoEnd);
                        }
                    }
                }

                float sideAngle = bearing + 42f * Mathf.Deg2Rad;
                Vector3 sidePos = new Vector3(
                    Mathf.Sin(sideAngle) * (laneRadius + 5.2f),
                    playerPos.y + 0.45f,
                    Mathf.Cos(sideAngle) * (laneRadius + 5.2f));

                cam.transform.position = Vector3.Lerp(cam.transform.position, sidePos, dt * 8f);
                Vector3 forward = (playerPos - cam.transform.position).normalized;
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, 5.5f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 58f, dt * 5f);
            }
            // Shot 4 (26s - 42s): The Leviathan Boss Encounter
            else if (t < 42f)
            {
                RunTime.SetSpeed(1.0f);

                if (!bossSpawned && waveDirector != null)
                {
                    bossSpawned = true;
                    waveDirector.SpawnBossNow();
                }

                if (!bossHornPlayed && t >= 27.0f)
                {
                    bossHornPlayed = true;
                    if (GameSounds.Instance != null && GameSounds.Instance.BossHornArrival != null)
                    {
                        GameSounds.Play(GameSounds.Instance.BossHornArrival);
                    }
                    shakeIntensity = 0.35f;
                    Rumble.Pulse(Rumble.Strength.Heavy);
                }

                // Look head-on forward at incoming boss and torpedo barrages
                float fwdAngle = bearing - 38f * Mathf.Deg2Rad;
                Vector3 fwdPos = new Vector3(
                    Mathf.Sin(fwdAngle) * (laneRadius + 5.8f),
                    playerPos.y + 0.35f,
                    Mathf.Cos(fwdAngle) * (laneRadius + 5.8f));

                cam.transform.position = Vector3.Lerp(cam.transform.position, fwdPos, dt * 8f);
                Vector3 forward = (playerPos - cam.transform.position).normalized;
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -6.0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 68f, dt * 4f);
            }
            // Shot 5 (42s - 52s): Climax Dogfight Montage
            else if (t < 52f)
            {
                float chaseRadius = laneRadius + 4.0f;
                Vector3 chasePos = new Vector3(
                    Mathf.Sin(bearing) * chaseRadius,
                    playerPos.y - 0.2f,
                    Mathf.Cos(bearing) * chaseRadius);

                cam.transform.position = Vector3.Lerp(cam.transform.position, chasePos, dt * 10f);
                Vector3 lookTarget = playerPos + player.transform.forward * 5f;
                Vector3 forward = (lookTarget - cam.transform.position).normalized;
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -7.5f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 62f, dt * 5f);
            }
            // Shot 6 (52s - 60s): Pull out into deep orbit & Fade to Logo
            else
            {
                float pullRadius = laneRadius + 24f;
                Vector3 pullPos = new Vector3(
                    Mathf.Sin(bearing) * pullRadius,
                    playerPos.y + 22f,
                    Mathf.Cos(bearing) * pullRadius);

                cam.transform.position = Vector3.Lerp(cam.transform.position, pullPos, dt * 2.5f);
                Vector3 forward = (new Vector3(0f, 3f, 0f) - cam.transform.position).normalized;
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 85f, dt * 2f);
            }

            // Apply camera shake if active
            if (shakeIntensity > 0.001f)
            {
                cam.transform.position += new Vector3(
                    (Mathf.PerlinNoise(t * 30f, 0f) - 0.5f) * shakeIntensity,
                    (Mathf.PerlinNoise(0f, t * 30f) - 0.5f) * shakeIntensity,
                    (Mathf.PerlinNoise(t * 25f, t * 25f) - 0.5f) * shakeIntensity
                );
            }
        }

        private void OnGUI()
        {
            if (!isRunning)
            {
                return;
            }

            InitStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;
            float t = trailerTimer;

            // 1. Cinematic letterbox bars (10% top & bottom)
            float barH = screenH * 0.10f;
            if (blackTexture != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, screenW, barH), blackTexture);
                GUI.DrawTexture(new Rect(0f, screenH - barH, screenW, barH), blackTexture);
            }

            // 2. Broadcast Telemetry Overlay (Top-Left: REC ● 00:xx:xx // CAM: RIG-02)
            if (t < 53f)
            {
                int totalSecs = Mathf.FloorToInt(t);
                int centis = Mathf.FloorToInt((t - totalSecs) * 100f);
                string timecode = $"00:{totalSecs:D2}:{centis:D2}";

                // Blinking red recording indicator
                bool blinkOn = (Mathf.FloorToInt(t * 2.5f) % 2) == 0;
                string dot = blinkOn ? "●" : "○";

                string recText = $"REC {dot} {timecode} // RIG-02 [ORBIT-RING]";
                GUI.Label(new Rect(28f, barH + 12f, 400f, 22f), recText, recStyle);
            }

            // 3. Scene Title Cards with Lower-Third Holographic Styling
            float cardY = screenH * 0.77f;
            Rect titleRect = new Rect(0f, cardY, screenW, 36f);
            Rect subRect = new Rect(0f, cardY + 34f, screenW, 24f);

            if (t >= 1.5f && t < 6.2f)
            {
                DrawCard(titleRect, subRect, "AN ORBITAL ARENA", "NO ESCAPE FROM THE VOLCANIC RING", t, 1.5f, 6.2f);
            }
            else if (t >= 8.5f && t < 15.0f)
            {
                DrawCard(titleRect, subRect, "RELENTLESS WAVES", "SURVIVE THE CURVE • 360° THREAT MATRIX", t, 8.5f, 15.0f);
            }
            else if (t >= 17.0f && t < 25.0f)
            {
                DrawCard(titleRect, subRect, "DEFLECT. DASH. CONTROL TIME.", "BULLET-TIME FLIGHT AT ZERO MARGIN", t, 17.0f, 25.0f);
            }
            else if (t >= 28.0f && t < 39.0f)
            {
                DrawCard(titleRect, subRect, "THE LEVIATHAN AWAKENS", "COLOSSAL DREADNOUGHT CLASH", t, 28.0f, 39.0f);
            }
            else if (t >= 43.0f && t < 51.0f)
            {
                DrawCard(titleRect, subRect, "FIGHT TO THE CORE", "UPGRADE YOUR SHIP • CRUSH THE RECORD", t, 43.0f, 51.0f);
            }

            // 4. Outro Title Reveal (53s - 60s)
            if (t >= 53f)
            {
                float fadeAlpha = Mathf.Clamp01((t - 53f) / 1.5f);
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), blackTexture);
                GUI.color = prev;

                if (t >= 54.5f)
                {
                    float logoAlpha = Mathf.Clamp01((t - 54.5f) / 1.0f);

                    Color c = logoTitleStyle.normal.textColor;
                    logoTitleStyle.normal.textColor = new Color(c.r, c.g, c.b, logoAlpha);
                    Color sc = logoSubStyle.normal.textColor;
                    logoSubStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, logoAlpha * 0.88f);

                    float wishlistPulse = 0.8f + 0.2f * Mathf.Sin((t - 54.5f) * 4f);
                    Color wc = logoWishlistStyle.normal.textColor;
                    logoWishlistStyle.normal.textColor = new Color(wc.r, wc.g, wc.b, logoAlpha * wishlistPulse);

                    float centerY = screenH * 0.38f;
                    GUI.Label(new Rect(0f, centerY, screenW, 58f), "SURVIVAL CHAOS", logoTitleStyle);
                    GUI.Label(new Rect(0f, centerY + 62f, screenW, 30f), "AN ORBITAL SURVIVAL SHOOTER", logoSubStyle);
                    GUI.Label(new Rect(0f, centerY + 120f, screenW, 36f), "[ ◈ PLAY IT ON ITCH.IO ◈ ]", logoWishlistStyle);
                }
            }
        }

        private void DrawCard(Rect r1, Rect r2, string title, string sub, float t, float start, float end)
        {
            float duration = end - start;
            float progress = t - start;
            float alpha = progress < 0.6f ? progress / 0.6f :
                          progress > duration - 0.6f ? (duration - progress) / 0.6f : 1f;
            alpha = Mathf.Clamp01(alpha);

            Color c = titleCardStyle.normal.textColor;
            titleCardStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
            Color sc = titleSubStyle.normal.textColor;
            titleSubStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha * 0.85f);

            GUI.Label(r1, title, titleCardStyle);
            GUI.Label(r2, sub, titleSubStyle);

            // Subtle cyan accent line beneath the card
            if (whiteTexture != null && alpha > 0.01f)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.35f, 0.88f, 1f, alpha * 0.5f);
                float lineW = Screen.width * 0.28f;
                float lineX = (Screen.width - lineW) * 0.5f;
                GUI.DrawTexture(new Rect(lineX, r2.y + 28f, lineW, 2f), whiteTexture);
                GUI.color = prev;
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

            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }

            if (recStyle == null)
            {
                recStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 11, 16),
                    fontStyle = FontStyle.Bold
                };
                recStyle.normal.textColor = new Color(0.95f, 0.25f, 0.25f, 0.85f);
            }

            if (titleCardStyle == null)
            {
                titleCardStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.034f), 20, 36),
                    fontStyle = FontStyle.Bold
                };
                titleCardStyle.normal.textColor = new Color(0.35f, 0.88f, 1f, 1f);
            }

            if (titleSubStyle == null)
            {
                titleSubStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.018f), 12, 19),
                    fontStyle = FontStyle.Normal
                };
                titleSubStyle.normal.textColor = new Color(0.72f, 0.92f, 1f, 0.85f);
            }

            if (logoTitleStyle == null)
            {
                logoTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.065f), 36, 68),
                    fontStyle = FontStyle.Bold
                };
                logoTitleStyle.normal.textColor = new Color(0.35f, 0.88f, 1f, 1f);
            }

            if (logoSubStyle == null)
            {
                logoSubStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.024f), 14, 26),
                    fontStyle = FontStyle.Normal
                };
                logoSubStyle.normal.textColor = new Color(0.8f, 0.92f, 1f, 0.9f);
            }

            if (logoWishlistStyle == null)
            {
                logoWishlistStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.028f), 16, 30),
                    fontStyle = FontStyle.Bold
                };
                logoWishlistStyle.normal.textColor = new Color(1f, 0.85f, 0.3f, 1f);
            }
        }
    }
}
#else
namespace SurvivalChaos
{
    /// <summary>Release builds carry no trailer tooling; this answers the callers.</summary>
    public static class CinematicTrailerDirector
    {
        public static bool IsRunning => false;
    }
}
#endif
