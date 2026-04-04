using Kope.Core.CompilerServices;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Actors/Weapon")]
public class WeaponSO : ScriptableObject {
	[SerializeField] private string weaponName = "Bare Hands";
	[SerializeField] private WeaponType weaponType = WeaponType.Bare;

	[Tooltip("This overrides the default animation mapping for this weapon. You can leave this as None to use the default mapping.")]
	[SerializeField] private AnimationState overrideAttackAnimation = AnimationState.None;

	[SerializeField] private float attackSpeed = 1.0f;

	public string WeaponName => CurrentWeaponData.WeaponName;
	public WeaponType WeaponType => CurrentWeaponData.WeaponType;
	public AnimationState PrimaryAttackAnimation => CurrentWeaponData.PrimaryAttackAnimation;
	public float AttackSpeed => CurrentWeaponData.AttackSpeed;
	private WeaponData weaponDataCache = null;

	public WeaponData CurrentWeaponData {
		get {
			weaponDataCache ??= new WeaponData(weaponName, weaponType, attackSpeed, overrideAttackAnimation);
			return weaponDataCache;
		}
	}

#if UNITY_EDITOR
	private void OnValidate() {
		if (!WeaponAnimationMapper.IsValidAttackAnimation(this.overrideAttackAnimation) &&
			this.overrideAttackAnimation != AnimationState.None) {
			MyLogger.Warn(
			 $"WeaponSO '{this.weaponName}' has an override attack animation '{this.overrideAttackAnimation}'\n" +
			 "that is not a valid attack animation.\n" +
			 "Consider removing the override (set to None) to use the default mapping, or choose a valid attack animation.\n" +
			 $"Current valid attack animations are: {string.Join(", ", WeaponAnimationMapper.onlyAttackAnimations)}"
		 );
		}
		this.weaponDataCache = new WeaponData(weaponName, weaponType, attackSpeed, overrideAttackAnimation);
	}
#endif

}

