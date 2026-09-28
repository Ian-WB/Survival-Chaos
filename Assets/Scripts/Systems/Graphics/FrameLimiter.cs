using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

namespace SurvivalChaos
{
    /// <summary>
    /// Holds the game to the frame cap. This is the job
    /// Application.targetFrameRate used to do, and that stays at -1 now.
    ///
    /// Unity's cap was the stutter. Measured on 26 Sep 2026, in play with the
    /// bot flying and the cap at 60 on a 200 Hz display, frames alternated
    /// between about 6 ms and 30 ms. The deviation was 4.6 ms, and 48% of
    /// frames landed more than 2 ms off the 16.7 they were meant to take. Every
    /// build and the editor had it whenever a cap was on, and Unity's own
    /// documentation calls targetFrameRate "subject to microstuttering". A wait
    /// at the same point in the same run held 98% of frames between 16.07 and
    /// 17.27 ms, a deviation of 0.14 ms. Through the menu, alternated with
    /// Unity's cap three times as the run went on, 3-7% of frames missed by more
    /// than 2 ms against Unity's 25-47%.
    ///
    /// It waits at the very start of the frame, before Unity reads the clock,
    /// so Time.deltaTime is the paced interval and everything moves by what the
    /// screen is about to show. It sleeps while there is plenty of time left and
    /// spins through the last stretch, because a sleep is only good to a
    /// millisecond or two and a whole frame at 200 FPS is 5 ms.
    ///
    /// It also works with VSync on, which targetFrameRate never did: Unity drops
    /// it entirely whenever vSyncCount is non-zero.
    /// </summary>
    public static class FrameLimiter
    {
        /// <summary>
        /// Time left in hand when the sleeping stops and the spinning starts,
        /// beyond what a sleep has lately been seen to take.
        /// </summary>
        private const double SpinMargin = 0.0005d;

        /// <summary>
        /// The least a one-millisecond sleep is ever assumed to cost. Windows
        /// never wakes a sleeper sooner than asked.
        /// </summary>
        private const double MinimumSleepCost = 0.001d;

        /// <summary>
        /// The slowest recent sleep, in seconds. Sleep(1) takes a millisecond
        /// when the system timer runs fine and up to 15.6 when it runs coarse,
        /// and that is decided outside the game. Learning the figure keeps the
        /// last sleep from overshooting a deadline either way.
        /// </summary>
        private static double sleepCost = 0.002d;

        /// <summary>
        /// How far past its slot a frame may be let go and still count as on
        /// time. See <see cref="AnchorOf"/>.
        ///
        /// Measured on 26 September 2026, capped at 60 in the editor with the bot
        /// fighting the boss: over 13,721 frames the release ran a median 0.2
        /// microseconds past its slot, 0.08 ms at the 99.9th percentile, and past
        /// 0.2 ms only three times, the worst by 0.45 ms. So this separates the
        /// spin loop's ordinary overrun from a real late wake with room to spare.
        /// </summary>
        private const double LateRelease = 0.0002d;

        /// <summary>When the previous frame's interval counts from, in seconds.</summary>
        private static double lastStart;

        /// <summary>Whether the wait is in the player loop.</summary>
        private static bool installed;

        private static int targetFps;

        /// <summary>
        /// The cap in frames per second, or 0 for none.
        ///
        /// The finer system timer is asked for only while there is a cap to hold
        /// and handed back as soon as there is not, so VSync holding the cap by
        /// itself, or no cap at all, leaves the system timer as it found it.
        /// </summary>
        public static int TargetFps
        {
            get => targetFps;
            set
            {
                targetFps = Math.Max(0, value);

                if (installed && targetFps > 0)
                {
                    RaiseTimerResolution();
                }
                else
                {
                    RestoreTimerResolution();
                }
            }
        }

        /// <summary>
        /// How long the cap held this frame at its start, in seconds. It is part
        /// of Time.unscaledDeltaTime without being part of what the frame cost.
        /// </summary>
        public static float LastWaitSeconds { get; private set; }

