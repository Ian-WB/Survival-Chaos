using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class CinematicTrailerDirectorTests
    {
        [Test]
        public void TrailerDirector_IsNotRunningByDefault()
        {
            Assert.IsFalse(CinematicTrailerDirector.IsRunning);
        }

        [Test]
        public void TrailerDirector_StopTrailer_CanBeCalledSafelyWhenNotRunning()
        {
            Assert.DoesNotThrow(() => CinematicTrailerDirector.StopTrailer());
            Assert.IsFalse(CinematicTrailerDirector.IsRunning);
        }

        /// <summary>
        /// The trailer hotkeys make the ship invulnerable, call the boss and slow time. They may
        /// exist only where the debug menu does, never in a release build.
        /// </summary>
        [TestCase("CinematicTrailerDirector.cs")]
        [TestCase("CinematicTrailerCam.cs")]
        public void TrailerTooling_IsCompiledOnlyWithTheDebugMenu(string file)
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts", "Gameplay", "Arena", file));

            StringAssert.StartsWith(
                "#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU",
                source.TrimStart('﻿'));
        }

        [Test]
        public void TrailerEndCard_NamesTheStoreTheGameIsOn()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "Scripts", "Gameplay", "Arena", "CinematicTrailerDirector.cs"));

            StringAssert.DoesNotContain("STEAM", source);
            StringAssert.Contains("ITCH.IO", source);
        }
    }
}
