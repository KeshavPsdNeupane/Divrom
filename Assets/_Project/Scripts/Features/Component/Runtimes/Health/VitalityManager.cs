using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Component.Health.Interface;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;
using Kope.Component.HitBox;


namespace Kope.Component.Health.Interface {
	public interface IVitalityManager {
		void Heal(float flat, float percent);

		void ApplyEffect(IEffect<IVitalityManager> effect);
	}
	public class VitalityManager : InitializableBase, IVitalityManager {
		[SerializeField] private EntityComponentsRegistry ecr;

		private IHurtBoxComponent _hurtBox;
		private IHealthComponent _health;
		private IStatSystem _statSystem;

		private readonly List<ITickableEffect> _activeHealEffects = new();

		protected override bool OnInit() {
			if (!ecr.ComponentRegistry.TryGetMutatableComponent(out _health)) return false;
			if (!ecr.ComponentRegistry.TryGetReadOnlyComponent(out _statSystem)) return false;
			if (!ecr.ComponentRegistry.TryGetMutatableComponent(out _hurtBox)) return false;

			return true;
		}

		private void OnEnable() {
			if (_hurtBox != null) _hurtBox.OnHitHealable += HandleHurtBoxHeal;
		}

		private void OnDisable() {
			if (_hurtBox != null) _hurtBox.OnHitHealable -= HandleHurtBoxHeal;
			ClearActiveEffects();
		}

		protected override void OnUpdate() {
			if (_activeHealEffects.Count == 0) return;

			float dt = Time.deltaTime;
			for (int i = _activeHealEffects.Count - 1; i >= 0; i--) {
				_activeHealEffects[i].Tick(dt);
			}
		}

		public void Heal(float flat, float percent) {
			if (!IsInitialized) return;

			// Example of Logic: Increase healing based on a "Recovery" stat or SP
			float sp = _statSystem.GetStatValue(CharacterStatType.SP);
			float bonusMult = 1f + (sp * 0.01f); // 1% extra healing per SP

			_health.Heal(flat * bonusMult, percent);
		}

		public void ApplyEffect(IEffect<IVitalityManager> effect) {
			if (effect is ITickableEffect tickable) {
				_activeHealEffects.Add(tickable);
				tickable.OnCompletedOrCancell += (e) => _activeHealEffects.Remove(e);
			}
			effect.Apply(this);
		}

		private void HandleHurtBoxHeal(HealableHitInfo info) {
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