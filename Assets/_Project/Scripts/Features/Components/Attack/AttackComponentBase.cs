using UnityEngine;
using UnityEngine.Events;
using Kope.Core.LifeTimeManagement;
using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using System.Collections.Generic;
using Kope.Component.Animation;


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
		event System.Action OnAttackPerformed;
		event System.Action<WeaponData> OnAttackPerformed1;
		float PerformAttack();
		float GetDamageValue(CharacterStatType damageType, float multiplier = 1f);
		float GetDamageValue(List<DamageBaseStatComposition> composition);
	}

	public abstract class AttackComponentBase : InitializableBase, IAttackComponent {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private WeaponSO equippedWeapDataSO;

		private AnimationComponentBase _animationComponent;
		// Caching dictionaries for O(1) local access
		private readonly Dictionary<CharacterStatType, float> _cachedStats = new();
		private readonly Dictionary<CharacterStatType, UnityAction<float>> _statCallbacks = new();

		protected CharacterStatsSystemBase _statsSystem;
		protected float _normalizedCriticalChance;
		protected float _normalizedCriticalDamage;

		public event System.Action OnAttackPerformed;
		public event System.Action<WeaponData> OnAttackPerformed1;


		protected abstract float PerformAttackInternal();


		#region  Init And Unity 
		protected override bool OnInit() {
			if (this.ecr == null) {
				Debug.LogError("EntityComponentStore reference is missing." + this.HieararchyPath);
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetMutable(out this._animationComponent)) {
				Debug.LogError("AnimationComponent not found." + this.HieararchyPath);
			}

			if (!this.ecr.ComponentRegistry.TryGetMutable(out this._statsSystem)) {
				Debug.LogError("CharacterStatsSystem not found." + this.HieararchyPath);
				return false;
			}
			if (this.equippedWeapDataSO == null) {
				Debug.LogError($"WeaponSo is null {this.HieararchyPath}");
			}
			InitializeStatCache();
			SubscribeToStats();

			return true;
		}
		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() => UnsubscribeFromStats();
		#endregion


		#region  IAttackComponent Implementation
		public float PerformAttack() {
			if (!CanPerformAttack()) return 0f;
			float dmg = PerformAttackInternal();
			this.OnAttackPerformed?.Invoke();
			this.OnAttackPerformed1?.Invoke(this.equippedWeapDataSO.CurrentWeaponData);
			return dmg;
		}
		/// <summary>
		/// The simplest way to calculate damage based on a single stat type and multiplier. 
		/// This is useful for most basic attacks that scale off a single stat, like a sword attack that scales off ATK.
		/// For more complex attacks that scale off multiple stats, the GetDamageValue(List<DamageBaseStatComposition>) 
		/// method can be used, which takes a list of stat compositions and sums them up for the damage calculation.
		/// Defaults to using ATK as the base stat type with a multiplier of 1, which means 
		/// if you just call GetDamageValue() without parameters, it will calculate damage 
		/// based on the ATK stat directly.
		/// </summary>
		/// <param name="statType"></param>
		/// <param name="Multiplier"></param>
		/// <returns></returns>
		public float GetDamageValue(CharacterStatType statType = CharacterStatType.ATK, float Multiplier = 1f) {
			float baseStat = this._cachedStats.TryGetValue(statType, out float val) ? val : 0f;
			if (baseStat == 0f)
				return 0f;
			return CalculateDamage(baseStat * Multiplier);
		}
		/// <summary>
		/// The most flexible way to calculate damage based on multiple stats and multipliers.
		/// This allows for attacks that scale off multiple stats, like a magic attack that scales off
		/// both ATK and MAG, or an attack that has a complex formula for damage calculation.
		/// The composition list allows you to specify any number of stats and their corresponding
		/// multipliers, and the method will sum them up to get the total base stat for damage calculation.
		/// For example, if you have an attack that scales 50% off ATK and 30% off MAG, you can create
		/// a composition list with two entries: one for ATK with a multiplier of 0.5, and one for MAG 
		/// with a multiplier of 0.3. The method will then calculate the damage based on the combined 
		/// contribution of both stats.
		/// </summary>
		/// <param name="composition"></param>
		/// <returns></returns>
		public float GetDamageValue(List<DamageBaseStatComposition> composition) {
			float totalBase = 0f;
			foreach (var comp in composition) {
				// Use cached lookup
				float val = this._cachedStats.TryGetValue(comp.BaseStatType, out float s) ? s : 0f;
				totalBase += val * comp.Multiplier;
			}
			return CalculateDamage(totalBase);
		}

		#endregion


		private void InitializeStatCache() {
			// Pre-fill cache for all stats to avoid "KeyNotFound" errors
			foreach (CharacterStatType type in System.Enum.GetValues(typeof(CharacterStatType))) {
				this._cachedStats[type] = this._statsSystem.GetStatValue(type);

				// Create a persistent callback for each stat
				this._statCallbacks[type] = (val) => {
					this._cachedStats[type] = val;
					// Special handling for crit which is used in every CalculateDamage call
					if (type == CharacterStatType.CRATE) CriticalRateCallBack(val);
					if (type == CharacterStatType.CDMG) CriticalDamageCallBack(val);
				};
			}
		}



		protected void SubscribeToStats() {
			if (!this.IsInitialized || this._statsSystem == null) return;

			foreach (var kvp in this._statCallbacks) {
				this._statsSystem.StatsSubscribe(kvp.Key, kvp.Value);
				// Forcing the callback to populate the cache with current values on subscribe
				// this is necessary because some stats might not change for a long time, and
				// we want to ensure our cache is always up to date without having to wait for a stat change.
				// like critical chance the rate of this stat changing is very low, and it's used in
				//  every damage calculation, so we want to ensure it's always accurate in our cache.
				kvp.Value.Invoke(this._statsSystem.GetStatValue(kvp.Key));
			}
		}

		protected void UnsubscribeFromStats() {
			if (this._statsSystem == null) return;

			foreach (var kvp in this._statCallbacks) {
				this._statsSystem.StatsUnsubscribe(kvp.Key, kvp.Value);
			}
		}

		protected virtual void CriticalRateCallBack(float value) => this._normalizedCriticalChance = value * 0.01f;
		protected virtual void CriticalDamageCallBack(float value) => this._normalizedCriticalDamage = 1 + value * 0.01f;


		/// <summary>
		/// Calculates the final damage after applying critical hit modifiers. The baseScalingStat parameter is
		/// the result of the GetDamageValue methods, which already takes into account the relevant stats
		/// and multipliers for the attack. This method then applies the critical hit chance and damage 
		/// modifiers to determine the final damage output. If a critical hit occurs (determined by comparing
		/// a random value to the normalized critical chance), the base damage is multiplied by the normalized
		/// critical damage multiplier. Otherwise, the base damage is returned as is. This separation of 
		/// concerns allows for flexible damage calculation while keeping critical hit logic centralized in one method.
		/// </summary>
		/// <param name="baseScalingStat"></param>
		/// <returns></returns>
		private float CalculateDamage(float baseScalingStat) {
			// why making the two if checks for 0 and 1? why not just do the random check and let the math work it out?
			// well if the critical chance is 0, we can skip the random check and just return the base damage, 
			// which is a common case and can save some performance by avoiding unnecessary random number generation 
			// and comparison.
			// similarly, if the critical chance is 1 (which is effectively a guaranteed crit), we can skip the
			// random check and directly apply the critical damage multiplier, which is also a common case for 
			// testing and certain game mechanics, and can save performance as well.
			// SO IN A NUTSHELL, WE ARE NOT CRATING A RANDOM OBJECT FOR RANDOM VALUE GENERATION AND WE ARE 
			// NOT PERFORMING A RANDOM CHECK UNNECESSARILY, WHICH CAN IMPROVE PERFORMANCE IN COMMON 
			// SCENARIOS WHERE CRIT CHANCE IS 0 OR 100%.
			if (this._normalizedCriticalChance <= 0f) return baseScalingStat;
			if (this._normalizedCriticalChance >= 1f) return baseScalingStat * this._normalizedCriticalDamage;
			if (UnityEngine.Random.value <= this._normalizedCriticalChance) {
				return baseScalingStat * this._normalizedCriticalDamage;
			}
			return baseScalingStat;
		}

		// this method will be later refactored to work with ,
		// with the new damage calculation system, but for now it serves as a placeholder for performing the attack action.
		// but we will still use this call to perform normal attacks, but ability part is delegated to the ability system,
		// so this method will only be responsible for performing the attack and triggering the animation
		// and damage calculation, while the ability system will handle the specific logic for different 
		// types of attacks and their effects.


		private bool CanPerformAttack() =>
			this._animationComponent.CanTransitionToNextAnimation(
				this.equippedWeapDataSO.CurrentWeaponData.AttackAnimationHash
			);


	}
}
