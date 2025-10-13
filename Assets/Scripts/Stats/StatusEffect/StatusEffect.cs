using UnityEngine;

[System.Serializable]
public class StatusEffect
{
    [Header("Info")]

    [SerializeField] public string source;
    [SerializeField] public string effectName;
    [SerializeField] public CharacterStatType statType; 
    [SerializeField] public int modifierAmount;
    [SerializeField] public float totalDuration;
    [SerializeField] public bool isPercentage;
    [SerializeField] public bool isDebuffFromArmor;
    [SerializeField] public bool isDebuffFromEnemy;
    [SerializeField] public int debuffPriority;

    [TextArea]
    [SerializeField] public string description;


    [HideInInspector]public bool isDebuff => this.modifierAmount < 0;    
}
