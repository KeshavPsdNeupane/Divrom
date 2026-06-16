using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using UnityEngine;
using ZLinq;
using Kope.Core.Types.Extensions;

#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Kope.SpriteComposer2D {
	/// <summary>
	/// Generic Sprite Animation Library Definition.
	/// Used to manage sprite overrides for different parts based on gender, race, and color permutation and part type.
	/// TGender: Any Enum representing gender can be used here.
	/// TRace: Any Enum representing race can be used here.
	/// TColorPermutation: Any Enum representing color permutation can be used here.
	/// TPart: Any Enum representing parts can be used here.
	/// but 0 in the enum should represent 'none' or 'undefined' state.
	/// since 0 is being used as default value. for editing convenience and validation.
	/// other values should represent valid parts.
	/// All the children of this class should implement the abstract methods for validation.
	/// and runtime applicability checks.
	/// </summary>
	/// <typeparam name="TGender"></typeparam>
	/// <typeparam name="TRace"></typeparam>
	/// <typeparam name="TColorPermutation"></typeparam>
	/// <typeparam name="TPart"></typeparam>

	public abstract class SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TPart>
	: ScriptableObject where TGender : System.Enum
	where TRace : System.Enum
	where TColorPermutation : System.Enum
	where TPart : System.Enum {
		[SerializeField] protected SpriteLibraryAsset spriteLibraryAsset;
		[SerializeField] protected string variantName;
		[SerializeField] protected TGender applicableGender = default;
		[SerializeField] protected TPart applicablePart = default;
		[SerializeField] protected TColorPermutation applicableColorPermutation = default;

		// only for editor selection convenience, not used at runtime, hashset used for runtime checks
		[SerializeField] protected List<TRace> applicableRaces = new() { default };

		protected HashSet<TRace> _applicableRacesSet; // for faster lookup and caching

		private string _cachedId;

		public TPart ApplicablePart => applicablePart;
		public virtual string LibraryId {
			get {
				if (string.IsNullOrEmpty(this._cachedId)) {
					this._cachedId = this.applicableGender.ToIdPart() + "_" +
									this.applicablePart.ToIdPart() + "_" +
									this.variantName + "_" + this.applicableColorPermutation.ToIdPart();
				}
				return this._cachedId;
			}
		}


		/// <summary>
		/// Validate the asset configuration
		/// </summary>
		protected virtual void OnValidate() {

#if UNITY_EDITOR
			// Skip this warning if the object is still prefab default or not yet serialized
			if (PrefabUtility.IsPartOfPrefabAsset(this)) return;
#endif
			this._applicableRacesSet = this.applicableRaces.AsValueEnumerable().ToHashSet();

			if (EqualityComparer<TGender>.Default.Equals(this.applicableGender, default)) {
				Debug.LogWarning($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableGender set to 'none'");
			}
			if (EqualityComparer<TColorPermutation>.Default.Equals(this.applicableColorPermutation, default)) {
				Debug.LogWarning($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableColorPermutation set to 'none'");
			}
			if ((this._applicableRacesSet.Count == 1 && this._applicableRacesSet.AsValueEnumerable().First().Equals(default)) ||
			 this._applicableRacesSet.Contains(default)) {
				Debug.Log("races = " + string.Join(", ", this.applicableRaces));
				Debug.LogWarning($"SpriteAnimationLibraryAssetDefinition '{this.name}' has no applicableRaces defined or has 'none' in the list");
			}

			this._cachedId = null;
			if (this.applicablePart.Equals(default)) {
				Debug.LogWarning($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicablePart set to 'none'");
			}
		}

		/// <summary>
		/// Unique ID for this library definition
		/// Useful for lookups and caching while reading from disk using Ad dressables
		/// </summary>


		protected virtual bool IsApplicable(TGender gender, TPart tpart, TRace race) {
			// Lazy initialize the hashset here, so child classes don't have to worry about it
			// since the ??= operator makes sure its only initialized once
			this._applicableRacesSet ??= new HashSet<TRace>(applicableRaces);

			bool genderOk = GenderOk(gender);
			bool partOk = PartOk(tpart);
			bool raceOk = RaceOk(race);

			if (!genderOk) Debug.LogError($"Gender mismatch: {gender} != {this.applicableGender} on library {this.LibraryId}");
			if (!partOk) Debug.LogError($"EquipingPart mismatch: {tpart} != {this.applicablePart} on library {this.LibraryId}");
			if (!raceOk) Debug.LogError($"Race mismatch: {race} not in {string.Join(", ", this.applicableRaces)} on library {this.LibraryId}");

			return genderOk && partOk && raceOk;
		}

		public virtual bool TryGetResolvedLibrary(
			TGender gender,
			TPart tpart,
			TRace race,
			out SpriteLibraryAsset lib
		) {
			lib = null;
			if (this.spriteLibraryAsset == null) return false;
			if (!IsApplicable(gender, tpart, race)) return false;

			lib = this.spriteLibraryAsset;
			return true;
		}

		/// <summary>
		/// Checks if the given gender is applicable for this asset.
		/// Must be implemented by child classes.
		/// </summary>
		/// <param name="gender"></param>
		/// <returns></returns>
		protected abstract bool GenderOk(TGender gender);

		/// <summary>
		/// Checks if the given race is applicable for this asset.
		/// Must be implemented by child classes.
		/// </summary>
		/// <param name="race"></param>
		/// <returns></returns>
		protected abstract bool RaceOk(TRace race);


		/// <summary>
		/// Checks if the given part is applicable for this asset.
		/// Can be overridden by child classes for custom logic.
		/// </summary>
		/// <param name="tpart"></param>
		/// <returns></returns>
		protected virtual bool PartOk(TPart tpart) {
			if (EqualityComparer<TPart>.Default.Equals(tpart, default)) return false;
			return EqualityComparer<TPart>.Default.Equals(this.applicablePart, tpart);
		}

	}

}