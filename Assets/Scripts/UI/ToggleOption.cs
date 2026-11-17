using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>The on/off settings on the Controls tab.</summary>
    public enum ToggleOptionKind
    {
        Rumble,
        ControlHints,

        /// <summary>
        /// The HUD tab's CONTEXTUAL HUD: on is ALWAYS ON, off is SMART (the
        /// default), which fades idle parts of the HUD back.
        /// </summary>
        HudContextual
    }

    /// <summary>
    /// An on/off setting on a row, laid out like the graphics settings so the
    /// tabs read as one screen: a label, the value between two arrows, and a
    /// note underneath. Left, right and A all flip it.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Toggle Option")]
    public sealed class ToggleOption : SteppedSetting
    {
        [SerializeField]
        private ToggleOptionKind kind = ToggleOptionKind.Rumble;

        [SerializeField]
        [Tooltip("Shows On or Off.")]
        private TMP_Text value;

        [SerializeField]
        [Tooltip("Shown under the row.")]
        private TMP_Text note;

        [SerializeField]
        [Tooltip("The two steppers. Kept for the row's shape; either one flips the setting.")]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        public ToggleOptionKind Kind => kind;

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Two values, so every step is a flip.</summary>
        public override bool Step(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            Set(!Get());
            Refresh();
            return true;
        }

        private bool Get()
        {
            switch (kind)
            {
                case ToggleOptionKind.Rumble: return Rumble.Enabled;
                case ToggleOptionKind.ControlHints: return Tutorial.HintsOn;
                case ToggleOptionKind.HudContextual: return HudSettings.Mode == HudSettings.Contextual.AlwaysOn;
                default: return false;
            }
        }

        private void Set(bool on)
        {
            switch (kind)
            {
                case ToggleOptionKind.Rumble:
                    Rumble.Enabled = on;

                    // A short pulse, so the player feels what they turned on.
                    if (on)
                    {
                        Rumble.Pulse(Rumble.Strength.Light);
                    }
                    break;

                case ToggleOptionKind.ControlHints:
                    Tutorial.HintsOn = on;
                    break;

                case ToggleOptionKind.HudContextual:
                    HudSettings.Mode = on ? HudSettings.Contextual.AlwaysOn : HudSettings.Contextual.Smart;
                    break;
            }
        }

        private void Refresh()
        {
            bool on = Get();

            if (value != null)
            {
                value.text = ValueText(kind, on);
            }

            if (note != null)
            {
                note.text = Note(on);
                note.gameObject.SetActive(note.text.Length > 0);
            }
        }

        /// <summary>What the row shows for a value: On or Off, or the HUD's two modes.</summary>
        public static string ValueText(ToggleOptionKind kind, bool on)
        {
            if (kind == ToggleOptionKind.HudContextual)
            {
                return on ? "Always On" : "Smart";
            }

            return on ? "On" : "Off";
        }

        private string Note(bool on)
        {
            switch (kind)
            {
                // Short: a note is one line of small capitals under the row.
                case ToggleOptionKind.Rumble:
                    return "On a hit, a dash, a Leviathan part and the end.";

                case ToggleOptionKind.ControlHints:
                    int left = Tutorial.HintRunsLeft;
                    return on
                        ? "Showing in your next " + left + (left == 1 ? " run." : " runs.")
                        : "Turn on to see the control prompts again.";

                case ToggleOptionKind.HudContextual:
                    return on
                        ? "Every part of the HUD stays lit."
                        : "Idle parts of the HUD fade back.";

                default:
                    return string.Empty;
            }
        }
    }
}
