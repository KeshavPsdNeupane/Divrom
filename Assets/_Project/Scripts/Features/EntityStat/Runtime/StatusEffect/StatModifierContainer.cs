using Kope.Character.Stats;
using Kope.EntityComponentSystem;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class StatModifierContainer : ComponentBase
// needed so it can be put on ECR prefab and have the stat modifier data set in the inspector
{
	[Header("Info")]
	[SerializeField] private string source = "Default";
	[SerializeField] private string effectName = "None";
	[SerializeField] private CharacterStatType statType;
	[SerializeField] private float modifierAmount;

	[Tooltip("If you put the value exact -1, it means the buff/debuff is permanent")]
	[Min(-1f)]
	[SerializeField] private float totalDuration;

	[SerializeField] private bool isPercentage;
	[SerializeField] private bool isDebuffFromArmor;
	[SerializeField] private bool isDebuffFromEnemy;
	[SerializeField] private int debuffPriority;

	[TextArea]
	[SerializeField] private string description;

	private BaseStatModifier statusEffect;
	public BaseStatModifier StatusEffect => this.statusEffect ??= new BaseStatModifier(
		this.source,
		this.effectName,
		this.statType,
		this.modifierAmount,
		this.totalDuration,
		this.isPercentage,
		this.isDebuffFromArmor,
		this.isDebuffFromEnemy,
		this.debuffPriority,
		this.description
	);
}
