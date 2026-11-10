using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class TitleSplashDirectorTests
    {
        [TearDown]
        public void TearDown()
        {
            TitleSplashDirector.Enabled = true;
        }

        [Test]
        public void Enabled_CanBeToggled()
        {
            TitleSplashDirector.Enabled = false;
            Assert.IsFalse(TitleSplashDirector.Enabled);

            TitleSplashDirector.Enabled = true;
            Assert.IsTrue(TitleSplashDirector.Enabled);
        }

        [Test]
        public void Enabled_IsTrueByDefault()
        {
            Assert.IsTrue(TitleSplashDirector.Enabled);
        }

        [Test]
        public void TitleSplash_TriggerMenuReveal_DoesNotCrashOutsidePlay()
        {
            var go = new GameObject("TestSplashDirector");
            try
            {
                var director = go.AddComponent<TitleSplashDirector>();
                Assert.DoesNotThrow(() => director.TriggerMenuReveal());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
