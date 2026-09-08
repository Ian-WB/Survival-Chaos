using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>Explains the boss's current damage rule beside its health bar.</summary>
    public sealed class BossFightHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private TMP_Text[] weakPointLabels;

        private static readonly Color TargetColor = new Color(0.65f, 1f, 0.3f);
        private static readonly Color ExposedColor = new Color(0.65f, 1f, 1f);
        private static readonly Color WreckedColor = new Color(0.68f, 0.74f, 0.76f);

        private BossEmitter observed;
        private BossPhase shownPhase;
        private int shownStanding = -1;
        private bool shownBlocked;
        private float phaseChangedAt;

        private void OnEnable()
        {
            observed = null;
            shownStanding = -1;
            if (phaseLabel != null) { phaseLabel.text = "Leviathan approaching"; }
            if (weakPointLabels == null) { return; }
            foreach (TMP_Text label in weakPointLabels)
            {
                if (label != null) { label.gameObject.SetActive(false); }
            }
        }

        private void LateUpdate()
        {
            if (RunOutcome.RunEnded)
            {
                gameObject.SetActive(false);
                return;
            }

            BossEmitter boss = BossEmitter.Active;
            if (boss == null) { return; }

            int standing = 0;
            var pods = boss.Emplacements;
            for (int i = 0; pods != null && i < pods.Count; i++)
            {
                if (pods[i] != null && !pods[i].Destroyed) { standing++; }
            }

            bool blocked = boss.Phase == BossPhase.Armoured && Time.time - boss.LastBlockedHitTime < 1.5f;
            bool phaseChanged = observed != boss || shownPhase != boss.Phase;
            if (phaseChanged) { phaseChangedAt = Time.time; }

            if (phaseChanged || shownStanding != standing || shownBlocked != blocked)
            {
                observed = boss;
                shownPhase = boss.Phase;
                shownStanding = standing;
                shownBlocked = blocked;
                Refresh(boss, standing, blocked);
            }

            if (phaseLabel != null)
            {
                phaseLabel.fontSize = Time.time - phaseChangedAt < 2f ? 22f : 18f;
            }
        }

        private void Refresh(BossEmitter boss, int standing, bool blocked)
        {
            if (phaseLabel != null)
            {
                switch (boss.Phase)
                {
                    case BossPhase.Armoured:
                        phaseLabel.text = blocked
                            ? "Armour blocked the shot - aim at the green targets"
                            : "Armoured hull - destroy " + standing + " green targets";
                        phaseLabel.color = TargetColor;
                        break;
                    case BossPhase.Exposed:
                        phaseLabel.text = "Hull exposed - fire at the ship";
                        phaseLabel.color = ExposedColor;
                        break;
                    default:
                        phaseLabel.text = "Hull critical - keep firing and dodge the wreckage";
                        phaseLabel.color = new Color(1f, 0.62f, 0.78f);
                        break;
                }
            }

            if (weakPointLabels == null) { return; }
            var pods = boss.Emplacements;
            for (int i = 0; i < weakPointLabels.Length; i++)
            {
                TMP_Text label = weakPointLabels[i];
                if (label == null) { continue; }
                bool exists = pods != null && i < pods.Count && pods[i] != null;
                label.gameObject.SetActive(exists && boss.Phase == BossPhase.Armoured);
                if (!exists) { continue; }
                bool wrecked = pods[i].Destroyed;
                label.text = pods[i].Label + (wrecked ? "  [destroyed]" : "  [target]");
                label.color = wrecked ? WreckedColor : TargetColor;
            }
        }
    }
}
