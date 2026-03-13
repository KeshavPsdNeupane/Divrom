using UnityEngine;
using Kope.Character.Stats;


[System.Serializable]
public class StatusEffect
{
	public const float PERMANENT_BUFF_DURATION = -1f;
	public string source = "Default";
	public string effectName = "None";
	public CharacterStatType statType;
	public float modifierAmount;

	public float totalDuration;

	public bool isPercentage;
	public bool isDebuffFromArmor;
	public bool isDebuffFromEnemy;
	public int debuffPriority;


	public string description;

	public bool IsDebuff => modifierAmount < 0;
	public bool IsPermanentEffect => totalDuration == PERMANENT_BUFF_DURATION;

#if UNITY_EDITOR
	private void OnValidate()
	{
		// Clamp duration: allow -1 for permanent, otherwise >= 0
		if (!(totalDuration == PERMANENT_BUFF_DURATION))
			totalDuration = Mathf.Max(0f, totalDuration);
	}
#endif
}
