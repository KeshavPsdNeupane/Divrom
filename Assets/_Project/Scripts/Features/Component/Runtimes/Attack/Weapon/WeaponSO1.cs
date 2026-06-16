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
				if (this._dataCache != default) return this._dataCache;

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
					animInstance.InternalValue,
					animInstance.Alias
				);
				return this._dataCache;
			}
		}

		private void OnValidate() {
			this._dataCache = null;
			_ = this.weaponType.ValidateTheInternal(this);

			if (this.attackAnimationMap == null) return;

			if (!this.attackAnimationMap.ValidateSourceOrTarget(this.weaponType.Source, true, this)) return;

			bool hasOverride = this.overrideAttackAnimation.Source != null;

			if (hasOverride && this.attackAnimationMap.ValidateSourceOrTarget(this.overrideAttackAnimation.Source, false, this)) {
				var overrideInstance = this.overrideAttackAnimation.GetInstance();
				if (overrideInstance != null && this.attackAnimationMap.IsExcluded(overrideInstance.InternalValue)) {
					Debug.LogError($"[{this.name}] Override {overrideInstance.Alias} animation is excluded in map.", this);
				}
			}
			// no need to check if the weapon type exist in the map or not this since the 
			// EnumToEnumDrawer already forces all source values to
			// be mapped to something,
		}
	}
}