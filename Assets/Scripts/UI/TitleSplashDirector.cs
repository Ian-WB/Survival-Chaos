using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalChaos
{
    /// <summary>
    /// Coordinates the cinematic boot splash and attract mode on the Title Menu (Menu.unity).
    ///
    /// On initial launch:
    /// 1. Plays a cinematic developer splash ("IAN WANDERLEY PRESENTS") with expanding laser rules.
    /// 2. Slams into the holographic title card ("SURVIVAL CHAOS") with audio impact cue.
    /// 3. Fades smoothly into the video background (TitleScreen.mp4) with music crescendo.
    /// 4. Holds in arcade attract mode with a breathing "[ ◈ PRESS ANY KEY TO ENGAGE ◈ ]" prompt.
    /// 5. On keypress, triggers a reactor ignition flash, sound burst, animated menu reveal,
    ///    and auto-focuses the first button for pad/keyboard navigation.
    /// 6. If idle on the title panel for 40 seconds, smoothly returns to attract mode. Only
    ///    the title panel counts: the prompt is drawn over the whole screen, and on the
    ///    options or credits it covered a screen someone was reading. Mouse movement, the
    ///    wheel, the sticks and the d-pad count as activity, as well as presses.
    ///
    /// Pressing any key during the splash skips directly to attract/menu. Returning to the menu
    /// from a run skips the splash and presents the ready menu.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class TitleSplashDirector : MonoBehaviour
    {
        private enum SplashState
        {
            None,
            DeveloperCard,
            TitleCard,
            FadeToAttract,
            AttractMode,
            RevealingMenu,
            Ready
        }

        private static TitleSplashDirector instance;
        private static bool hasPlayedSessionSplash;

        /// <summary>Master switch to enable or disable the boot splash.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Seconds idle on the title panel before the attract prompt returns.</summary>
        public const float AttractIdleSeconds = 40f;

        /// <summary>Stick travel that counts as someone at the pad. Below it is drift.</summary>
        public const float StickActivity = 0.2f;

        /// <summary>The most one frame may move the splash on, in seconds.</summary>
        private const float MaxSplashStep = 0.1f;

        private SplashState state = SplashState.None;
        private float stateTimer;
        private float blackAlpha = 1f;
        private float flashAlpha = 0f;
        private float menuIdleTimer;

        private CanvasGroup titleScreenCanvasGroup;
        private RectTransform titleScreenRect;
        private AudioSource menuMusic;
        private float originalMusicVolume = 0.2066f;

        private Texture2D blackTexture;
        private Texture2D whiteTexture;
        private GUIStyle devCardStyle;
        private GUIStyle devSubStyle;
        private GUIStyle titleCardStyle;
        private GUIStyle titleSubStyle;
        private GUIStyle attractPromptStyle;
        private GUIStyle attractSubPromptStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null || !Application.isPlaying)
            {
                return;
            }

            GameObject host = new GameObject("Title Splash Director");
            instance = host.AddComponent<TitleSplashDirector>();
            DontDestroyOnLoad(host);

            // sceneLoaded has already fired for the scene the game started in. Without this the
            // boot splash was skipped at launch and played on the first return from a run.
            instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance = null;
            hasPlayedSessionSplash = false;

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
            if (whiteTexture != null)
            {
                Destroy(whiteTexture);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Menu" || !Enabled)
            {
                state = SplashState.None;
                return;
            }

            StopAllCoroutines();
            StartCoroutine(SetupMenuScene());
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "Menu")
            {
                StopAllCoroutines();
                state = SplashState.None;
                titleScreenCanvasGroup = null;
                titleScreenRect = null;
                menuMusic = null;
            }
        }

        private IEnumerator SetupMenuScene()
        {
            // Wait one frame so Canvas and MenuScreens are awakened and placed
            yield return null;

            GameObject titleScreenGo = GameObject.Find("Title Screen");
            if (titleScreenGo != null)
            {
                if (!titleScreenGo.TryGetComponent(out titleScreenCanvasGroup))
                {
                    titleScreenCanvasGroup = titleScreenGo.AddComponent<CanvasGroup>();
                }
                titleScreenRect = titleScreenGo.GetComponent<RectTransform>();
            }

            GameObject musicGo = GameObject.Find("Music");
            if (musicGo != null && musicGo.TryGetComponent(out menuMusic))
            {
                originalMusicVolume = menuMusic.volume > 0.01f ? menuMusic.volume : 0.2066f;
            }

            if (!hasPlayedSessionSplash)
            {
                // First boot: play full splash sequence
                hasPlayedSessionSplash = true;
                blackAlpha = 1f;
                flashAlpha = 0f;
                menuIdleTimer = 0f;

                if (menuMusic != null)
                {
                    menuMusic.volume = 0f;
                }

                if (titleScreenCanvasGroup != null)
                {
                    titleScreenCanvasGroup.alpha = 0f;
                    titleScreenCanvasGroup.interactable = false;
                    titleScreenCanvasGroup.blocksRaycasts = false;
                }

                state = SplashState.DeveloperCard;
                stateTimer = 0f;
            }
            else
            {
                // Returning from a run: menu entries ready immediately
                blackAlpha = 0f;
                flashAlpha = 0f;
                if (titleScreenCanvasGroup != null)
                {
                    titleScreenCanvasGroup.alpha = 1f;
                    titleScreenCanvasGroup.interactable = true;
                    titleScreenCanvasGroup.blocksRaycasts = true;
                }

                // A fresh wait. The count left from before the run carried over,
                // and a run started near the 40 s mark - or from the attract
                // prompt itself - came back straight to the prompt.
                menuIdleTimer = 0f;
                state = SplashState.Ready;
            }
        }

        private void Update()
        {
            if (state == SplashState.None || SceneManager.GetActiveScene().name != "Menu")
            {
                return;
            }

            // Capped, because the first frame after the load carries the load's
            // hitch: in the editor it was about 1.5 s, which the developer card
            // took off its own 2.1 s and showed for 0.65 (10 November 2026).
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxSplashStep);
            stateTimer += dt;

            // Decay engagement flash
            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.Max(0f, flashAlpha - dt * 3.5f);
            }

            // Idle on the title panel in the Ready state returns to attract mode.
            // Anywhere else the count starts again, so backing out of the options
            // gives the full wait rather than whatever was left of it.
            if (state == SplashState.Ready)
            {
                menuIdleTimer = NextIdle(menuIdleTimer, dt, TitlePanelShown(), AnyActivity());
                if (AttractDue(menuIdleTimer))
                {
                    EnterAttractMode();
                }
                return;
            }

            // Progression state machine
            switch (state)
            {
                case SplashState.DeveloperCard:
                    blackAlpha = 1f;
                    if (AnyInputTriggered() || stateTimer >= 2.1f)
                    {
                        PlayTitleSting();
                        state = SplashState.TitleCard;
                        stateTimer = 0f;
                    }
                    break;

                case SplashState.TitleCard:
                    blackAlpha = 1f;
                    if (AnyInputTriggered() || stateTimer >= 2.3f)
                    {
                        state = SplashState.FadeToAttract;
                        stateTimer = 0f;
                    }
                    break;

                case SplashState.FadeToAttract:
                    float fadeT = Mathf.Clamp01(stateTimer / 0.85f);
                    blackAlpha = Mathf.Lerp(1f, 0f, fadeT);

                    if (menuMusic != null)
                    {
                        menuMusic.volume = Mathf.Lerp(0f, originalMusicVolume, fadeT);
                    }

                    if (AnyInputTriggered() || stateTimer >= 0.85f)
                    {
                        blackAlpha = 0f;
                        if (menuMusic != null)
                        {
                            menuMusic.volume = originalMusicVolume;
                        }
                        state = SplashState.AttractMode;
                        stateTimer = 0f;
                    }
                    break;

                case SplashState.AttractMode:
                    blackAlpha = 0f;
                    if (AnyInputTriggered())
                    {
                        TriggerMenuReveal();
                    }
                    break;

                case SplashState.RevealingMenu:
                    blackAlpha = 0f;
                    float revealDuration = 0.40f;
                    float revealT = Mathf.Clamp01(stateTimer / revealDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, revealT);

                    if (titleScreenCanvasGroup != null)
                    {
                        titleScreenCanvasGroup.alpha = smoothT;
                    }

                    if (titleScreenRect != null)
                    {
                        // Subtle slide into position from -25px left
                        float offsetX = Mathf.Lerp(-25f, 0f, smoothT);
                        titleScreenRect.anchoredPosition = new Vector2(offsetX, titleScreenRect.anchoredPosition.y);
                    }

                    if (stateTimer >= revealDuration)
                    {
                        if (titleScreenCanvasGroup != null)
                        {
                            titleScreenCanvasGroup.alpha = 1f;
                            titleScreenCanvasGroup.interactable = true;
                            titleScreenCanvasGroup.blocksRaycasts = true;
                        }
                        if (titleScreenRect != null)
                        {
                            titleScreenRect.anchoredPosition = new Vector2(0f, titleScreenRect.anchoredPosition.y);
                        }

                        FocusFirstButton();
                        menuIdleTimer = 0f;
                        state = SplashState.Ready;
                    }
                    break;
            }
        }

        private void EnterAttractMode()
        {
            if (titleScreenCanvasGroup != null)
            {
                titleScreenCanvasGroup.alpha = 0f;
                titleScreenCanvasGroup.interactable = false;
                titleScreenCanvasGroup.blocksRaycasts = false;
            }

            state = SplashState.AttractMode;
            stateTimer = 0f;
        }

        /// <summary>
        /// Whether idle time counts toward the attract prompt: only with the
        /// title panel up and showing. Its siblings are the other menu screens,
        /// and opening one switches it off.
        /// </summary>
        public static bool IdleCounts(bool titleActive, float titleAlpha)
        {
            return titleActive && titleAlpha > 0.5f;
        }

        /// <summary>
        /// The idle count after <paramref name="dt"/> more seconds: on from
        /// <paramref name="idle"/> while it counts and nothing was touched,
        /// back to nothing otherwise.
        /// </summary>
        public static float NextIdle(float idle, float dt, bool counts, bool activity)
        {
            return counts && !activity ? idle + Mathf.Max(0f, dt) : 0f;
        }

        /// <summary>Whether <paramref name="idle"/> seconds is long enough for the attract prompt.</summary>
        public static bool AttractDue(float idle)
        {
            return idle >= AttractIdleSeconds;
        }

        /// <summary>
        /// Whether anything was moved this frame: the mouse, its wheel, either
        /// stick past drift, or the d-pad. Presses are <see cref="AnyInputTriggered"/>.
        /// </summary>
        public static bool Moved(Vector2 mouseDelta, Vector2 scroll, Vector2 leftStick, Vector2 rightStick, Vector2 dpad)
        {
            return mouseDelta.sqrMagnitude > 0f
                || scroll.sqrMagnitude > 0f
                || leftStick.magnitude > StickActivity
                || rightStick.magnitude > StickActivity
                || dpad.sqrMagnitude > 0f;
        }

        private bool TitlePanelShown()
        {
            return titleScreenCanvasGroup != null
                && IdleCounts(titleScreenCanvasGroup.gameObject.activeInHierarchy, titleScreenCanvasGroup.alpha);
        }

        private bool AnyActivity()
        {
            if (AnyInputTriggered())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            Vector2 scroll = mouse != null ? mouse.scroll.ReadValue() : Vector2.zero;
            Vector2 leftStick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            Vector2 rightStick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
            Vector2 dpad = pad != null ? pad.dpad.ReadValue() : Vector2.zero;
            if (Moved(mouseDelta, scroll, leftStick, rightStick, dpad))
            {
                return true;
            }
#endif

            // Held keys and anything the game's own input maps, keyboard arrows included.
            return Mathf.Abs(GameInput.Horizontal) > StickActivity || Mathf.Abs(GameInput.Vertical) > StickActivity;
        }

        private bool AnyInputTriggered()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
            {
                return true;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                return true;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame))
            {
                return true;
            }
