using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The ship's frame, in order: the dash settles, both copies of
    /// PlayerMovement move - the ship's and the Main Camera's - and the height
    /// clamp has the last word.
    ///
    /// Unity orders nothing between scripts unless it is told to. Until 25
    /// September 2026 it was not told, and a dash that started between the two
    /// PlayerMovements would have moved one a frame further than the other, for
    /// good. This pins what it is told now.
    /// </summary>
    public class UpdateOrderTests
    {
        [Test]
        public void TheDashSettlesBeforeAnythingMoves()
        {
            Assert.That(Order(typeof(PlayerDash)), Is.LessThan(Order(typeof(PlayerMovement))));
        }

        [Test]
        public void TheHeightClampComesAfterTheMove()
        {
            Assert.That(Order(typeof(ApplyBounds)), Is.GreaterThan(Order(typeof(PlayerMovement))));
        }

        /// <summary>
        /// A value set in Project Settings > Script Execution Order replaces the
        /// attribute, so that is read first: one set there by hand would undo the
        /// attributes without touching them.
        /// </summary>
        private static int Order(Type type)
        {
            foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script == null || script.GetClass() != type)
                {
                    continue;
                }

                int configured = MonoImporter.GetExecutionOrder(script);
                if (configured != 0)
                {
                    return configured;
                }

                break;
            }

            var attribute = (DefaultExecutionOrder)Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder));
            return attribute != null ? attribute.order : 0;
        }
    }
}
