using UnityEngine;
using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Sensor;
using Kope.Core.Identity;


[RequireComponent(typeof(CircleCollider2D))]
public class StatModifierCollector : SensorBase {
	[SerializeField] private string StatusObjectTagName = "StatusEffect";
	[SerializeField] private EntityComponentsRegistry ecr;
	private CharacterStatsSystem characterStats;


	public override void OnStart() {

		if (ecr == null) {
			Debug.LogError("No EntityComponentStore assigned to StatModifierCollector" + this._parentGOHiearchPathMessage);
			return;
		}
		// since we are mutating the CharacterStatsSystem by adding stat modifiers to it, we need mutatable access here. so using TryGetMutatableComponent for semantic clarity
		if (ecr.ComponentRegistry.TryGetMutatableComponent(out CharacterStatsSystem statsSystem)) {
			this.characterStats = statsSystem;
		} else {
			Debug.LogError("No CharacterStatsSystem found in EntityComponentStoreConfig for StatModifierCollector" + this._parentGOHiearchPathMessage);
			return;
		}
	}
	public override void OnDetect(Collider2D other) {
		if (other.CompareTag(StatusObjectTagName)) {
			// we are using TryGetComponent here instead of GetComponent because we want to log an error 
			// if the component is not found, rather than throwing an exception. since we are only trying
			//  to get the StatusEffectContainer component, and it's possible that the detected object might
			//  not have it (if it's not set up correctly), using TryGetComponent allows us to handle that case
			//  gracefully by logging an error message and returning early, rather than having an unhandled exception
			//  that could disrupt the game flow.
			if (!other.TryGetComponent(out EntityInstance mgr)) {
				Debug.LogError("No EntityManager found on detected object with tag " + StatusObjectTagName + ". Please ensure the object has an EntityManager component." + this._parentGOHiearchPathMessage);
				return;
			}


			// using tryGet so we can satisfy the semantic clarity of "if it has the component, 
			// we will use it, if not, we will log an error and return". since we are not mutating
			// the StatusEffectContainer, we don't need mutatable access, so TryGetComponent is sufficient here.

			// we are garunteed to get StatusEffectContainer from the detected object, 
			// because we are only detecting objects with the specified tag,
			//  and we have a convention that any object with that tag must have 
			// a StatusEffectContainer component. so if we don't find it, it means something is
			//  wrong with the setup of the detected object, and we log an error to notify the developer to fix it.
			if (!mgr.EntityDetail.ComponentRegistry.TryGetReadOnlyComponent(out StatModifierContainer effect)) {
				Debug.LogError("No StatusEffectContainer found on detected object with tag " + StatusObjectTagName + ". Please ensure the object has a StatusEffectContainer component." + this._parentGOHiearchPathMessage);
				return;
			}
			if (effect != null && effect.StatusEffect != null && this.characterStats != null) {
				if (this.characterStats.AddStatModifier(effect.StatusEffect)) {
					// always call NotifyEntityDiedOrPooled before destroying the gameobject,
					// so that any systems that need to react to the entity's death or pooling 
					// can do so before the gameobject is destroyed and becomes inaccessible.
					mgr.NotifyEntityDiedOrPooled();

					// for this case we are treating the status effect as a "pickup" that the character can collect
					// and apply to themselves, so we destroy the gameobject after collecting it.
					// we could even use pooling but the format will be same as we are already notifying
					// the EntityManager that the entity is "pooled" (in this case, returned to the pool 
					// instead of actually being destroyed), so any pooling system that listens to the OnEntityDiedOrPooled 
					// event can handle it accordingly, whether it's actually destroying the gameobject or just deactivating
					// it and returning it to the pool for later reuse.
					Destroy(other.gameObject);

				}

			}
		}
	}
}