#endif
            return GameInput.DashPressed ||
                   GameInput.PausePressed ||
                   GameInput.ToggleDirectionReleased;
        }

        private void PlayTitleSting()
        {
            if (GameSounds.Instance != null && GameSounds.Instance.SalvagePicked != null)
            {
                GameSounds.Play(GameSounds.Instance.SalvagePicked);
            }
        }

        /// <summary>Triggers the attract mode exit transition into the menu buttons.</summary>
        public void TriggerMenuReveal()
        {
            // Ignition sound feedback
            if (GameSounds.Instance != null)
            {
                if (GameSounds.Instance.DeflectorBlock != null)
                {
                    GameSounds.Play(GameSounds.Instance.DeflectorBlock);
                }
                if (GameSounds.Instance.SkillPicked != null)
                {
                    GameSounds.Play(GameSounds.Instance.SkillPicked);
                }
            }

            // Haptic burst
            Rumble.Pulse(Rumble.Strength.Light);

            // Momentary ignition screen flash
            flashAlpha = 0.45f;

            state = SplashState.RevealingMenu;
            stateTimer = 0f;
        }

        private void FocusFirstButton()
        {
            if (titleScreenCanvasGroup == null)
            {
                return;
            }

            Selectable first = titleScreenCanvasGroup.GetComponentInChildren<Selectable>();
            if (first != null && EventSystem.current != null)
            {
                first.Select();
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        private void OnGUI()
        {
            if (state == SplashState.None || SceneManager.GetActiveScene().name != "Menu")
            {
                return;
            }

            InitStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. Draw solid black backdrop / fade
            if (blackAlpha > 0.001f && blackTexture != null)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, blackAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), blackTexture);
                GUI.color = prev;
            }

            // 2. Developer Card with expanding laser rules
            if (state == SplashState.DeveloperCard)
            {
                float alpha = stateTimer < 0.45f ? (stateTimer / 0.45f) :
                              stateTimer > 1.65f ? (2.1f - stateTimer) / 0.45f : 1f;
                alpha = Mathf.Clamp01(alpha);

                // Expanding laser line progression (0% -> 100%)
                float expandT = Mathf.Clamp01(stateTimer / 0.6f);
                float lineW = Mathf.Lerp(40f, screenW * 0.22f, expandT);

                Color c = devCardStyle.normal.textColor;
                devCardStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                Color sc = devSubStyle.normal.textColor;
                devSubStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha * 0.75f);

                // Vertical drift
                float centerY = screenH * 0.45f - stateTimer * 4f;

                // Center text
                GUI.Label(new Rect(0f, centerY, screenW, 36f), "IAN WANDERLEY", devCardStyle);
                GUI.Label(new Rect(0f, centerY + 38f, screenW, 24f), "PRESENTS", devSubStyle);

                // Laser rules flanking the text
                if (whiteTexture != null && alpha > 0.01f)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(0.4f, 0.85f, 1f, alpha * 0.6f);

                    float textMidX = screenW * 0.5f;
                    float textGap = 160f;
                    float ruleY = centerY + 18f;

                    // Left rule
                    GUI.DrawTexture(new Rect(textMidX - textGap - lineW, ruleY, lineW, 2f), whiteTexture);
                    // Right rule
                    GUI.DrawTexture(new Rect(textMidX + textGap, ruleY, lineW, 2f), whiteTexture);

                    GUI.color = prev;
                }
            }
            // 3. Title Card with holographic styling
            else if (state == SplashState.TitleCard)
            {
                float alpha = stateTimer < 0.4f ? (stateTimer / 0.4f) :
                              stateTimer > 1.9f ? (2.3f - stateTimer) / 0.4f : 1f;
                alpha = Mathf.Clamp01(alpha);

                Color c = titleCardStyle.normal.textColor;
                titleCardStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                Color sc = titleSubStyle.normal.textColor;
                titleSubStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha * 0.85f);

                // A version line under these, "SYSTEM REVISION 2.4.0", was invented
                // and matched no build; Ian had it taken out on 10 November 2026.
                float centerY = screenH * 0.43f - stateTimer * 3f;
                GUI.Label(new Rect(0f, centerY, screenW, 52f), "SURVIVAL CHAOS", titleCardStyle);
                GUI.Label(new Rect(0f, centerY + 54f, screenW, 26f), "AN ORBITAL SURVIVAL SHOOTER", titleSubStyle);
            }
            // 4. Arcade Attract Mode: pulsing prompt over video background
            else if (state == SplashState.AttractMode)
            {
                float pulse = 0.50f + 0.50f * Mathf.Sin(Time.unscaledTime * 4.8f);
                Color pc = attractPromptStyle.normal.textColor;
                attractPromptStyle.normal.textColor = new Color(pc.r, pc.g, pc.b, pulse);

                float subPulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4.8f + 0.5f);
                Color spc = attractSubPromptStyle.normal.textColor;
                attractSubPromptStyle.normal.textColor = new Color(spc.r, spc.g, spc.b, subPulse * 0.8f);

                float promptY = screenH * 0.80f;
                GUI.Label(new Rect(0f, promptY, screenW, 36f), "[ ◈ PRESS ANY KEY TO ENGAGE ◈ ]", attractPromptStyle);
                GUI.Label(new Rect(0f, promptY + 34f, screenW, 22f), "[ SPACE  •  ENTER  •  GAMEPAD A ]", attractSubPromptStyle);
            }

            // 5. Engagement Ignition Screen Flash
            if (flashAlpha > 0.001f && whiteTexture != null)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.35f, 0.85f, 1f, flashAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), whiteTexture);
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

            if (devCardStyle == null)
            {
                devCardStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.038f), 22, 38),
                    fontStyle = FontStyle.Bold
                };
                devCardStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);
            }

            if (devSubStyle == null)
            {
                devSubStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.020f), 13, 20),
                    fontStyle = FontStyle.Normal
                };
                devSubStyle.normal.textColor = new Color(0.6f, 0.78f, 0.92f, 0.8f);
            }

            if (titleCardStyle == null)
            {
                titleCardStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.054f), 32, 58),
                    fontStyle = FontStyle.Bold
                };
                titleCardStyle.normal.textColor = new Color(0.35f, 0.88f, 1f, 1f);
            }

            if (titleSubStyle == null)
            {
                titleSubStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.022f), 14, 22),
                    fontStyle = FontStyle.Normal
                };
                titleSubStyle.normal.textColor = new Color(0.68f, 0.88f, 1f, 0.85f);
            }

            if (attractPromptStyle == null)
            {
                attractPromptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.026f), 16, 26),
                    fontStyle = FontStyle.Bold
                };
                attractPromptStyle.normal.textColor = new Color(0.40f, 0.92f, 1f, 1f);
            }

            if (attractSubPromptStyle == null)
            {
                attractSubPromptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 11, 16),
                    fontStyle = FontStyle.Normal
                };
                attractSubPromptStyle.normal.textColor = new Color(0.55f, 0.80f, 0.92f, 0.75f);
            }
        }
    }
}
