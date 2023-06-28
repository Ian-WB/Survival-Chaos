using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSelect : MonoBehaviour
{

    private int shotCount = 1;
    private int attackSpeedCount = 3;
    private List<System.Action> skillsList = new List<System.Action>();
    public Player player;
    public HealthBar hpBar;
    public TextMeshProUGUI skillText;
    public GameObject skillTextObject;
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("teste1");
        skillsList.Add(MoreShots);
        skillsList.Add(MoreMaxHp);
        attackSpeedCount = 3;
        shotCount = 1;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F7)){
            PickSkill();
        }
        if(Input.GetKeyDown(KeyCode.F8)){
        }
    }

    public void PickSkill(){
        if(skillsList.Count > 0){
            skillsList[Random.Range(0, skillsList.Count)]();
        } else {
            Heal();
        }
        
    }

    void MoreShots(){
        Debug.Log("teste");
        if(shotCount == 1){
            player.tiroDuplo = true;
            shotCount++;
            StartCoroutine(showText("Double Shot!"));
        } else if(shotCount == 2){
            player.tiroDuplo = false;
            player.tiroTriplo = true;
            shotCount++;
            StartCoroutine(showText("Triple Shot!"));
        } else if(shotCount == 3){
            player.tiroTriplo = false;
            player.sextuplo = true;
            shotCount++;
            StartCoroutine(showText("SexTUPLO Shot!"));
        } else if(shotCount == 4){
            player.tiroTriplo = false;
            player.tiroAtras = true;
            player.sextuplo = false;
            skillsList.Remove(MoreShots);
            StartCoroutine(showText("Back Shot!"));
        } 
    }

    void MoreMaxHp(){
        player.addMaxHp(20);
        skillsList.Remove(MoreMaxHp);
        StartCoroutine(showText("Max HP Increased!"));
    }

    void Heal(){
        player.addHp(5);
        StartCoroutine(showText("Heal!"));
    }
    
    void MoreAttackSpeed(){
        if(attackSpeedCount > 0){
            player.addAttackSpeed();
            StartCoroutine(showText("Attack Speed Increased!"));
            attackSpeedCount--;
            if(attackSpeedCount == 0){
                skillsList.Remove(MoreAttackSpeed);
            }
        }
    }

    IEnumerator showText(string skillName)
    {
        skillTextObject.SetActive(true);
        skillText.text = "Level Up!\n" + skillName;
        
        yield return new WaitForSeconds(3);
        skillTextObject.SetActive(false);
    }
}