using Kope.Core.CompilerServices;
using UnityEngine;

namespace Kope.ModularSpriteAnimation.Runtime
{
    public enum BodyRegionEnum
    {
        none = -1,
        hair = 0,
        head = 1,
        ear = 2,
        body = 3,
        tail = 4,
        shadow = 5,
        wings = 6
    }

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
                MyLogger.Warn($"BodyRegionAnimationLibraryAsset '{this.name}' has applicableBaseBody set to 'none'");
            }
        }

        protected override bool IsApplicable<TPart>(GenderEnum gender, TPart tpart, RacesEnum race)
        {

            bool genderOk = GenderOk(gender);
            bool partOk = PartOk(tpart);
            bool raceOk = RaceOk(race);

            if (!genderOk) MyLogger.Error($"Gender mismatch: {gender} != {this.applicableGender} on library {this.LibraryId}");
            if (!partOk) MyLogger.Error($"BodyRegion mismatch: {tpart} != {this.applicableBaseBody} on library {this.LibraryId}");
            if (!raceOk) MyLogger.Error($"Race mismatch: {race} not in {string.Join(", ", this.applicableRaces)} on library {this.LibraryId}");

            return genderOk && partOk && raceOk;
        }

        protected override bool PartOk<TPart>(TPart tpart)
        {
            return tpart is BodyRegionEnum part
            && (part != BodyRegionEnum.none) && part == this.applicableBaseBody;
        }
    }
}