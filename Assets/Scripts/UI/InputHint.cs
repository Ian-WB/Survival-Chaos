using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A label that names a button in whichever of the two the player last
    /// used: "LB" to someone holding a pad, "Q" to someone at the keys.
    /// Checked every frame it is on screen, so picking up the pad mid-menu
    /// changes it on the spot.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Input Hint")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class InputHint : MonoBehaviour
    {
        [SerializeField]
        private string keyboard = "Q";

        [SerializeField]
        private string pad = "LB";

        private TMP_Text text;
        private int shown = -1;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            shown = -1;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            int wanted = GameInput.PadLastUsed ? 1 : 0;
            if (wanted == shown || text == null)
            {
                return;
            }

            shown = wanted;
            text.text = wanted == 1 ? pad : keyboard;
        }
    }
}
