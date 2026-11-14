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
    /// 1. Plays a developer card ("IAN WANDERLEY PRESENTS") with expanding rules, in silence.
    /// 2. Fades into the title scene and its logo. A text card naming the game stood between
    ///    the two until 12 November 2026; the logo says it better.
    /// 3. The music fades in with it. That fade is the music component's own: this only holds
    ///    it back until then (see MusicSource.HeldBack).
    /// 4. Holds in arcade attract mode with a breathing "PRESS ANY KEY" prompt.
    /// 5. On keypress, triggers a reactor ignition flash, sound burst, animated menu reveal,
    ///    and auto-focuses the first button for pad/keyboard navigation.
    /// 6. If idle on the title panel for 40 seconds, smoothly returns to attract mode. Only
    ///    the title panel counts: the prompt is drawn over the whole screen, and on the
    ///    options or credits it covered a screen someone was reading. Mouse movement, the
    ///    wheel, the sticks and the d-pad count as activity, as well as presses.
    ///
    /// Pressing any key during the splash goes straight to the menu, in one press. Returning to
    /// the menu from a run skips the splash and presents the ready menu.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class TitleSplashDirector : MonoBehaviour
    {
        private enum SplashState
        {
            None,
            DeveloperCard,
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

        /// <summary>
        /// How bright the title film stays behind the options and the credits.
        /// The film carries the logo, and at full strength it showed through
        /// those panels behind their text (12 November 2026).
        /// </summary>
        public const float BackdropBehindPanels = 0.3f;

        private Graphic backdrop;
        private float backdropLevel = 1f;

        private CanvasGroup titleScreenCanvasGroup;
        private RectTransform titleScreenRect;

        private Texture2D blackTexture;
        private Texture2D whiteTexture;
        private GUIStyle devCardStyle;
        private GUIStyle devSubStyle;
        private GUIStyle attractPromptStyle;

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
                MusicSource.HeldBack = false;
                return;
            }

            // Asked for now, before the scene's first frame, so not a note of
            // the music is heard under the opening card.
            MusicSource.HeldBack = !hasPlayedSessionSplash;

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
                backdrop = null;
                MusicSource.HeldBack = false;
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

            GameObject backdropGo = GameObject.Find("Video Background");
            backdrop = backdropGo != null ? backdropGo.GetComponent<Graphic>() : null;
            backdropLevel = 1f;

            if (!hasPlayedSessionSplash)
            {
                // First boot: play full splash sequence
                hasPlayedSessionSplash = true;
                blackAlpha = 1f;
                flashAlpha = 0f;
                menuIdleTimer = 0f;

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
                MusicSource.HeldBack = false;
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
            float dt = Mathf.Min(CaptureClock.UnscaledDelta, MaxSplashStep);
            stateTimer += dt;

            // Decay engagement flash
            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.Max(0f, flashAlpha - dt * 3.5f);
            }

            DimBackdrop(dt);

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
                    if (AnyInputTriggered())
                    {
                        SkipToMenu();
                    }
                    else if (stateTimer >= 2.1f)
                    {
                        PlayTitleSting();
                        MusicSource.HeldBack = false;
                        state = SplashState.FadeToAttract;
                        stateTimer = 0f;
                    }
                    break;

                case SplashState.FadeToAttract:
                    float fadeT = Mathf.Clamp01(stateTimer / 0.85f);
                    blackAlpha = Mathf.Lerp(1f, 0f, fadeT);

                    if (AnyInputTriggered())
                    {
                        SkipToMenu();
                    }
                    else if (stateTimer >= 0.85f)
                    {
                        blackAlpha = 0f;
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

        /// <summary>
        /// One press during the opening goes to the menu. It used to take one
        /// for each card, one for the fade and one for the prompt.
        /// </summary>
        private void SkipToMenu()
        {
            blackAlpha = 0f;
            MusicSource.HeldBack = false;
            TriggerMenuReveal();
        }

        /// <summary>
        /// The level the title film is drawn at: full with the title panel up,
        /// <see cref="BackdropBehindPanels"/> with another screen open over it.
        /// </summary>
        public static float BackdropTarget(bool titlePanelActive)
        {
            return titlePanelActive ? 1f : BackdropBehindPanels;
        }

        private void DimBackdrop(float dt)
        {
            if (backdrop == null || titleScreenCanvasGroup == null)
            {
                return;
            }

            float want = BackdropTarget(titleScreenCanvasGroup.gameObject.activeInHierarchy);
            backdropLevel = Mathf.MoveTowards(backdropLevel, want, dt * 4f);
            backdrop.color = new Color(backdropLevel, backdropLevel, backdropLevel, 1f);
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
                float nameH = devCardStyle.fontSize * 1.4f;
                GUI.Label(new Rect(0f, centerY, screenW, nameH), "IAN WANDERLEY", devCardStyle);
                GUI.Label(new Rect(0f, centerY + nameH, screenW, devSubStyle.fontSize * 1.6f), "PRESENTS", devSubStyle);

                // Laser rules flanking the text
                if (whiteTexture != null && alpha > 0.01f)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(0.4f, 0.85f, 1f, alpha * 0.6f);

                    float textMidX = screenW * 0.5f;
                    float textGap = devCardStyle.fontSize * 6f;
                    float ruleY = centerY + nameH * 0.5f;

                    // Left rule
                    GUI.DrawTexture(new Rect(textMidX - textGap - lineW, ruleY, lineW, 2f), whiteTexture);
                    // Right rule
                    GUI.DrawTexture(new Rect(textMidX + textGap, ruleY, lineW, 2f), whiteTexture);

                    GUI.color = prev;
                }
            }
            // 4. Arcade Attract Mode: pulsing prompt over video background
            if (state == SplashState.AttractMode)
            {
                float pulse = 0.50f + 0.50f * Mathf.Sin(Time.unscaledTime * 4.8f);
                Color pc = attractPromptStyle.normal.textColor;
                attractPromptStyle.normal.textColor = new Color(pc.r, pc.g, pc.b, pulse);

                float promptH = attractPromptStyle.fontSize * 1.5f;
                GUI.Label(new Rect(0f, screenH * 0.80f, screenW, promptH), "PRESS ANY KEY", attractPromptStyle);
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
                devCardStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                devCardStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);
            }

            if (devSubStyle == null)
            {
                devSubStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                devSubStyle.normal.textColor = new Color(0.6f, 0.78f, 0.92f, 0.8f);
            }

            if (attractPromptStyle == null)
            {
                attractPromptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                attractPromptStyle.normal.textColor = new Color(0.40f, 0.92f, 1f, 1f);
            }

            // Every frame: the resolution can change in the options.
            OverlayFont.Fit(devCardStyle, 0.038f, 22, 54);
            OverlayFont.Fit(devSubStyle, 0.020f, 13, 28);
            OverlayFont.Fit(attractPromptStyle, 0.026f, 16, 38);
        }
    }
}
