using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.CompilerServices;
using Kope.Core.EntityComponentSystem;

[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectCollector : SensorBase
{
	[SerializeField] private string StatusObjectTagName = "StatusEffect";
	[SerializeField] private EntityComponentsRegistry ecr;
	private CharacterStatsSystem characterStats;


	public override void OnStart()
	{

		if (ecr == null)
		{
			MyLogger.Error("No EntityComponentStore assigned to StatusEffectCollector" + this.parentGameObjectStackTraceMessage);
			return;
		}
		if (ecr.ComponentRegistry.TryGetComponent<CharacterStatsSystem>(out var statsSystem))
		{
			this.characterStats = statsSystem;
		}
		else
		{
			MyLogger.Error("No CharacterStatsSystem found in EntityComponentStoreConfig for StatusEffectCollector" + this.parentGameObjectStackTraceMessage);
			return;
		}
	}
	public override void OnDetect(Collider2D other)
	{
		if (other.CompareTag(StatusObjectTagName))
		{
			StatusEffectContainer effect = other.GetComponent<StatusEffectContainer>();
			if (effect != null && effect.StatusEffect != null && this.characterStats != null)
			{
				if (this.characterStats.AddStatModifier(effect.StatusEffect))
				{
					Destroy(other.gameObject);
				}

			}
		}
	}
}
