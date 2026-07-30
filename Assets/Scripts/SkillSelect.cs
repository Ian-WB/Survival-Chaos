using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SurvivalChaos;

public class SkillSelect : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Skills this run can draw from. Include an unlimited skill (Heal) so the pool never runs dry.")]
    private List<SkillDefinition> skills = new List<SkillDefinition>();

    public Player player;
    public TextMeshProUGUI skillText;
    public GameObject skillTextObject;

    private SkillPool pool;

    void Start()
    {
        pool = new SkillPool(skills);
    }

    void Update()
    {
        if(GameInput.DebugLevelUpPressed){
            PickSkill();
        }
    }

    public void PickSkill(){
        if(pool == null){
            pool = new SkillPool(skills);
        }

        SkillDefinition skill = pool.Next();
        if(skill == null){
            return;
        }

        skill.Apply(player);
        StartCoroutine(showText(skill.GetDisplayName(pool.PicksTaken(skill))));
    }

    IEnumerator showText(string skillName)
    {
        skillTextObject.SetActive(true);
        skillText.text = "Level Up!\n" + skillName;

        yield return new WaitForSeconds(3);
        skillTextObject.SetActive(false);
    }
}
