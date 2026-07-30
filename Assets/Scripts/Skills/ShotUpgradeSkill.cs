using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Advances the player's shot pattern. Four picks walk the whole progression:
    /// double, triple, sextuple, then shots to the rear.
    /// </summary>
    [CreateAssetMenu(fileName = "ShotUpgrade", menuName = "Survival Chaos/Skills/Shot Upgrade")]
    public sealed class ShotUpgradeSkill : SkillDefinition
    {
        [SerializeField]
        [Tooltip("Banner text per stage, in pick order.")]
        private string[] stageNames =
        {
            "Double Shot!",
            "Triple Shot!",
            "SexTUPLO Shot!",
            "Back Shot!"
        };

        public override string GetDisplayName(int picksTaken)
        {
            if (stageNames == null || stageNames.Length == 0)
            {
                return base.GetDisplayName(picksTaken);
            }

            int index = Mathf.Clamp(picksTaken - 1, 0, stageNames.Length - 1);
            return stageNames[index];
        }

        public override void Apply(ISkillTarget target)
        {
            target.UpgradeShotPattern();
        }
    }
}
