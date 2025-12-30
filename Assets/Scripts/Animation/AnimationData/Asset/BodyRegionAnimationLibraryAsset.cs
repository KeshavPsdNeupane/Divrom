using UnityEngine;


public enum BodyRegionEnum { head, body, ear, hair, tail, none }

[CreateAssetMenu(fileName = "New Base Body Animation Library", menuName = "Animation/BodyRegionAsset")]
public class BodyRegionAnimationLibraryAsset : SpriteAnimationLibraryAssetDefinition
{

    [SerializeField] private BodyRegionEnum applicableBaseBody = BodyRegionEnum.none;
    public BodyRegionEnum ApplicableBaseBody => this.applicableBaseBody;

    public override string LibraryId =>
        $"{applicableGender}_{applicableBaseBody}_{variantName}_{applicableColorPermutation}";



    protected override bool IsApplicable<TPart>(GenderEnum gender, TPart tpart, RacesEnum race)
    {
        bool genderOk = this.applicableGender == GenderEnum.both || this.applicableGender == gender;
        bool partOk = tpart is BodyRegionEnum part && part == applicableBaseBody;
        bool raceOk = this.applicableRaces.Contains(RacesEnum.All) || this.applicableRaces.Contains(race);

        if (!genderOk) Logger.Error($"Gender mismatch: {gender} != {this.applicableGender}");
        if (!partOk) Logger.Error($"BodyRegion mismatch: {tpart} != {this.applicableBaseBody}");
        if (!raceOk) Logger.Error($"Race mismatch: {race} not in {string.Join(", ", this.applicableRaces)}");

        return genderOk && partOk && raceOk;
    }
}
