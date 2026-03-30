using System;
using System.Collections.Generic;
using UnityEngine;
public enum WeaponType {
	Bare = -1,
	LongSword = 0,
	Sword = 1,
	Bow = 2,
	Crossbow = 3,
	Spear = 4,
	Dagger = 5,
	Staff = 6,
}

/// <summary>
/// Maps weapon types to their default animation states.
///  If a weapon has an override animation, that will be used instead.
/// 
/// </summary>


public static class WeaponAnimationMapper {
	private static readonly Dictionary<WeaponType, AnimationState> cached;
	public static readonly HashSet<AnimationState> onlyAttackAnimations = new()
	{
		AnimationState.Thrust,
		AnimationState.Swing,
		AnimationState.Shoot,
		AnimationState.Spell,
	};

	static WeaponAnimationMapper() {
		cached = new Dictionary<WeaponType, AnimationState>
		{
			{ WeaponType.Bare, AnimationState.Swing },
			{ WeaponType.LongSword, AnimationState.Swing },
			{ WeaponType.Crossbow, AnimationState.Thrust }, // Crossbow uses Thrust for attack animation,intentional 
            { WeaponType.Bow, AnimationState.Shoot },
			{ WeaponType.Staff, AnimationState.Thrust }, // Staff uses Thrust for attack animation, intentional
            { WeaponType.Sword, AnimationState.Swing },
			{ WeaponType.Dagger, AnimationState.Swing },
			{ WeaponType.Spear, AnimationState.Thrust }
		};
	}

	public static AnimationState GetAnimationType(WeaponType weaponType)
		=> cached.TryGetValue(weaponType, out var anim) ? anim : AnimationState.None;

	public static bool IsValidAttackAnimation(AnimationState animationState)
		=> onlyAttackAnimations.Contains(animationState);
}

[Serializable]
public class WeaponData {
	private readonly string weaponName;
	private readonly WeaponType weaponType;
	private readonly float attackSpeed;
	private readonly AnimationState primaryAttackAnimation;
	private readonly int primaryAttackAnimationHash;

	public string WeaponName => weaponName;
	public WeaponType WeaponType => weaponType;
	public AnimationState PrimaryAttackAnimation => primaryAttackAnimation;
	public float AttackSpeed => attackSpeed;
	public int PrimaryAttackAnimationHash => primaryAttackAnimationHash;

	public WeaponData(string weaponName, WeaponType weaponType, float attackSpeed, AnimationState overrideAnimation = AnimationState.None) {
		this.weaponName = weaponName;
		this.weaponType = weaponType;
		this.attackSpeed = attackSpeed;

		// Use override if provided; otherwise default
		this.primaryAttackAnimation = overrideAnimation != AnimationState.None
			? overrideAnimation
			: WeaponAnimationMapper.GetAnimationType(weaponType);

		// Immutable hash computed once
		this.primaryAttackAnimationHash = Animator.StringToHash(primaryAttackAnimation.ToString());
	}
}
