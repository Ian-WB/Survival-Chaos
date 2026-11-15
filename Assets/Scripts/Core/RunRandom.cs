using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// The run's own random numbers: where each enemy arrives, which way an
    /// offer fans out, which upgrades a level-up draws.
    ///
    /// These came from UnityEngine.Random, which the lightning, the fireflies
    /// and every sound's pitch draw from too, as often as the frame rate has
    /// them. So no two runs could be given the same arrivals, and a balance
    /// change could only be judged against runs that differed in everything
    /// (15 November 2026, for the playtest bot's batches).
    ///
    /// Each caller draws from its own numbered stream, so one stream's draws
    /// do not depend on how many another has made: a wave stream's fortieth
    /// enemy takes the same numbers in every run on the same seed, whichever
    /// frame it lands on and whatever was picked up before it. What a seed
    /// does not fix is the run itself. Where the ship is decides which side
    /// of the ring an arrival keeps clear of, and frame timing decides the
    /// rest, so two runs on one seed meet the same arrivals, not the same
    /// fight.
    ///
    /// A new seed is taken at every scene load, like the run's other statics.
    /// Nothing may draw before Start: sceneLoaded comes after Awake and
    /// OnEnable, and a draw made there would come from the run before.
    /// </summary>
    public static class RunRandom
    {
        /// <summary>The stream upgrades are drawn from. Wave streams use their index, 0 and up.</summary>
        public const int Skills = -1;

        /// <summary>The stream that decides which way round an offer fans out.</summary>
        public const int Offers = -2;

        private static readonly Dictionary<int, ulong> states = new Dictionary<int, ulong>();
        private static int? requested;

        /// <summary>This run's seed.</summary>
        public static int Seed { get; private set; }

        /// <summary>Gives the next run, and only that one, this seed.</summary>
        public static void SeedNext(int seed)
        {
            requested = seed;
        }

        /// <summary>Starts every stream again from <paramref name="seed"/>.</summary>
        public static void Reseed(int seed)
        {
            Seed = seed;
            states.Clear();
        }

        /// <summary>A number from 0 up to, never reaching, 1.</summary>
        public static float Value(int stream)
        {
            // The top 24 bits, which is every bit a float between 0 and 1 holds.
            return (Next(stream) >> 40) * (1f / (1 << 24));
        }

        public static float Range(int stream, float min, float max)
        {
            return min + (max - min) * Value(stream);
        }

        /// <summary>An index from 0 to <paramref name="count"/> - 1. 0 when there is nothing to choose from.</summary>
        public static int Index(int stream, int count)
        {
            return count <= 0 ? 0 : (int)((Next(stream) >> 33) % (ulong)count);
        }

        /// <summary>
        /// SplitMix64: one addition and three multiplies a draw, and the same
        /// numbers on every machine and runtime, which System.Random does not
        /// promise between versions.
        /// </summary>
        private static ulong Next(int stream)
        {
            if (!states.TryGetValue(stream, out ulong state))
            {
                // Mixed once so that neighbouring seeds and neighbouring
                // streams do not start out as neighbours.
                state = Mix(((ulong)(uint)Seed << 32) | (uint)stream);
            }

            state += 0x9E3779B97F4A7C15UL;
            states[stream] = state;
            return Mix(state);
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static void NewRun()
        {
            Reseed(requested ?? Guid.NewGuid().GetHashCode());
            requested = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            NewRun();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // On load, like RunStats: a scene load is the only way a run starts.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            NewRun();
        }
    }
}
