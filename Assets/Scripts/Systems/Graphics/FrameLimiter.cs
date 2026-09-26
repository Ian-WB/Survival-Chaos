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

        /// <summary>When the previous frame was let start, in seconds.</summary>
        private static double lastStart;

        /// <summary>The cap in frames per second, or 0 for none.</summary>
        public static int TargetFps { get; set; }

        /// <summary>
        /// How long the cap held this frame at its start, in seconds. It is part
        /// of Time.unscaledDeltaTime without being part of what the frame cost.
        /// </summary>
        public static float LastWaitSeconds { get; private set; }

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            TargetFps = 0;
            LastWaitSeconds = 0f;
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

            RaiseTimerResolution();

            // Leaving play mode keeps the domain and the player loop, and the
            // editor has no business being capped.
            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
        }

        private static void Uninstall()
        {
            Application.quitting -= Uninstall;
            TargetFps = 0;

            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            RemoveFrom(ref root);
            PlayerLoop.SetPlayerLoop(root);

            RestoreTimerResolution();
        }

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
            int fps = TargetFps;
            if (fps <= 0)
            {
                LastWaitSeconds = 0f;
                return;
            }

            double arrived = Now();
            double start = StartOf(lastStart, arrived, 1d / fps);

            while (true)
            {
                double left = start - Now();
                if (left <= 0d)
                {
                    break;
                }

                if (left > sleepCost + SpinMargin)
                {
                    double before = Now();
                    Thread.Sleep(1);

                    // The slowest sleep lately, forgotten slowly. One slow wake is
                    // remembered long enough to keep the next few sleeps clear of
                    // the deadline.
                    sleepCost = Math.Max(MinimumSleepCost, Math.Max(Now() - before, sleepCost * 0.99d));
                }
                else
                {
                    Thread.SpinWait(20);
                }
            }

            lastStart = start;
            LastWaitSeconds = (float)(Now() - arrived);
        }

        private static double Now()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
        }

        // Windows wakes sleepers on a 15.6 ms tick unless a process asks for a
        // finer one. Asking only saves spinning: the learned sleep cost keeps the
        // pacing right either way.
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
