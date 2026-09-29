using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Entering play mode keeps the domain since 29 September 2026, which saves
    /// about four seconds each time. The price is that statics are no longer
    /// wiped between play sessions: whatever the last one left in a static, the
    /// next one starts with, and nothing says so. A run beginning at the last
    /// run's speed, or input still driven by a fake, looks like a game bug.
    ///
    /// So every class that holds static state resets it in a SubsystemRegistration
    /// method, the one hook that runs on entering play either way. Where some of
    /// it must outlive the session, as FSR 3's native contexts do, that method is
    /// where to say so. This finds a class holding state with no such method.
    /// </summary>
    public class PlayModeStaticsTests
    {
        private const BindingFlags Statics =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [Test]
        public void EveryClassWithStaticStateResetsItOnEnteringPlay()
        {
            var missing = new List<string>();

            foreach (Type type in typeof(GameInput).Assembly.GetTypes())
            {
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    continue;
                }

                string[] state = type.GetFields(Statics).Where(IsState).Select(field => field.Name).ToArray();

                if (state.Length > 0 && !ResetsOnEnteringPlay(type))
                {
                    missing.Add(type.FullName + " (" + string.Join(", ", state) + ")");
                }
            }

            Assert.IsEmpty(missing,
                "These hold static state that would carry from one play session into the next. " +
                "Give each a [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] " +
                "method that resets it:\n" + string.Join("\n", missing));
        }

        [Test]
        public void TheCheckSeesStateItShouldAndSkipsWhatItShould()
        {
            // The rule is only as good as its idea of state, so pin it on classes
            // whose fields are known.
            Assert.IsTrue(typeof(GameInput).GetFields(Statics).Any(IsState), "a plain static field");
            Assert.IsTrue(typeof(ShootScript).GetFields(Statics).Any(f => f.Name == "live" && IsState(f)),
                "a readonly list that fills up in play");
            Assert.IsFalse(typeof(DisplayOptions).GetFields(Statics).Any(IsState),
                "readonly tables of names and numbers are configuration, not state");
            Assert.IsTrue(ResetsOnEnteringPlay(typeof(GameInput)));
        }

        /// <summary>
        /// Anything assignable, auto-property backing fields and events included,
        /// plus a readonly collection, which is still filled and emptied. A
        /// readonly array or string is a table of constants.
        /// </summary>
        private static bool IsState(FieldInfo field)
        {
            if (field.IsLiteral)
            {
                return false;
            }

            if (!field.IsInitOnly)
            {
                return true;
            }

            Type type = field.FieldType;
            return !type.IsArray && type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static bool ResetsOnEnteringPlay(Type type)
        {
            return type.GetMethods(Statics).Any(method =>
                method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>()?.loadType ==
                RuntimeInitializeLoadType.SubsystemRegistration);
        }
    }
}
