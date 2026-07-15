using UnityEngine;
using Kope.SpriteComposer2D;
using Kope.EntityIdentity;


/// <summary>
/// Example Enum for different equipment parts.
/// 
/// Here put all different part on the group of 100s so that we can easily add new parts in between later.
/// For example, if we want to add "gloves" later, we can put it at 250 without breaking existing numbering.
/// and similar grouping will be as 1s difference like helmale = 0 so helmate1 =1 and similarly for rest.
/// </summary>
public enum EquipmentPartEnum : short {
	HELMET = 0,
	NECKLACE = 100,
	ARM = 200,
	TORSO = 300,
	LEG = 400,
	FEET = 500,
	WEAPON = 600
}

/// <summary>
/// Example Animation Library Asset for different equipment parts.
/// Makes use of the generic SpriteAnimationLibraryAssetDefinition class.
/// 
/// </summary>
[CreateAssetMenu(fileName = "New Animation Library", menuName = "Animation/EquipmentAsset")]
public class EquipmentAnimationLibraryAsset
: SpriteAnimationLibraryAssetDefinition<GenderEnum, RaceEnum, ItemColorPermutationEnum, EquipmentPartEnum> {
	protected override bool GenderOk(GenderEnum gender) {
		return this.applicableGender == GenderEnum.NEUTRAL || this.applicableGender == gender;
	}

	protected override bool RaceOk(RaceEnum race) {
		// Lazy Initialization: is already handled in the base class IsApplicable method
		return this._applicableRacesSet.Contains(RaceEnum.All) || this._applicableRacesSet.Contains(race);
	}
}
