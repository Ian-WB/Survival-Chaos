using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The pause screen's card of what is waiting on the ring (17 November 2026
    /// redesign, block 8): each offer's skill with its tier in gold, on a dark
    /// card with a gold edge at the right of the screen. With the run HUD and
    /// the offers' own floating labels hidden behind the pause menu, this is
    /// where the player reads them. No offers, no card.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Offers Card")]
    [DisallowMultipleComponent]
    public sealed class OffersCard : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The card itself, shown only while there are offers.")]
        private GameObject card;

        [SerializeField]
        [Tooltip("One line per offer.")]
        private TMP_Text list;

        [SerializeField]
        [Tooltip("The card's height with no rows: header and padding.")]
        private float baseHeight = 64f;

        [SerializeField]
        [Tooltip("Height each offer adds.")]
        private float rowHeight = 40f;

        private readonly List<string> captions = new List<string>();

        private void OnEnable()
        {
            Fill();
        }

        private void Fill()
        {
            captions.Clear();
            PickupLabelBoard board = PickupLabelBoard.Instance;
            if (board != null)
            {
                foreach (PickupLabel label in board.Attached)
                {
                    if (label != null && label.Live && !string.IsNullOrEmpty(label.Caption))
                    {
                        captions.Add(label.Caption);
                    }
                }
            }

            if (card != null)
            {
                card.SetActive(captions.Count > 0);
            }

            if (captions.Count == 0 || list == null)
            {
                return;
            }

            list.text = Compose(captions);

            if (card != null && card.transform is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, baseHeight + rowHeight * captions.Count);
            }
        }

        /// <summary>
        /// The rows: each name on a dark plate, its tier (a trailing Roman
        /// numeral) in gold at the right.
        /// </summary>
        public static string Compose(IList<string> offers)
        {
            var into = new StringBuilder("<line-height=" + LineHeight + ">");
            for (int i = 0; i < offers.Count; i++)
            {
                Split(offers[i], out string name, out string tier);
                into.Append("<mark=#5CB8CC1C padding=\"14,14,8,8\"><color=#FFD140>|</color> ")
                    .Append(name)
                    .Append("<pos=88%><color=#FFD140>").Append(tier).Append("</color><pos=100%></mark>");
                if (i < offers.Count - 1)
                {
                    into.Append('\n');
                }
            }

            return into.ToString();
        }

        private const string LineHeight = "40";

        /// <summary>
        /// "Attack Speed III" to "Attack Speed" and "III". A caption with no
        /// numeral at the end keeps its whole name and no tier. Line breaks and
        /// rich text in a caption are flattened.
        /// </summary>
        public static void Split(string caption, out string name, out string tier)
        {
            string flat = System.Text.RegularExpressions.Regex.Replace(caption ?? string.Empty, "<[^>]*>", string.Empty)
                .Replace('\n', ' ').Replace('\r', ' ').Trim();
            int space = flat.LastIndexOf(' ');
            string last = space >= 0 ? flat.Substring(space + 1) : string.Empty;

            if (last.Length > 0 && IsNumeral(last))
            {
                name = flat.Substring(0, space).Trim();
                tier = last;
            }
            else
            {
                name = flat;
                tier = string.Empty;
            }
        }

        private static bool IsNumeral(string word)
        {
            foreach (char c in word)
            {
                if (c != 'I' && c != 'V' && c != 'X')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
