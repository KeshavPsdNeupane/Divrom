using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Component.Attack {
	[CreateAssetMenu(fileName = "NewWeapon", menuName = "Scriptable Objects/WeaponSO1")]
	public class WeaponSO1 : ScriptableObject {
		[SerializeField] private string weaponName = "Bare Hands";
		[SerializeField] private EnumPicker weaponType;

		[SerializeField, Tooltip("Maps weapon type to its default attack animation.")]
		private EnumToEnumMap attackAnimationMap;

		[SerializeField, Tooltip("Optional override animation.")]
		private EnumPicker overrideAttackAnimation;

		[SerializeField] private float attackSpeed = 1.0f;

		private WeaponData1 _dataCache;

		public WeaponData1 CurrentWeaponData {
			get {
				if (this._dataCache != null) return this._dataCache;

				var weaponTypeInstance = this.weaponType.GetInstance();
				var weaponTypeID = weaponTypeInstance.InternalValue;

				bool hasOverride = this.overrideAttackAnimation.Source != null;
				var animInstance = hasOverride
					? this.overrideAttackAnimation.GetInstance()
					: this.attackAnimationMap.GetTargetInstance(weaponTypeID);

				this._dataCache = new WeaponData1(
					this.weaponName,
					weaponTypeInstance,
					this.attackSpeed,
					animInstance.Alias
				);
				return this._dataCache;
			}
		}

		private void OnValidate() {
			this._dataCache = null;

			// Major validation only

			if (this.weaponType.Source == null) {
				Debug.LogWarning($"[{this.name}] Weapon type not assigned.");
				return;
			}

			if (this.attackAnimationMap == null) {
				Debug.LogWarning($"[{this.name}] Attack animation map missing.");
				return;
			}

			if (this.attackAnimationMap.Source != this.weaponType.Source) {
				Debug.LogWarning($"[{this.name}] Weapon type and attack map source mismatch.");
				return;
			}

			if (this.overrideAttackAnimation.Source != null &&
				this.overrideAttackAnimation.Source != this.attackAnimationMap.Target) {
				Debug.LogWarning($"[{this.name}] Override animation enum mismatch with map target.");
				return;
			}

			bool hasOverride = this.overrideAttackAnimation.Source != null;

			if (hasOverride) {
				var overrideInstance = this.overrideAttackAnimation.GetInstance();
				if (overrideInstance == null) {
					Debug.LogWarning($"[{this.name}] Override animation not selected.");
					return;
				}

				if (this.attackAnimationMap.IsExcluded(overrideInstance.InternalValue)) {
					Debug.LogError($"[{this.name}] Override animation is excluded in map.");
					return;
				}
			} else {
				var instance = this.weaponType.GetInstance();
				var weaponTypeID = instance.InternalValue;

				if (!this.attackAnimationMap.IsMapped(weaponTypeID)) {
					Debug.LogWarning($"[{this.name}] Weapon type not mapped in attack animation map.");
					return;
				}
			}
		}
	}
}