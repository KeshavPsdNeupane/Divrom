using UnityEngine;


public enum BodyRegionEnum { none = -1, hair = 0, head = 1, ear = 2, body = 3, tail = 4, }

[CreateAssetMenu(fileName = "New Base Body Animation Library", menuName = "Animation/BodyRegionAsset")]
public class BodyRegionAnimationLibraryAsset : SpriteAnimationLibraryAssetDefinition
{

    [SerializeField] private BodyRegionEnum applicableBaseBody = BodyRegionEnum.none;
    public BodyRegionEnum ApplicableBaseBody => this.applicableBaseBody;
    private string _cachedId;

    public override string LibraryId
    {
        get
        {
            if (string.IsNullOrEmpty(_cachedId))
            {
                _cachedId = this.applicableGender.ToIdPart() + "_" +
                            this.applicableBaseBody.ToIdPart() + "_" +
                            this.variantName + "_" + this.applicableColorPermutation.ToIdPart();
            }
            return _cachedId;
        }
    }

    override protected void OnValidate()
    {
        base.OnValidate();
        this._cachedId = null;
        if (this.applicableBaseBody == BodyRegionEnum.none)
        {
            Logger.Warn($"BodyRegionAnimationLibraryAsset '{this.name}' has applicableBaseBody set to 'none'");
        }
    }

    protected override bool IsApplicable<TPart>(GenderEnum gender, TPart tpart, RacesEnum race)
    {

        bool genderOk = gender != GenderEnum.none
        && (applicableGender == GenderEnum.both || applicableGender == gender);

        bool partOk = tpart is BodyRegionEnum part
        && (part != BodyRegionEnum.none) && part == this.applicableBaseBody;

        bool raceOk = race != RacesEnum.none && (applicableRaces.Contains(RacesEnum.All) ||
         applicableRaces.Contains(race));
        if (!genderOk) Logger.Error($"Gender mismatch: {gender} != {this.applicableGender} on library {this.LibraryId}");
        if (!partOk) Logger.Error($"BodyRegion mismatch: {tpart} != {this.applicableBaseBody} on library {this.LibraryId}");
        if (!raceOk) Logger.Error($"Race mismatch: {race} not in {string.Join(", ", this.applicableRaces)} on library {this.LibraryId}");

        return genderOk && partOk && raceOk;
    }
}