        /// <summary>
        /// How far past its slot the wait let this frame go, in seconds. A few
        /// microseconds normally; more is a sleep that overran or the thread
        /// losing the CPU. For measuring, nothing reads it in play.
        /// </summary>
        public static float LastLateSeconds { get; private set; }

        /// <summary>
        /// When a frame may start, given when the previous one was let start and
        /// what time it is now.
        ///
        /// An early frame waits for its slot, one interval after the last. A late
        /// one starts at once and the rhythm restarts from it. It does not rush
        /// the next frames short to win the lost time back, because that catch-up
        /// turns one uneven frame into two.
        /// </summary>
        public static double StartOf(double previousStart, double now, double interval)
        {
            double due = previousStart + interval;
            return due > now ? due : now;
        }

        /// <summary>
        /// When the next frame's interval counts from, given when this one was
        /// due and when the wait actually let it go.
        ///
        /// Normally the slot, so the rhythm stays on an exact grid rather than
        /// drifting by the few microseconds a release always runs over. A release
        /// well past its slot - a sleep that overran, or the thread losing the CPU
        /// - counts from the release instead, for the reason a late arrival does
        /// in <see cref="StartOf"/>: counted from the slot, the next frame would
        /// be let go early by however late this one was, and one uneven frame
        /// would become two.
        /// </summary>
        public static double AnchorOf(double due, double released)
        {
            return released - due > LateRelease ? released : due;
        }

        /// <summary>
        /// Whether there is time left for another sleep, given the slowest one
        /// lately. Otherwise the rest of the wait is spun.
        /// </summary>
        public static bool SleepsWith(double left, double cost)
        {
            return left > cost + SpinMargin;
        }

        /// <summary>
        /// The sleep cost to remember after a sleep that took
        /// <paramref name="slept"/> seconds: the slowest sleep lately, forgotten
        /// slowly. One slow wake is remembered long enough to keep the next few
        /// sleeps clear of the deadline.
        /// </summary>
        public static double SleepCostAfter(double cost, double slept)
        {
            return Math.Max(slept, SleepCostForgotten(cost));
        }

        /// <summary>
        /// The sleep cost a little later, with nothing new learned.
        ///
        /// Also run once for every frame that never slept. A sleep cost larger
        /// than a frame's spare time stops the sleeping, and sleeping was the
        /// only place this used to be forgotten, so a single slow wake - the
        /// thread losing the CPU, or the 15.6 ms timer Windows can fall back to
        /// for a hidden window - left the wait spinning a core flat out for the
        /// rest of the session (scan of 28 September 2026). Forgetting a
        /// hundredth each frame, a 15.6 ms wake is let go in about a second at
        /// 120 FPS. If the timer really is still coarse, the next sleep
        /// relearns it.
        /// </summary>
        public static double SleepCostForgotten(double cost)
        {
            return Math.Max(MinimumSleepCost, cost * 0.99d);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            LastWaitSeconds = 0f;
            LastLateSeconds = 0f;
            lastStart = 0d;

            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            RemoveFrom(ref root);

            PlayerLoopSystem wait = new PlayerLoopSystem
            {
                type = typeof(FrameLimiter),
                updateDelegate = Wait
            };

            // First thing in TimeUpdate, ahead of the system that reads the clock
            // for the frame. Anywhere later and deltaTime would miss the wait.
            PlayerLoopSystem[] top = root.subSystemList;
            int timeUpdate = Array.FindIndex(top, s => s.type == typeof(UnityEngine.PlayerLoop.TimeUpdate));

            if (timeUpdate >= 0)
            {
                List<PlayerLoopSystem> inside = new List<PlayerLoopSystem>(
                    top[timeUpdate].subSystemList ?? Array.Empty<PlayerLoopSystem>());
                inside.Insert(0, wait);
                top[timeUpdate].subSystemList = inside.ToArray();
            }
            else
            {
                List<PlayerLoopSystem> all = new List<PlayerLoopSystem>(top);
                all.Insert(0, wait);
                top = all.ToArray();
            }

            root.subSystemList = top;
            PlayerLoop.SetPlayerLoop(root);

            installed = true;

            // Starts uncapped. GraphicsDirector sets the cap once it has read the
            // settings.
            TargetFps = 0;

            // Leaving play mode keeps the domain and the player loop, and the
            // editor has no business being capped.
            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
        }

