using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;


namespace Kope.Component.Health.Interface {
	public interface IVitalityManager {
		void Heal(float flat, float percent);

		void ApplyEffect(IEffect<IHealable> effect);
	}

	/// <summary>
	/// This component is responsible for processing healing and related effects on an entity. 
	/// It listens for hits on the attached HurtBox that are healable and applies healing and effects accordingly.
	/// If a entity his this component, then that entity is expected fight back to player, so be careful where you put this.<br/>
	/// <b> Important: </b><br/>
	/// - This component assumes that the entity has a IHealable component and an IStatSystem for it to function properly. <br/>
	/// - The main reason of seperation of this component from the IHealable component is to allow for more flexible and modular healing processing logic
	/// , as well as to keep the IHealable component focused solely on managing and not all entity need to be healed, for example,
	/// a destructible environment might just need to get destroyed on a single hit, we can just 
	/// create 1HitEntityComponent which will handle that event rather than this bloat of component. <br/>
	/// 	
	/// </summary>
	public class HealingProcessor : InitializableBase, IVitalityManager {
		[SerializeField] private EntityComponentsRegistry ecr;

		private IHitBoxComponent _hurtBox;

		private IStatSystem _statSystem;
		private IHealable _health;

		private readonly List<ITickableEffect> _activeHealEffects = new();

		protected override bool OnInit() {
			if (ecr == null || ecr.ComponentRegistry == null) {
				Debug.LogError($"HealingProcessor on {gameObject.name} is missing an EntityComponentsRegistry.");
				return false;
			}
			if (!ecr.ComponentRegistry.TryGetReadOnlyComponent(out this._health)) {
				Debug.LogError($"HealingProcessor on {gameObject.name} could not find an IHealable component in the EntityComponentsRegistry.");
				return false;
			}
			if (!ecr.ComponentRegistry.TryGetReadOnlyComponent(out this._statSystem)) {
				Debug.LogError($"HealingProcessor on {gameObject.name} could not find an IStatSystem component in the EntityComponentsRegistry.");
				return false;
			}
			if (!ecr.ComponentRegistry.TryGetMutatableComponent(out this._hurtBox)) {
				Debug.LogError($"HealingProcessor on {gameObject.name} could not find an IHurtBoxComponent component in the EntityComponentsRegistry.");
				return false;
			}

			return true;
		}

		private void OnEnable() {
			if (this._hurtBox != null) this._hurtBox.OnHitHealable += HandleHurtBoxHeal;
		}

		private void OnDisable() {
			if (this._hurtBox != null) this._hurtBox.OnHitHealable -= HandleHurtBoxHeal;
			ClearActiveEffects();
		}

		protected override void OnUpdate() {
			if (this._activeHealEffects.Count == 0) return;

			float dt = Time.deltaTime;
			for (int i = this._activeHealEffects.Count - 1; i >= 0; i--) {
				this._activeHealEffects[i].Tick(dt);
			}
		}

		public void Heal(float flat, float percent) {
			if (!IsInitialized) return;

			// Example of Logic: Increase healing based on a "Recovery" stat or SP
			float sp = this._statSystem.GetStatValue(CharacterStatType.SP);
			float bonusMult = 1f + (sp * 0.01f); // 1% extra healing per SP

			this._health.Heal(flat * bonusMult, percent);
		}

		public void ApplyEffect(IEffect<IHealable> effect) {
			if (effect is ITickableEffect tickable) {
				this._activeHealEffects.Add(tickable);
				// using the class callback to remove the efect than using lambda to avoid potential issues
				// with closures and ensure the correct effect is removed when it completes or is cancelled.
				tickable.OnCompletedOrCancelled += RemoveEffect;
			}
			effect.Apply(this._health);
		}

		private void RemoveEffect(ITickableEffect effect) {
			this._activeHealEffects.Remove(effect);
		}

		private void HandleHurtBoxHeal(HealableHitInfo info) {
			if (info.Effects == null) return;
			foreach (var factory in info.Effects) {
				var effect = factory.Create(info.EffectContext);
				ApplyEffect(effect);
			}
		}

		private void ClearActiveEffects() {
			for (int i = _activeHealEffects.Count - 1; i >= 0; i--) {
				_activeHealEffects[i].Cancel();
			}
			_activeHealEffects.Clear();
		}
	}
}