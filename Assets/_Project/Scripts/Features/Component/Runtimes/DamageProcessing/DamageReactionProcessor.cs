using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Component.Health.Interface;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.LifeTimeManagement;
using UnityEngine;
using Kope.Component.Health;

namespace Kope.Component.Combat {
	/// <summary>
	/// This component is responsible for processing incoming damage and effects on an entity. 
	/// It listens for hits on the attached HurtBox and applies damage and effects accordingly.
	/// If a entity his this component, then that entity is expected fight back to player, so be careful where you put this.<br/>
	/// <b> Important: </b><br/>
	/// - This component assumes that the entity has a HealthComponent and an IStatSystem for it to function properly. <br/>
	/// - The main reason of seperation of this component from the HealthComponent is to allow for more flexible and 
	/// modular damage processing logic, as well as to keep the HealthComponent focused solely on managing
	/// health values and related mechanics. <br/>
	/// - and not all entity needs below whole complex damage processing, for example,
	///  a destructible environment might just need to get destroyed on a single hit, we can just create 1HitEntityComponent.
	/// 	which will handle that event rather than this bloat of component. <br/>
	/// </summary>
	public class DamageReactionProcessor : InitializableBase, IDamagable {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private DamageCalculationConfig config;

		private IHitBoxComponent _hurtBox;
		private IHealthComponent _healthComponent;
		private IStatSystem _statSystem;

		private readonly List<ITickableEffect> _activeTickableEffects = new();
		// replace this with the actual LevelUp component when we have it, this is just for
		// testing the level scaling portion of the damage formula
		private readonly float _currentLevel = 0;

		public IHitBoxComponent HurtBox => this._hurtBox;

		protected override bool OnInit() {
			string parentHierarchy = GetParentGameObjectHeirarchyMessage();
			if (this.ecr == null) {
				Debug.LogError($"DamageProcessor on {gameObject.name} has no ECR assigned."
				+ $"on{parentHierarchy}");
				return false;
			}
			if (this.config == null) {
				Debug.LogError($"DamageProcessor on {gameObject.name} has no DamageCalculationConfig assigned."
				+ $"on{parentHierarchy}");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out _healthComponent)) {
				Debug.LogError($"DamageProcessor on {gameObject.name} failed to find HealthComponent."
				+ $"on{parentHierarchy}");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out _statSystem)) {
				Debug.LogError($"DamageProcessor on {gameObject.name} failed to find IStatSystem."
				+ $"on{parentHierarchy}");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._hurtBox)) {
				Debug.LogError($"DamageProcessor on {gameObject.name} failed to find HurtBox."
				+ $"on{parentHierarchy}");
				return false;
			}
			return true;
		}



		#region IDamagable Implementation

		public void TakeDamageDebugOnly(int amount) {
			this._healthComponent.ApplyDamage(amount);
			Debug.Log($"TakeDamageDebugOnly called with amount: {amount}. Current health: {this._healthComponent.CurrentHealth}");
		}

		public float TakeHit(DamageDetail damageDetail) {
			if (!this.IsInitialized) return 0f;
			float finalDamage = this.config.TakeHit(damageDetail, this._currentLevel, this._statSystem);

			this._healthComponent.ApplyDamage(finalDamage);
			return finalDamage;
		}
		public void AddStatModifier(BaseStatModifier modifier) {
			if (!this.IsInitialized) return;
			// for the return bool, this component dont care whether modifier is applied or not.
			_ = this._statSystem.AddStatModifier(modifier);
		}
		#endregion




		#region  Hit Handling
		private void HandleHurtBoxHit(DamagableHitInfo hitInfo) {
			if (!this.IsInitialized || hitInfo.Effects == null) return;

			for (int i = 0; i < hitInfo.Effects.Count; i++) {
				var effect = hitInfo.Effects[i]?.Create(hitInfo.EffectContext);
				ApplyEffect(effect);
			}
		}
		private void HandleStatChangeHit(StatChangeHitInfo hitInfo) {
			if (!this.IsInitialized || hitInfo.StatEffects == null) return;

			for (int i = 0; i < hitInfo.StatEffects.Count; i++) {
				var effect = hitInfo.StatEffects[i]?.Create(hitInfo.EffectContext);
				ApplyEffect(effect);
			}
		}
		#endregion

		#region Effect Application
		// why we are handling stat effect here rather than in the stat system itself?
		// since this class references stats anyway to get the stat values for damage calculation, 
		// it is more efficient to apply the stat modifier here rather than having the stat
		// system listen for hit events and then apply the modifier, which would require additional
		// event handling and potentially more complex logic to determine when to apply the modifier.
		// finally if a entity has this component then that eniity has 1000% the stat system, 
		// so we don't need to worry about null reference when applying stat modifier here.
		public void ApplyEffect(IEffect<IStatSystem> effect) {
			if (!this.IsInitialized || effect == null) return;
			// IEffect is never ITickableEffect since the statsystem effects
			// are instant for this component, and stat system manages the duration of the stat modifier 
			// itself, so we don't need to track it here.
			effect.Apply(this._statSystem);
		}


		public void ApplyEffect(IEffect<IDamagable> effect) {
			if (!this.IsInitialized || effect == null) return;

			if (effect is ITickableEffect tickable) {
				this._activeTickableEffects.Add(tickable);
				tickable.OnCompletedOrCancelled += OnEffectExpired;
			}
			effect.Apply(this);
		}

		#endregion



		private void OnEffectExpired(ITickableEffect effect) {
			effect.OnCompletedOrCancelled -= OnEffectExpired;
			this._activeTickableEffects.Remove(effect);
		}

		private void ClearActiveEffects() {
			for (int i = this._activeTickableEffects.Count - 1; i >= 0; i--) {
				this._activeTickableEffects[i]?.Cancel();
			}
			this._activeTickableEffects.Clear();
		}


		private void OnEnable() {
			if (this._hurtBox == null) return;
			this._hurtBox.OnHitCombatible += HandleHurtBoxHit;
			this._hurtBox.OnHitStatChange += HandleStatChangeHit;
		}

		private void OnDisable() {
			if (this._hurtBox != null) {
				this._hurtBox.OnHitCombatible -= HandleHurtBoxHit;
				this._hurtBox.OnHitStatChange -= HandleStatChangeHit;
			}
			ClearActiveEffects();
		}

		protected override void OnUpdate() {
			if (this._activeTickableEffects.Count == 0) return;

			float deltaTime = Time.deltaTime;
			for (int i = this._activeTickableEffects.Count - 1; i >= 0; i--) {
				this._activeTickableEffects[i]?.Tick(deltaTime);
			}
		}



	}
}