        /// <summary>
        /// Takes the wait out of the player loop and hands the system timer back.
        /// Safe to call when nothing is installed, and more than once.
        /// </summary>
        private static void Uninstall()
        {
            Application.quitting -= Uninstall;
            installed = false;
            TargetFps = 0;

            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            RemoveFrom(ref root);
            PlayerLoop.SetPlayerLoop(root);
        }

#if UNITY_EDITOR
        /// <summary>Where the cap waits out a recompile. Editor session only.</summary>
        private const string HeldCapKey = "SurvivalChaos.FrameLimiter.HeldCap";

        /// <summary>
        /// A recompile in the middle of play, which the editor does by default
        /// when a script is saved. It throws away every static here, and with
        /// them the note that the system timer was raised - but not the raise,
        /// which belongs to the editor's process and would then never be handed
        /// back, and not the wait, which the player loop would go on calling into
        /// code that no longer exists. So both are undone before the reload, the
        /// cap is parked in SessionState, and afterwards, if the game is still
        /// playing, the wait goes back in and the cap with it.
        /// SubsystemRegistration only runs on entering play, so nothing else
        /// would put it back.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void SurviveRecompiles()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= BeforeRecompile;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += BeforeRecompile;

            int held = UnityEditor.SessionState.GetInt(HeldCapKey, -1);
            UnityEditor.SessionState.EraseInt(HeldCapKey);

            if (held >= 0 && UnityEditor.EditorApplication.isPlaying)
            {
                Install();
                TargetFps = held;
            }
        }

        private static void BeforeRecompile()
        {
            if (installed && UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.SessionState.SetInt(HeldCapKey, TargetFps);
            }

            Uninstall();
        }
#endif

        private static void RemoveFrom(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null)
            {
                return;
            }

            List<PlayerLoopSystem> kept = new List<PlayerLoopSystem>(system.subSystemList.Length);
            foreach (PlayerLoopSystem child in system.subSystemList)
            {
                if (child.type == typeof(FrameLimiter))
                {
                    continue;
                }

                PlayerLoopSystem copy = child;
                RemoveFrom(ref copy);
                kept.Add(copy);
            }

            system.subSystemList = kept.ToArray();
        }

        private static void Wait()
        {
            int fps = targetFps;
            if (fps <= 0)
            {
                LastWaitSeconds = 0f;
                LastLateSeconds = 0f;
                return;
            }

            double arrived = Now();
            double start = StartOf(lastStart, arrived, 1d / fps);
            bool slept = false;

            while (true)
            {
                double left = start - Now();
                if (left <= 0d)
                {
                    break;
                }

                if (SleepsWith(left, sleepCost))
                {
                    double before = Now();
                    Thread.Sleep(1);
                    sleepCost = SleepCostAfter(sleepCost, Now() - before);
                    slept = true;
                }
                else
                {
                    Thread.SpinWait(20);
                }
            }

            if (!slept)
            {
                sleepCost = SleepCostForgotten(sleepCost);
            }

            double released = Now();
            lastStart = AnchorOf(start, released);
            LastWaitSeconds = (float)(released - arrived);
            LastLateSeconds = (float)(released - start);
        }

        private static double Now()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
        }

        // Windows wakes sleepers on a 15.6 ms tick unless a process asks for a
        // finer one. Asking only saves spinning: the learned sleep cost keeps the
        // pacing right either way. Each request that succeeds is matched by
        // exactly one release, which is what timeEndPeriod's contract asks for.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint milliseconds);

        private static bool timerRaised;

        private static void RaiseTimerResolution()
        {
            if (!timerRaised)
            {
                timerRaised = timeBeginPeriod(1) == 0;
            }
        }

        private static void RestoreTimerResolution()
        {
            if (timerRaised)
            {
                timeEndPeriod(1);
                timerRaised = false;
            }
        }
#else
        private static void RaiseTimerResolution()
        {
        }

        private static void RestoreTimerResolution()
        {
        }
#endif
    }
}
