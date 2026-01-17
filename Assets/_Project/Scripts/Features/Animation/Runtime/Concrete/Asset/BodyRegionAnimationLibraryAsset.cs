using UnityEngine;
using Kope.SpriteComposer2D;

/// <summary>
/// make sure if the new parts are added, they are in ascending order
/// and spaced by 100s to allow future insertions.
/// same part groups should be clustered together seperated 1s.
/// </summary>
public enum BodyRegionEnum : short
{
    none = 0,
    primaryhair = 1,
    secondaryhair = 2,
    head = 100,
    ear = 200,
    body = 300,
    tail = 400,
    shadow = 500,
    wings = 600
}

[CreateAssetMenu(fileName = "New Base Body Animation Library", menuName = "Animation/BodyRegionAsset")]
public class BodyRegionAnimationLibraryAsset
 : SpriteAnimationLibraryAssetDefinition<GenderEnum, RacesEnum, ItemColorPermutationEnum, BodyRegionEnum>
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
