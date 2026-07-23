using Kope.Character.Stats;
using UnityEngine;

[System.Serializable]
public abstract class AbstractBaseModifier {
	public const float PERMANENT_BUFF_DURATION = -1f;

	public string source = "Default";
	public string effectName = "None";

	[Tooltip("For Resistance Type stat , this bool is always false and if 'True' " +
	"the modifier amount is treated as percentage")]
	public bool isPercentage;
	public bool isDebuffFromArmor;
	public bool isDebuffFromEnemy;
	public int debuffPriority;
	public string description;

	[Min(-1)]
	public float totalDuration;
	public abstract float ModifierAmount { get; }

	public bool IsDebuff => ModifierAmount < 0;
	public bool IsPermanentEffect => totalDuration == PERMANENT_BUFF_DURATION;

	protected AbstractBaseModifier(
		string source,
		string effectName,
		float totalDuration,
		bool isPercentage,
		bool isDebuffFromArmor,
		bool isDebuffFromEnemy,
		int debuffPriority,
		string description
	) {
		this.source = source;
		this.effectName = effectName;
		this.totalDuration = totalDuration;
		this.isPercentage = isPercentage;
		this.isDebuffFromArmor = isDebuffFromArmor;
		this.isDebuffFromEnemy = isDebuffFromEnemy;
		this.debuffPriority = debuffPriority;
		this.description = description;
	}

}
[System.Serializable]
public class BaseStatModifier : AbstractBaseModifier {
	public CharacterStatType statType;
	public float modifierAmount;
	public override float ModifierAmount => modifierAmount;

	public BaseStatModifier(string source, string effectName, CharacterStatType statType,
		float modifierAmount, float totalDuration = PERMANENT_BUFF_DURATION,
		bool isPercentage = false, bool isDebuffFromArmor = false, bool isDebuffFromEnemy = false,
		int debuffPriority = 0, string description = "") :
		 base(source, effectName, totalDuration, isPercentage, isDebuffFromArmor,
			isDebuffFromEnemy, debuffPriority, description) {
		this.statType = statType;
		this.modifierAmount = modifierAmount;
	}
	public override string ToString() {
		return $"{effectName},{statType} ({(isPercentage ? modifierAmount * 100 + "%" : modifierAmount.ToString())} {statType})";
	}
}

[System.Serializable]
public class ResistanceStatModifier : AbstractBaseModifier {
	public DamageType statType;
	[Range(-1f, 1f)]
	public float modifierAmount;
	public override float ModifierAmount => modifierAmount;
	public ResistanceStatModifier(string source, string effectName, DamageType statType,
		float modifierAmount, float totalDuration = PERMANENT_BUFF_DURATION
		, bool isDebuffFromArmor = false, bool isDebuffFromEnemy = false,
		int debuffPriority = 0, string description = "") :
		 base(source, effectName, totalDuration, false, isDebuffFromArmor,
			isDebuffFromEnemy, debuffPriority, description) {
		this.statType = statType;
		this.modifierAmount = modifierAmount;
	}
	public override string ToString() {
		return $"{effectName},{statType} ({modifierAmount * 100}% {statType})";
	}
}