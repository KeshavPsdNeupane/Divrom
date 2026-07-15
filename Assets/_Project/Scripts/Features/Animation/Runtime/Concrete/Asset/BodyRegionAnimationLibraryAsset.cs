using UnityEngine;
using Kope.SpriteComposer2D;
using Kope.EntityIdentity;
/// <summary>
/// make sure if the new parts are added, they are in ascending order
/// and spaced by 100s to allow future insertions.
/// same part groups should be clustered together seperated 1s.
/// </summary>
public enum BodyRegionEnum : short {
	PRIMARYHAIR = 0,
	SECONDARYHAIR = 1,
	HEAD = 100,
	EAR = 200,
	BODY = 300,
	TAIL = 400,
	SHADOW = 500,
	WING = 600
}

[CreateAssetMenu(fileName = "New Base Body Animation Library", menuName = "Animation/BodyRegionAsset")]
public class BodyRegionAnimationLibraryAsset
 : SpriteAnimationLibraryAssetDefinition<GenderEnum, RaceEnum, ItemColorPermutationEnum, BodyRegionEnum> {
	protected override bool GenderOk(GenderEnum gender) {
		return this.applicableGender == GenderEnum.NEUTRAL || this.applicableGender == gender;
	}
	protected override bool RaceOk(RaceEnum race) {
		// Lazy Initialization: is already handled in the base class IsApplicable method
		return this._applicableRacesSet.Contains(RaceEnum.All) || this._applicableRacesSet.Contains(race);
	}
}
