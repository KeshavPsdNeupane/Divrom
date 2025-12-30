using UnityEngine;


public enum EquipingPartEnum { none = -1, helmet = 0, neck = 1, arm = 2, torso = 3, leg = 4, feet = 5, weapon = 6 }


[CreateAssetMenu(fileName = "New Animation Library", menuName = "Animation/EquipmentAsset")]
public class EquipmentAnimationLibraryAsset : SpriteAnimationLibraryAssetDefinition
{
    [SerializeField] private EquipingPartEnum applicableEquipingPart;

    public EquipingPartEnum ApplicableEquipingPart => applicableEquipingPart;

    public override string LibraryId => $"{applicableGender}_{applicableEquipingPart}_{variantName}_{applicableColorPermutation}";

    override protected void OnValidate()
    {
        base.OnValidate();
        if (this.applicableEquipingPart == EquipingPartEnum.none)
        {
            Logger.Warn($"EquipmentAnimationLibraryAsset '{this.name}' has applicableEquipingPart set to 'none'");
        }
    }

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
