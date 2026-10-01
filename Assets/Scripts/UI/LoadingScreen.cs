using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Covers every scene change with a holo loading screen, so starting a run,
    /// restarting and returning to the menu no longer freeze the screen they
    /// left with nothing to say why.
    ///
    /// Measured in a build on 29 Sep 2026: the game scene loads in about 0.4 s,
    /// and its first two frames then take about 0.48 and 0.28 s while the GPU
    /// builds what it has not drawn before. The screen stays up through both
    /// and only goes once frames are coming at a normal pace, so the run is
    /// never first seen stuttering.
    ///
    /// Game time is held at zero from the moment the screen goes up until it
    /// starts to clear. The old scene stops where it was instead of carrying
    /// on under the fade - a paused or finished run used to be unpaused to
    /// load - and the new run starts when the player can see it.
    ///
    /// Lives on a prefab in Resources, built by LoadingScreenBuilder, and makes
    /// itself on first use. Without the prefab it falls back to a plain load:
    /// a missing loading screen must never be a way to get stuck on a menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        /// <summary>Where the prefab has to live for Resources.Load to find it.</summary>
        public const string ResourcePath = "LoadingScreen";

        [SerializeField]
        private CanvasGroup group;

        [SerializeField]
        [Tooltip("A holo bar image. HoloBar draws its fill amount.")]
        private Image bar;

        [SerializeField]
        private TMP_Text status;

        [SerializeField]
        [Tooltip("Seconds to cover the screen. Short: the scene behind is frozen by then.")]
        private float fadeInSeconds = 0.15f;

        [SerializeField]
        [Tooltip("Seconds to uncover the new scene.")]
        private float fadeOutSeconds = 0.3f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Real seconds the music takes to fade out as the screen comes up. The load waits " +
                 "for it, so this much is added to every load beyond the screen's own fade.")]
        private float musicFadeSeconds = 0.4f;

        [SerializeField]
        [Tooltip("The new scene has to draw at least this many frames under the screen.")]
        private int minimumWarmFrames = 3;

        [SerializeField]
        [Tooltip("A frame this quick or quicker counts as settled.")]
        private float settledFrameSeconds = 0.05f;

        [SerializeField]
        [Tooltip("How many settled frames in a row before the screen clears.")]
        private int settledFramesNeeded = 2;

        [SerializeField]
        [Tooltip("The longest the screen waits for frames to settle, so a slow machine is not " +
                 "kept on it forever.")]
        private float maximumWarmSeconds = 3f;

        // How much of the bar each part fills: the load itself, then the new
        // scene's first frames. The load is the part Unity reports progress for.
        private const float LoadShare = 0.75f;

        private static LoadingScreen instance;

        /// <summary>True from the moment a load is asked for until the screen has cleared.</summary>
        public static bool Busy { get; private set; }

        /// <summary>
        /// Loads a scene behind the loading screen. Asking again while a load is
        /// under way does nothing, so a second press of Play cannot start a
        /// second load.
        /// </summary>
        public static void Load(string scene, string message)
        {
            if (Busy)
            {
                return;
            }

            LoadingScreen screen = Instance();
            if (screen == null)
            {
                RunTime.ResetForNewRun();
                SceneManager.LoadScene(scene);
                return;
            }

            Busy = true;
            screen.gameObject.SetActive(true);
            screen.StartCoroutine(screen.Run(scene, message));
        }

        private static LoadingScreen Instance()
        {
            if (instance != null)
            {
                return instance;
            }

            LoadingScreen prefab = Resources.Load<LoadingScreen>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("No LoadingScreen prefab in Resources, so scenes load without one. " +
                                 "Build it from Survival Chaos > UI > Build Loading Screen.");
                return null;
            }

            instance = Instantiate(prefab);
            instance.name = prefab.name;
            DontDestroyOnLoad(instance.gameObject);
            instance.Hide();
            return instance;
        }

        // Domain reload can be off in the editor, and then statics outlive play
        // mode while the object they point at does not.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Busy = false;
        }

        private IEnumerator Run(string scene, string message)
        {
            RunTime.SetHold(true);
            status.text = message;
            bar.fillAmount = 0f;

            // The music goes out as the screen comes up, and the load waits for
            // it: the old scene's track would otherwise stop dead when the new
            // scene replaces it.
            MusicSource.FadeAllOut(musicFadeSeconds);
            yield return Fade(0f, 1f, fadeInSeconds);
            if (musicFadeSeconds > fadeInSeconds)
            {
                yield return new WaitForSecondsRealtime(musicFadeSeconds - fadeInSeconds);
            }

            // One whole frame fully covered before the heavy work starts, so the
            // screen is what stays up while the load holds the main thread.
            yield return null;

            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone)
            {
                bar.fillAmount = Mathf.Clamp01(load.progress / 0.9f) * LoadShare;
                yield return null;
            }

            yield return WarmUp();

            bar.fillAmount = 1f;
            RunTime.SetHold(false);
            yield return Fade(1f, 0f, fadeOutSeconds);

            // Before Hide: deactivating the object ends this coroutine.
            Busy = false;
            Hide();
        }

        /// <summary>
        /// Holds the screen while the new scene draws its first frames, which
        /// are the slow ones. Unloading the old scene put game time back to
        /// normal, so it is held at zero here on every frame.
        /// </summary>
        private IEnumerator WarmUp()
        {
            float started = Time.realtimeSinceStartup;
            int frames = 0;
            int settled = 0;

            while (true)
            {
                Time.timeScale = 0f;
                yield return null;

                frames++;
                settled = Time.unscaledDeltaTime <= settledFrameSeconds ? settled + 1 : 0;

                float waited = Time.realtimeSinceStartup - started;
                bool ready = frames >= minimumWarmFrames && settled >= settledFramesNeeded;
                if (ready || waited >= maximumWarmSeconds)
                {
                    yield break;
                }

                // Creeps toward full: how many slow frames are left is not
                // something anyone knows in advance.
                float share = 1f - Mathf.Exp(-frames / 3f);
                bar.fillAmount = LoadShare + (1f - LoadShare) * share * 0.9f;
            }
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                group.alpha = Mathf.Lerp(from, to, elapsed / seconds);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            group.alpha = to;
        }

        private void Hide()
        {
            group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
