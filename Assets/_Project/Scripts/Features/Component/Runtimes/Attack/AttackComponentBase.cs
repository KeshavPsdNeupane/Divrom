using Kope.Core.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using Kope.Component.Animation;
using System.Collections.Generic;


// this will be refactored and will be made to work with the new ability system, 
// so it can be used for both player and enemy attacks, and also for different types 
// of attacks like melee, ranged, magic, etc.

namespace Kope.Component.Attack {
	public readonly struct DamageBaseStatComposition {
		public readonly CharacterStatType BaseStatType;
		public readonly float Multiplier;
		public DamageBaseStatComposition(CharacterStatType baseStatType, float multiplier) {
			this.BaseStatType = baseStatType;
			this.Multiplier = multiplier;
		}
	}


	public interface IAttackComponent {
		float PerformAttack();
		float GetDamageValue(CharacterStatType damageType, float multiplier = 1f);
		float GetDamageValue(List<DamageBaseStatComposition> composition);
		event UnityAction OnAttackPerformed;
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

				this._statsSystem.StatsSubscribe(CharacterStatType.CRATE, this.CriticalRateCallBack);
				this._statsSystem.StatsSubscribe(CharacterStatType.CDMG, this.CriticalDamageCallBack);


				this.CriticalRateCallBack(this._statsSystem.GetStatValue(CharacterStatType.CRATE));
				this.CriticalDamageCallBack(this._statsSystem.GetStatValue(CharacterStatType.CDMG));
			}
		}

		protected void UnsubscribeFromStats() {
			if (this._statsSystem != null && this._statsSystem.CurrentStats != null) {

				this._statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, this.CriticalRateCallBack);
				this._statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, this.CriticalDamageCallBack);
			}
		}

		protected virtual void CriticalRateCallBack(float value) => this._normalizedCriticalChance = value * 0.01f;
		protected virtual void CriticalDamageCallBack(float value) => this._normalizedCriticalDamage = 1 + value * 0.01f;

		/// <summary>
		/// Gets the base damage value based on the character's stats and the provided damage type and multiplier.
		/// This is the base damage before applying any modifiers like critical hits, enemy defenses, etc
		/// defaults to using ATK stat as base scaling, if caller want to use different stat or multiple stats, 
		/// look at the other overloads
		/// </summary>
		/// <returns></returns>
		protected float GetDamageValue() {
			return GetDamageValue(CharacterStatType.ATK, 1f);
		}

		/// <summary>
		/// Gets the base damage value based on the character's stats and the provided damage type and multiplier.
		/// flexible version of GetDamageValue that allows caller to specify which stat to use as base scaling
		/// and also a multiplier, which can be used for abilities that want to scale the damage based on a 
		/// percentage of the stat,
		/// </summary>
		/// <param name="statType"></param>
		/// <param name="Multiplier"></param>
		/// <returns></returns>
		public float GetDamageValue(CharacterStatType statType, float Multiplier = 1f) {
			// we could make this more flexible by allowing for multiple scaling stats, 
			// but for now we will just use one base scaling stat for simplicity.
			// and we could also cache the baseStat using same system we are using in StatGUI to 
			// avoid fetching it multiple times, but for now we will just fetch it directly from
			// the stats system for simplicity.
			float baseStat = this._statsSystem.GetStatValue(statType);
			var damage = CalculateDamage(baseStat * Multiplier);
			return damage;
		}

		/// <summary>
		/// Gets the base damage value based on the character's stats and the provided composition of multiple base stats and their multipliers.
		/// The most flexible version of GetDamageValue that allows caller to specify multiple base stats 
		/// and their multipliers, which can be used for abilities that want to scale the damage based on multiple stats,
		/// like 50% of ATK and 30% of HP, etc.
		/// </summary>
		/// <param name="composition"></param>
		/// <returns></returns>
		public float GetDamageValue(List<DamageBaseStatComposition> composition) {
			float baseStat = 0f;
			foreach (var comp in composition) {
				// just a O(1) lookup so no need to cache here.
				baseStat += GetDamageValue(comp.BaseStatType, comp.Multiplier);
			}
			var damage = CalculateDamage(baseStat);
			return damage;
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

		public float PerformAttack() {
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