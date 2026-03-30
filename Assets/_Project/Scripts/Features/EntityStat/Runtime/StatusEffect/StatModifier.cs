using Kope.Character.Stats;


[System.Serializable]
public class StatModifier {
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
	public StatModifier(string source, string effectName, CharacterStatType statType, float modifierAmount, float totalDuration, bool isPercentage, bool isDebuffFromArmor, bool isDebuffFromEnemy, int debuffPriority, string description) {
		this.source = source;
		this.effectName = effectName;
		this.statType = statType;
		this.modifierAmount = modifierAmount;
		this.totalDuration = totalDuration;
		this.isPercentage = isPercentage;
		this.isDebuffFromArmor = isDebuffFromArmor;
		this.isDebuffFromEnemy = isDebuffFromEnemy;
		this.debuffPriority = debuffPriority;
		this.description = description;
	}
}
