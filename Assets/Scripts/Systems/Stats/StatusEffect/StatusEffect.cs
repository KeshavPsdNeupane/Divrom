using UnityEngine;

[System.Serializable]
public class StatusEffect
{
    public const float PERMANENT_BUFF_DURATION = -1f;

    [Header("Info")]
    public string source;
    public string effectName;
    public CharacterStatType statType;
    public float modifierAmount;

    [Tooltip("If you put the value exact -1, it means the buff is permanent")]
    [Min(-1f)]
    public float totalDuration;

    public bool isPercentage;
    public bool isDebuffFromArmor;
    public bool isDebuffFromEnemy;
    public int debuffPriority;

    [TextArea]
    public string description;

    [HideInInspector] public bool IsDebuff => modifierAmount < 0;
    [HideInInspector] public bool IsPermanentEffect => totalDuration == PERMANENT_BUFF_DURATION;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Clamp duration: allow -1 for permanent, otherwise >= 0
        if (!(totalDuration == PERMANENT_BUFF_DURATION))
            totalDuration = Mathf.Max(0f, totalDuration);
    }
#endif
}
