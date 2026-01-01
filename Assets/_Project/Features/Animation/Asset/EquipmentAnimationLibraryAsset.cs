using UnityEngine;


public enum EquipmentPartEnum { none = -1, helmet = 0, neck = 1, arm = 2, torso = 3, leg = 4, feet = 5, weapon = 6 }


[CreateAssetMenu(fileName = "New Animation Library", menuName = "Animation/EquipmentAsset")]
public class EquipmentAnimationLibraryAsset : SpriteAnimationLibraryAssetDefinition
{
    [SerializeField] private EquipmentPartEnum applicableEquipingPart = EquipmentPartEnum.none;

    public EquipmentPartEnum ApplicableEquipingPart => applicableEquipingPart;

    private string _cachedId;

    public override string LibraryId
    {
        get
        {
            if (string.IsNullOrEmpty(_cachedId))
            {
                _cachedId = this.applicableGender.ToIdPart() + "_" +
                            this.applicableEquipingPart.ToIdPart() + "_" +
                            this.variantName + "_" + this.applicableColorPermutation.ToIdPart();
            }
            return _cachedId;
        }
    }

    override protected void OnValidate()
    {
        base.OnValidate();
        this._cachedId = null;
        if (this.applicableEquipingPart == EquipmentPartEnum.none)
        {
            Logger.Warn($"EquipmentAnimationLibraryAsset '{this.name}' has applicableEquipingPart set to 'none'");
        }
    }

    protected override bool IsApplicable<TPart>(GenderEnum gender, TPart tpart, RacesEnum race)
    {
        bool genderOk = gender != GenderEnum.none
        && (applicableGender == GenderEnum.both || applicableGender == gender);

        bool partOk = tpart is EquipmentPartEnum part
        && (part != EquipmentPartEnum.none) && part == this.applicableEquipingPart;

        bool raceOk = race != RacesEnum.none && (applicableRaces.Contains(RacesEnum.All) ||
        applicableRaces.Contains(race));

        if (!genderOk) Logger.Error($"Gender mismatch: {gender} != {applicableGender} on library {this.LibraryId}");
        if (!partOk) Logger.Error($"EquipingPart mismatch: {tpart} != {applicableEquipingPart} on library {this.LibraryId}");
        if (!raceOk) Logger.Error($"Race mismatch: {race} not in {string.Join(", ", applicableRaces)} on library {this.LibraryId}");

        return genderOk && partOk && raceOk;
    }
}
