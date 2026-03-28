using Kope.Core.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentSystem;
/// <summary>
/// Base attack logic component. Can be used for both player and AI.
/// Handles stat subscription and damage calculation.
/// </summary>
public abstract class AttackComponentBase : InitializableBase {
	[SerializeField] private EntityComponentsRegistry ecr;
	[SerializeField] private WeaponSO equippedWeaponDataSO;
	private AnimationComponentBase animationComponent;
	protected CharacterStatsSystem statsSystem;
	protected float attack;
	protected float normalizedCriticalChance;
	protected float normalizedCriticalDamage;

	public WeaponData EquippedWeaponData => this.equippedWeaponDataSO.CurrentWeaponData;

	public event UnityAction OnAttackPerformed;

	protected override bool OnInit() {
		if (ecr == null) {
			MyLogger.Error("EntityComponentStore reference is missing in AttackComponentBase." +
			GetParentGameObjectHeirarchyMessage());
			return false;
		}
		if (this.ecr.ComponentRegistry.TryGetMutatableComponent(out AnimationComponentBase animComp)) {
			this.animationComponent = animComp;
		} else {
			MyLogger.Error("AnimationComponentBase not found in EntityComponentStore." +
			GetParentGameObjectHeirarchyMessage());
			return false;
		}

		if (this.ecr.ComponentRegistry.TryGetMutatableComponent(out CharacterStatsSystem statsSys)) {
			this.statsSystem = statsSys;
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
		if (!IsInitialized || statsSystem == null) return;
		if (statsSystem != null && statsSystem.CurrentStats != null) {
			statsSystem.StatsSubscribe(CharacterStatType.ATK, AttackCallback);
			statsSystem.StatsSubscribe(CharacterStatType.CRATE, CriticalRateCallBack);
			statsSystem.StatsSubscribe(CharacterStatType.CDMG, CriticalDamageCallBack);

			// Initial fetch
			AttackCallback(statsSystem.GetStatValue(CharacterStatType.ATK));
			CriticalRateCallBack(statsSystem.GetStatValue(CharacterStatType.CRATE));
			CriticalDamageCallBack(statsSystem.GetStatValue(CharacterStatType.CDMG));
		}
	}

	protected void UnsubscribeFromStats() {
		if (statsSystem != null && statsSystem.CurrentStats != null) {
			statsSystem.StatsUnsubscribe(CharacterStatType.ATK, AttackCallback);
			statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, CriticalRateCallBack);
			statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, CriticalDamageCallBack);
		}
	}

	protected virtual void AttackCallback(float value) => this.attack = value;
	protected virtual void CriticalRateCallBack(float value) => this.normalizedCriticalChance = value * 0.01f;
	protected virtual void CriticalDamageCallBack(float value) => this.normalizedCriticalDamage = 1 + value * 0.01f;

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
		return CalculateDamage(this.attack);
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
		if (this.normalizedCriticalChance >= 1f) return damage * this.normalizedCriticalDamage;

		if (Random.value < this.normalizedCriticalChance) return damage * this.normalizedCriticalDamage;
		return damage;
	}

	public void PerformAttack() {
		if (!CanPerformAttack()) return;
		PerformAttackInternal();
		RaiseOnAttackPerformedEvent();
	}


	private bool CanPerformAttack() {
		return this.animationComponent.CanTransitionToAnimation(EquippedWeaponData.PrimaryAttackAnimationHash);
	}

	/// <summary>
	/// Hook for subclasses to implement the actual attack logic, such as playing animations, 
	/// applying damage to targets, etc.
	/// </summary>
	protected abstract void PerformAttackInternal();

	protected void RaiseOnAttackPerformedEvent() {
		OnAttackPerformed?.Invoke();
	}
}
