using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The HUD options tab's three settings (17 November 2026 redesign): scale,
    /// opacity and contextual fading. Saved the way the other options are,
    /// straight to PlayerPrefs with SettingsStore flushing shortly after.
    /// </summary>
    public static class HudSettings
    {
        private const string ScaleKey = "SurvivalChaos.Hud.Scale";
        private const string OpacityKey = "SurvivalChaos.Hud.Opacity";
        private const string ContextualKey = "SurvivalChaos.Hud.Contextual";

        public const int MinScalePercent = 70;
        public const int MaxScalePercent = 130;
        public const int MinOpacityPercent = 40;
        public const int MaxOpacityPercent = 100;

        /// <summary>
        /// SMART fades what is idle; ALWAYS ON keeps everything at least at
        /// <see cref="AlwaysOnFloor"/>.
        /// </summary>
        public enum Contextual { Smart = 0, AlwaysOn = 1 }

        /// <summary>The least anything shows under ALWAYS ON.</summary>
        public const float AlwaysOnFloor = 0.85f;

        /// <summary>Raised whenever one of the three changes.</summary>
        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;

        /// <summary>HUD scale in percent, 70 to 130, default 100.</summary>
        public static int ScalePercent
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ScaleKey, 100), MinScalePercent, MaxScalePercent);
            set => Write(ScaleKey, Mathf.Clamp(value, MinScalePercent, MaxScalePercent));
        }

        /// <summary>HUD opacity in percent, 40 to 100, default 100.</summary>
        public static int OpacityPercent
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(OpacityKey, 100), MinOpacityPercent, MaxOpacityPercent);
            set => Write(OpacityKey, Mathf.Clamp(value, MinOpacityPercent, MaxOpacityPercent));
        }

        public static Contextual Mode
        {
            get => PlayerPrefs.GetInt(ContextualKey, 0) == 1 ? Contextual.AlwaysOn : Contextual.Smart;
            set => Write(ContextualKey, value == Contextual.AlwaysOn ? 1 : 0);
        }

        public static float Scale => ScalePercent / 100f;

        public static float Opacity => OpacityPercent / 100f;

        /// <summary>
        /// The resting strength a faded element actually rests at: its own
        /// under SMART, never below the floor under ALWAYS ON.
        /// </summary>
        public static float Rest(float smartRest, Contextual mode)
        {
            return mode == Contextual.AlwaysOn ? Mathf.Max(smartRest, AlwaysOnFloor) : smartRest;
        }

        private static void Write(string key, int value)
        {
            if (PlayerPrefs.GetInt(key, int.MinValue) == value)
            {
                return;
            }

            PlayerPrefs.SetInt(key, value);
            SettingsStore.MarkDirty();
            Changed?.Invoke();
        }
    }
}
