using UnityEngine;

public class BossHpBar : MonoBehaviour
{
    public GameObject HpBar;

    public void showHpBar(){
        if (HpBar == null){
            Debug.LogWarning("BossHpBar has no Hp Bar assigned, so the boss fight has no health bar.", this);
            return;
        }

        HpBar.SetActive(true);
    }
}
