using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Where keyboard and pad focus lands when a screen opens, and what the
    /// screen's sliders do with left and right.
    ///
    /// Placing the focus needs a running EventSystem, which edit mode does not
    /// have, so this covers the rules the placing follows rather than the placing.
    /// </summary>
    public class MenuFocusTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Screen", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private T Control<T>(string name) where T : Selectable
        {
            GameObject control = new GameObject(name, typeof(RectTransform));
            control.transform.SetParent(root.transform, false);
            return control.AddComponent<T>();
        }

        private static GameObject DefaultOf(MenuScreen screen)
        {
            return (GameObject)typeof(MenuScreen).GetMethod("DefaultControl", Private).Invoke(screen, null);
        }

        [Test]
        public void FocusLandsOnTheFirstLiveControl()
        {
            Control<Button>("Greyed out").interactable = false;
            Control<Button>("Hidden").gameObject.SetActive(false);
            Button first = Control<Button>("First live");
            Control<Button>("Second live");

            MenuScreen screen = root.AddComponent<MenuScreen>();

            Assert.That(DefaultOf(screen), Is.SameAs(first.gameObject));
        }

        [Test]
        public void AnAuthoredFirstControlWinsOverTheHierarchy()
        {
            Control<Button>("Top");
            Button chosen = Control<Button>("Chosen");

            MenuScreen screen = root.AddComponent<MenuScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("firstSelected").objectReferenceValue = chosen;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(DefaultOf(screen), Is.SameAs(chosen.gameObject));
        }

        [Test]
        public void AnAuthoredFirstControlThatIsGreyedOutIsPassedOver()
        {
            Button top = Control<Button>("Top");
            Button chosen = Control<Button>("Chosen");
            chosen.interactable = false;

            MenuScreen screen = root.AddComponent<MenuScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("firstSelected").objectReferenceValue = chosen;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(DefaultOf(screen), Is.SameAs(top.gameObject));
        }

        [Test]
        public void HorizontalSlidersTakeLeftAndRight_AndKeepUpAndDownForTheRows()
        {
            Slider bar = Control<Slider>("Volume");
            bar.direction = Slider.Direction.LeftToRight;

            Slider upright = Control<Slider>("Upright");
            upright.direction = Slider.Direction.BottomToTop;

            Slider pinned = Control<Slider>("Explicit");
            pinned.direction = Slider.Direction.LeftToRight;
            pinned.navigation = new Navigation { mode = Navigation.Mode.Explicit };

            MenuScreen screen = root.AddComponent<MenuScreen>();
            typeof(MenuScreen).GetMethod("LetSlidersTakeLeftAndRight", Private).Invoke(screen, null);

            Assert.That(bar.navigation.mode, Is.EqualTo(Navigation.Mode.Vertical));
            Assert.That(upright.navigation.mode, Is.EqualTo(Navigation.Mode.Automatic),
                "a vertical slider takes up and down, and needs left and right to leave it");
            Assert.That(pinned.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit),
                "navigation someone wired by hand is theirs");
        }
    }
}
