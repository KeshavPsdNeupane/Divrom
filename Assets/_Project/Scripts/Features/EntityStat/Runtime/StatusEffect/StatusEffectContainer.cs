using Kope.Character.Stats;
using Kope.Core.Init;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectContainer : InitializableBase
// needed so it can be put on ECR prefab and have the status effect data set in the inspector
{
	[Header("Info")]
	[SerializeField] private string source = "Default";
	[SerializeField] private string effectName = "None";
	[SerializeField] private CharacterStatType statType;
	[SerializeField] private float modifierAmount;

	[Tooltip("If you put the value exact -1, it means the buff is permanent")]
	[Min(-1f)]
	[SerializeField] private float totalDuration;

	[SerializeField] private bool isPercentage;
	[SerializeField] private bool isDebuffFromArmor;
	[SerializeField] private bool isDebuffFromEnemy;
	[SerializeField] private int debuffPriority;

	[TextArea]
	[SerializeField] private string description;

	private StatusEffect statusEffect;
	public StatusEffect StatusEffect => this.statusEffect ??= new StatusEffect {
		source = this.source,
		effectName = this.effectName,
		statType = this.statType,
		modifierAmount = this.modifierAmount,
		totalDuration = this.totalDuration,
		isPercentage = this.isPercentage,
		isDebuffFromArmor = this.isDebuffFromArmor,
		isDebuffFromEnemy = this.isDebuffFromEnemy,
		debuffPriority = this.debuffPriority,
		description = this.description
	};
}
