using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillSelect : MonoBehaviour
{

    public List<GameObject> Skills = new List<GameObject>();
    public GameObject healSkill;
    private int objCount;
    private GameObject pickedSkill = null;
    public GameObject slot1;
    public GameObject slot2;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space)){
            PickSkill();
        }
    }

    void PickSkill(){
        objCount=0;
        while(objCount < Skills.Count){
            Skills[objCount].SetActive(false);
            objCount+=1;
        }
        if (pickedSkill != null){
            Skills.Remove(pickedSkill);
        }
        if(Skills.Count > 0){
            pickedSkill = Skills[Random.Range(0, Skills.Count)];
            slot1 = GameObject.Find("SkillSlot1");
            pickedSkill.SetActive(true);
        } else {
            healSkill.SetActive(true);
        }
        
    }
}
