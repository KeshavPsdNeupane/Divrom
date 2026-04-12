using Kope.Core.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using Kope.Component.Animation;

namespace Kope.Component.Attack {


	public interface IAttackComponent {
		float GetAttackDamage();
	}

	/// <summary>
	/// Base attack logic component. Can be used for both player and AI.
	/// Handles stat subscription and damage calculation.
	/// </summary>
	public abstract class AttackComponentBase : InitializableBase, IAttackComponent {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private WeaponSO equippedWeaponDataSO;
		private AnimationComponentBase _animationComponent;
		protected CharacterStatsSystem _statsSystem;
		protected float _attack;
		protected float _normalizedCriticalChance;
		protected float _normalizedCriticalDamage;

		public WeaponData EquippedWeaponData => this.equippedWeaponDataSO.CurrentWeaponData;

		public event UnityAction OnAttackPerformed;

		protected override bool OnInit() {
			if (this.ecr == null) {
				MyLogger.Error("EntityComponentStore reference is missing in AttackComponentBase." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}
			if (this.ecr.ComponentRegistry.TryGetMutatableComponent(out AnimationComponentBase animComp)) {
				this._animationComponent = animComp;
			} else {
				MyLogger.Error("AnimationComponentBase not found in EntityComponentStore." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (this.ecr.ComponentRegistry.TryGetMutatableComponent(out CharacterStatsSystem statsSys)) {
				this._statsSystem = statsSys;
			} else {
				MyLogger.Error("CharacterStatsSystem not found in EntityComponentStore. " +
				"AttackComponentBase will not function properly." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}

			SubscribeToStats();
			return true;
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() => UnsubscribeFromStats();

		protected void SubscribeToStats() {
			if (!IsInitialized || this._statsSystem == null) return;
			if (this._statsSystem != null && this._statsSystem.CurrentStats != null) {
				this._statsSystem.StatsSubscribe(CharacterStatType.ATK, this.AttackCallback);
				this._statsSystem.StatsSubscribe(CharacterStatType.CRATE, this.CriticalRateCallBack);
				this._statsSystem.StatsSubscribe(CharacterStatType.CDMG, this.CriticalDamageCallBack);

				// Initial fetch
				this.AttackCallback(this._statsSystem.GetStatValue(CharacterStatType.ATK));
				this.CriticalRateCallBack(this._statsSystem.GetStatValue(CharacterStatType.CRATE));
				this.CriticalDamageCallBack(this._statsSystem.GetStatValue(CharacterStatType.CDMG));
			}
		}

		protected void UnsubscribeFromStats() {
			if (this._statsSystem != null && this._statsSystem.CurrentStats != null) {
				this._statsSystem.StatsUnsubscribe(CharacterStatType.ATK, this.AttackCallback);
				this._statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, this.CriticalRateCallBack);
				this._statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, this.CriticalDamageCallBack);
			}
		}

		protected virtual void AttackCallback(float value) => this._attack = value;
		protected virtual void CriticalRateCallBack(float value) => this._normalizedCriticalChance = value * 0.01f;
		protected virtual void CriticalDamageCallBack(float value) => this._normalizedCriticalDamage = 1 + value * 0.01f;

		/// <summary>
		/// Calculate the final damage based on current base Scaling stat (usually the attack stat) and critical hit chance/damage.
		/// Overridable to allow for different damage calculation logic based on character design.
		/// For example, some character scale from 50% of atk, 40% ofdef and 10% of hp, or whatever(at once).
		/// like we have 100atk, 50def, 200hp, and the damage is calculated as 100*0.5 + 50*0.4 + 200*0.1 = 90, 
		/// then we apply critical hit multiplier on top of that. 
		/// so we make it flexible to allow for different scaling logic based on the character's design.
		/// </summary>
		/// <returns></returns>
		protected virtual float CalculateDamage() {
			return CalculateDamage(this._attack);
		}
		/// <summary>
		/// Calculates the final damage based on the provided base scaling stat (usually the attack stat).
		/// But some character can scale from Hp, or Def, or whatever. So we make it flexible to allow
		/// for different scaling stats based on the character's design.
		/// </summary>
		/// <param name="baseScalingStat"></param>
		/// <returns></returns>
		protected virtual float CalculateDamage(float baseScalingStat) {
			float damage = baseScalingStat;
			if (this._normalizedCriticalChance >= 1f) return damage * this._normalizedCriticalDamage;

			if (Random.value < this._normalizedCriticalChance) return damage * this._normalizedCriticalDamage;
			return damage;
		}

		public float GetAttackDamage() {
			if (!CanPerformAttack()) return 0f;
			float dmg = PerformAttackInternal();
			RaiseOnAttackPerformedEvent();
			return dmg;
		}


		private bool CanPerformAttack() {
			return this._animationComponent.CanTransitionToAnimation(this.EquippedWeaponData.PrimaryAttackAnimationHash);
		}

		/// <summary>
		/// Hook for subclasses to implement the actual attack logic, such as playing animations, 
		/// applying damage to targets, etc.
		/// </summary>
		protected abstract float PerformAttackInternal();

		protected void RaiseOnAttackPerformedEvent() {
			this.OnAttackPerformed?.Invoke();
		}
	}

}