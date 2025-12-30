using UnityEngine;


public enum EquipingPartEnum { helmet, neck, arm, torso, leg, feet, weapon, none }


[CreateAssetMenu(fileName = "New Animation Library", menuName = "Animation/EquipmentAsset")]
public class EquipmentAnimationLibraryAsset : SpriteAnimationLibraryAssetDefinition
{
    [SerializeField] private EquipingPartEnum applicableEquipingPart;

    public EquipingPartEnum ApplicableEquipingPart => applicableEquipingPart;

    public override string LibraryId => $"{applicableGender}_{applicableEquipingPart}_{variantName}_{applicableColorPermutation}";
    protected override bool IsApplicable<TPart>(GenderEnum gender, TPart tpart, RacesEnum race)
    {
        bool genderOk = applicableGender == GenderEnum.both || applicableGender == gender;
        bool partOk = tpart is EquipingPartEnum part && part == applicableEquipingPart;

        bool raceOk = applicableRaces.Contains(RacesEnum.All) || applicableRaces.Contains(race);

        if (!genderOk) Logger.Error($"Gender mismatch: {gender} != {applicableGender}");
        if (!partOk) Logger.Error($"EquipingPart mismatch: {tpart} != {applicableEquipingPart}");
        if (!raceOk) Logger.Error($"Race mismatch: {race} not in {string.Join(", ", applicableRaces)}");

        return genderOk && partOk && raceOk;
    }
}
