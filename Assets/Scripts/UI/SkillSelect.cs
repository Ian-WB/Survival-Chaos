using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SurvivalChaos;

/// <summary>
/// Owns the run's skill pool and turns a level-up into an offer on the ring.
///
/// This used to apply a random skill the instant the bar filled. That is the
/// single biggest reason the player felt strong from the first minute: every
/// upgrade was free, arrived without being asked for, and cost no attention at
/// a moment when attention is the only currency the game charges. Routing the
/// same rewards through pickups keeps the power curve and puts a price on it.
/// </summary>
public class SkillSelect : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Skills this run can draw from. Include an unlimited skill (Heal) so the pool never runs dry.")]
    private List<SkillDefinition> skills = new List<SkillDefinition>();

    [SerializeField]
    [Tooltip("Places the offer on the ring. Left empty, level-ups fall back to granting a " +
             "skill outright so the game still runs in a scene that has not been wired yet.")]
    private PickupSpawner pickups;

    public Player player;
    public TextMeshProUGUI skillText;
    public GameObject skillTextObject;

    private SkillPool pool;

    /// <summary>
    /// Guards the fallback warning. Without it a run with no spawner wired logs
    /// once per level-up, which buries whatever else is in the console.
    /// </summary>
    private bool warnedAboutMissingSpawner;

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

    /// <summary>
    /// Answers a level-up. Draws what the offer needs and puts it on the ring;
    /// nothing is granted and nothing is charged against the pool until the
    /// player actually collects one.
    /// </summary>
    public void PickSkill(){
        if(pool == null){
            pool = new SkillPool(skills);
        }

        if (pickups == null)
        {
            GrantDirectly();
            return;
        }

        List<SkillDefinition> offered = pool.Draw(pickups.OfferSize);
        if (offered.Count == 0)
        {
            return;
        }

        pickups.OfferSkills(offered);
    }

    /// <summary>
    /// Applies a skill the player has just flown into, and only now charges it
    /// against its pick limit. The skills that were offered alongside it were
    /// never charged, so they are still available next level.
    /// </summary>
    public void ApplyCollected(SkillDefinition skill)
    {
        if (skill == null)
        {
            return;
        }

        pool?.RecordPick(skill);
        skill.Apply(player);

        if (GameSounds.Instance != null)
        {
            GameSounds.Play(GameSounds.Instance.SkillPicked);
        }

        int picksTaken = pool?.PicksTaken(skill) ?? 1;
        StartCoroutine(showText(skill.GetDisplayName(picksTaken)));
    }

    /// <summary>
    /// The old behaviour, kept only as a fallback for a scene with no spawner
    /// assigned. Warns once, because silently reverting to free upgrades would
    /// look like the pickups were simply not working.
    /// </summary>
    private void GrantDirectly()
    {
        if (!warnedAboutMissingSpawner)
        {
            warnedAboutMissingSpawner = true;
            Debug.LogWarning(
                "SkillSelect has no PickupSpawner assigned, so level-ups are granting skills " +
                "outright instead of offering them on the ring.", this);
        }

        SkillDefinition skill = pool.Next();
        if (skill == null)
        {
            return;
        }

        skill.Apply(player);

        if (GameSounds.Instance != null)
        {
            GameSounds.Play(GameSounds.Instance.SkillPicked);
        }

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
