using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public void AScreenShownFresh_ForgetsWhereItWasLeft()
        {
            // "Abandon this run?" is asked this way: a Yes backed out of must
            // not be what is selected the next time the question opens.
            Control<Button>("No");
            Button yes = Control<Button>("Yes");

            MenuScreen screen = root.AddComponent<MenuScreen>();
            FieldInfo left = typeof(MenuScreen).GetField("lastSelected", Private);
            left.SetValue(screen, yes.gameObject);

            screen.Show();
            Assert.That(left.GetValue(screen), Is.SameAs(yes.gameObject), "an ordinary screen resumes");

            screen.ShowFresh();
            Assert.That(left.GetValue(screen), Is.Null);
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

        private EventSystem Events()
        {
            GameObject host = new GameObject("EventSystem");
            host.transform.SetParent(root.transform, false);
            return host.AddComponent<EventSystem>();
        }

        private static PointerEventData Pointer(EventSystem events, Vector2 delta)
        {
            return new PointerEventData(events) { delta = delta };
        }

        /// <summary>Edit mode sends no Awake, and Awake is where these find their button.</summary>
        private static T Woken<T>(T component) where T : Component
        {
            typeof(T).GetMethod("Awake", Private).Invoke(component, null);
            return component;
        }

        /// <summary>
        /// A screen that opens under a still cursor keeps its own first choice,
        /// and a nudge of the mouse inside that same button then takes focus -
        /// which the pointer's enter alone never did, because it had already
        /// been sent while nothing moved.
        /// </summary>
        [Test]
        public void MovingInsideAButton_SelectsIt_AndAStillPointerDoesNot()
        {
            EventSystem events = Events();
            Button quit = Control<Button>("Quit");
            HoloButtonHighlight button = Woken(quit.gameObject.AddComponent<HoloButtonHighlight>());
            Button entry = Control<Button>("Entry");
            HoloMenuEntry menuEntry = Woken(entry.gameObject.AddComponent<HoloMenuEntry>());

            button.OnPointerMove(Pointer(events, Vector2.zero));
            menuEntry.OnPointerMove(Pointer(events, Vector2.zero));
            Assert.That(events.currentSelectedGameObject, Is.Null, "a still pointer takes nothing");

            button.OnPointerMove(Pointer(events, new Vector2(2f, 0f)));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(quit.gameObject));

            menuEntry.OnPointerMove(Pointer(events, new Vector2(0f, 1f)));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(entry.gameObject));
        }

        [Test]
        public void MovingInsideAGreyedOutButton_TakesNothing()
        {
            EventSystem events = Events();
            Button greyed = Control<Button>("Greyed out");
            greyed.interactable = false;
            HoloMenuEntry entry = Woken(greyed.gameObject.AddComponent<HoloMenuEntry>());

            entry.OnPointerMove(Pointer(events, new Vector2(2f, 0f)));

            Assert.That(events.currentSelectedGameObject, Is.Null);
        }

        /// <summary>
        /// A setting's arrows and the option tabs are out of navigation, so the
        /// pad passes over them. A screen must never open on one, or remember
        /// one as the place to come back to: from there no direction leads
        /// anywhere.
        /// </summary>
        [Test]
        public void AControlOutOfNavigation_IsNeverWhereFocusLands()
        {
            Button tab = Control<Button>("Tab");
            tab.navigation = new Navigation { mode = Navigation.Mode.None };
            Button row = Control<Button>("Row");

            MenuScreen screen = root.AddComponent<MenuScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("firstSelected").objectReferenceValue = tab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(DefaultOf(screen), Is.SameAs(row.gameObject),
                "an authored first control the keys cannot leave is passed over too");

            bool usable = (bool)typeof(MenuScreen).GetMethod("Usable", Private)
                .Invoke(screen, new object[] { tab.gameObject });
            Assert.That(usable, Is.False, "so it is not somewhere to come back to either");
        }

        private OptionRow Row(string name)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(root.transform, false);
            return host.AddComponent<OptionRow>();
        }

        private static AxisEventData Move(EventSystem events, MoveDirection direction)
        {
            return new AxisEventData(events) { moveDir = direction };
        }

        /// <summary>
        /// Left and right belong to the value, so they must not walk off the
        /// row the way they did when the arrows were the controls. Up and down
        /// still move between rows.
        /// </summary>
        [Test]
        public void ARow_KeepsLeftAndRight_AndPassesUpAndDownOn()
        {
            EventSystem events = Events();
            OptionRow top = Row("Top");
            OptionRow below = Row("Below");
            Button beside = Control<Button>("Beside");

            top.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = below,
                selectOnLeft = beside,
                selectOnRight = beside
            };
            events.SetSelectedGameObject(top.gameObject);

            AxisEventData left = Move(events, MoveDirection.Left);
            top.OnMove(left);
            AxisEventData right = Move(events, MoveDirection.Right);
            top.OnMove(right);

            Assert.That(events.currentSelectedGameObject, Is.SameAs(top.gameObject),
                "sideways changes the setting, even with something wired beside it");
            Assert.That(left.used && right.used, Is.True);

            top.OnMove(Move(events, MoveDirection.Down));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(below.gameObject));
        }

        [Test]
        public void ARowWithNothingToChange_StepsNowhere()
        {
            Assert.That(Row("Unwired").Step(1), Is.False,
                "a row that changed nothing must not click as if it had");
        }

        private OptionsTabs[] Tabs(int count)
        {
            MenuScreen[] screens = new MenuScreen[count];
            for (int i = 0; i < count; i++)
            {
                GameObject screen = new GameObject("Tab " + i, typeof(RectTransform));
                screen.transform.SetParent(root.transform, false);
                screen.SetActive(false);
                screens[i] = screen.AddComponent<MenuScreen>();
            }

            OptionsTabs[] tabs = new OptionsTabs[count];
            for (int i = 0; i < count; i++)
            {
                tabs[i] = screens[i].gameObject.AddComponent<OptionsTabs>();
                var serialized = new SerializedObject(tabs[i]);
                SerializedProperty list = serialized.FindProperty("tabs");
                list.arraySize = count;
                for (int j = 0; j < count; j++)
                {
                    list.GetArrayElementAtIndex(j).objectReferenceValue = screens[j];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return tabs;
        }

        /// <summary>
        /// The shoulders go round: RB from the last tab comes back to the first,
        /// and LB from the first goes to the last.
        /// </summary>
        [Test]
        public void TheTabsTurnBothWays_AndWrapAtTheEnds()
        {
            OptionsTabs[] tabs = Tabs(3);

            tabs[0].Turn(1);
            Assert.That(tabs[1].gameObject.activeSelf, Is.True);

            tabs[2].Turn(1);
            Assert.That(tabs[0].gameObject.activeSelf, Is.True, "RB from the last tab wraps to the first");

            tabs[2].gameObject.SetActive(false);
            tabs[0].Turn(-1);
            Assert.That(tabs[2].gameObject.activeSelf, Is.True, "LB from the first tab wraps to the last");
        }
    }
}
