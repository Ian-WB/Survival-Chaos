using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The start of the game: the splash, and the loading screen every scene
    /// change goes through. Both fail without saying so - a scene loads fine
    /// with no loading screen, and the splash draws around a logo that is not
    /// there - so these read the assets instead.
    /// </summary>
    public class LoadingScreenTests
    {
        private static SerializedObject Prefab()
        {
            LoadingScreen screen = Resources.Load<LoadingScreen>(LoadingScreen.ResourcePath);
            Assert.IsNotNull(screen, "no LoadingScreen prefab in Resources; run Survival Chaos > UI > Build Loading Screen");
            return new SerializedObject(screen);
        }

        [Test]
        public void PrefabIsInResourcesWithEveryPartWired()
        {
            SerializedObject screen = Prefab();

            foreach (string field in new[] { "group", "bar", "status" })
            {
                Assert.IsNotNull(screen.FindProperty(field).objectReferenceValue, field + " is not wired");
            }

            Image bar = (Image)screen.FindProperty("bar").objectReferenceValue;
            Assert.IsNotNull(bar.GetComponent<HoloBar>(), "the bar would not draw its progress");
            Assert.AreEqual("Survival Chaos/Holo Bar", bar.material.shader.name);

            Object status = screen.FindProperty("status").objectReferenceValue;
            Object font = new SerializedObject(status).FindProperty("m_fontAsset").objectReferenceValue;
            Assert.IsNotNull(font);
            StringAssert.Contains("Chakra", font.name, "the loading screen should use the interface font");
        }

        [Test]
        public void ItCoversEverythingAndTakesTheClicks()
        {
            GameObject root = ((Component)Prefab().targetObject).gameObject;

            Canvas canvas = root.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.GreaterOrEqual(canvas.sortingOrder, 1000, "the pause and death screens would draw over it");
            Assert.IsNotNull(root.GetComponent<GraphicRaycaster>());
            Assert.IsTrue(root.GetComponent<CanvasGroup>().blocksRaycasts);

            Image backdrop = root.transform.Find("Backdrop").GetComponent<Image>();
            Assert.AreEqual(1f, backdrop.color.a, "a see-through backdrop shows the frozen scene behind");
        }

        [Test]
        public void EverySplashLogoIsARealSprite()
        {
            // On 24 Aug 2026 the jam's university logo was deleted as unused,
            // but Project Settings still listed it. The splash kept its two
            // seconds and drew "Made with Unity" below an empty slot.
            foreach (PlayerSettings.SplashScreenLogo logo in PlayerSettings.SplashScreen.logos)
            {
                Assert.IsNotNull(logo.logo, "a splash logo points at a sprite that no longer exists");
            }
        }
    }
}
