using UnityEngine;
using Kope.SpriteComposer2D;



/// <summary>
/// Example Enum for different equipment parts.
/// 
/// Here put all different part on the group of 100s so that we can easily add new parts in between later.
/// For example, if we want to add "gloves" later, we can put it at 250 without breaking existing numbering.
/// and similar grouping will be as 1s difference like helmale = 0 so helmate1 =1 and similarly for rest.
/// </summary>
public enum EquipmentPartEnum : short
{
    none = 0,
    helmet = 1,
    necklace = 100,
    arm = 200,
    torso = 300,
    leg = 400,
    feet = 500,
    weapon = 600
}

/// <summary>
/// Example Animation Library Asset for different equipment parts.
/// Makes use of the generic SpriteAnimationLibraryAssetDefinition class.
/// 
/// </summary>
[CreateAssetMenu(fileName = "New Animation Library", menuName = "Animation/EquipmentAsset")]
public class EquipmentAnimationLibraryAsset
: SpriteAnimationLibraryAssetDefinition<GenderEnum, RacesEnum, ItemColorPermutationEnum, EquipmentPartEnum>
{
    protected override bool GenderOk(GenderEnum gender)
    {
        return gender != GenderEnum.none
            && (this.applicableGender == GenderEnum.both || this.applicableGender == gender);
    }

    protected override bool RaceOk(RacesEnum race)
    {
        if (race == RacesEnum.none) return false;
        // Lazy Initialization: is already handled in the base class IsApplicable method
        return this._applicableRacesSet.Contains(RacesEnum.All) || this._applicableRacesSet.Contains(race);
    }
}
