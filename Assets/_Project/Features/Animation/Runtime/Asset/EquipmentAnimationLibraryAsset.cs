using Kope.Core.CompilerServices;
using UnityEngine;

namespace Kope.ModularSpriteAnimation
{
    /// <summary>
    /// Here put all different part on the group of 100s so that we can easily add new parts in between later.
    /// For example, if we want to add "gloves" later, we can put it at 250 without breaking existing numbering.
    /// and similar grouping will be as 1s difference like helmale = 0 so helmate1 =1 and similarly for rest.
    /// </summary>
    public enum EquipmentPartEnum : short
    {

        none = -1,
        helmet = 0,
        necklace = 100,
        arm = 200,
        torso = 300,
        leg = 400,
        feet = 500,
        weapon = 600
    }


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
                MyLogger.Warn($"EquipmentAnimationLibraryAsset '{this.name}' has applicableEquipingPart set to 'none'");
            }
        }

        protected override bool IsApplicable<TPart>(GenderEnum gender, TPart tpart, RacesEnum race)
        {
            bool genderOk = GenderOk(gender);
            bool partOk = PartOk(tpart);
            bool raceOk = RaceOk(race);

            if (!genderOk) MyLogger.Error($"Gender mismatch: {gender} != {applicableGender} on library {this.LibraryId}");
            if (!partOk) MyLogger.Error($"EquipingPart mismatch: {tpart} != {applicableEquipingPart} on library {this.LibraryId}");
            if (!raceOk) MyLogger.Error($"Race mismatch: {race} not in {string.Join(", ", applicableRaces)} on library {this.LibraryId}");

            return genderOk && partOk && raceOk;
        }

        protected override bool PartOk<TPart>(TPart tpart)
        {
            return tpart is EquipmentPartEnum part
            && (part != EquipmentPartEnum.none) && part == this.applicableEquipingPart;
        }
    }
}