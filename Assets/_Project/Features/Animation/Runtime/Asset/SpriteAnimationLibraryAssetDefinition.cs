using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using UnityEngine;
using ZLinq;
using Kope.Core.CompilerServices;

namespace Kope.ModularSpriteAnimation
{
    /// <summary>
    /// "both" means this item is applicable to both genders
    /// </summary>
    public enum GenderEnum : short
    {
        none = -1,
        male,
        female,
        both,

    }

    public enum ItemColorPermutationEnum : short
    {
        none = -1,
        // default color 0 to 999
        black = 0, white = 8, lime = 1, yellow = 2, blue = 3,
        red = 4, orange = 5, brown = 6, bluegrey = 7,
        // metallic colors 1000 to 1999
        ceramic = 1000, gold = 1001, silver = 1002, bronze = 1003, steel = 1004,
        iron = 1005, wood = 1006, copper = 1007,
        // natural colors 2000 to 2999
        leather = 2000, sandy = 2001, ginger = 2002,

    }

    /// <summary>
    /// "All" means this item is applicable to all races,
    /// other values are grouped by race types with gaps for future additions
    /// 
    /// </summary>
    public enum RacesEnum : short
    {
        none = -1,
        All = 9999,
        // the listed enum are just the common varient for each race type

        // Humans (0 - 499), combineable with half-humans so total is 0 - 999
        // seperation is 10 since all are almost same termology
        human = 0,
        barbarian = 10,
        //half-humans (500 - 999)
        halfelf = 500,
        halfwolf = 510,
        halfcat = 520,

        // Humanoids (1000 - 1999)
        // seperation is 20 since the enum name is board based on race type
        // with this we can add like 50 total races in this category without conflict
        // and can add like 19 subraces to each 50 races if needed,

        elf = 1000,
        orc = 1020,
        goblin = 1040,
        troll = 1060,
        lizard = 1080,

        // angels / Light (2000 - 2999)
        // same resperation as above
        angel = 2000,
        spirit = 2020,
        fairy = 2040,

        // Demons / Dark races (3000 - 3999)
        // same resperation as above
        demon = 3000,
        vampire = 3020,
        werewolf = 3040,
        undead = 3060,
    }



    public abstract class SpriteAnimationLibraryAssetDefinition : ScriptableObject
    {
        [SerializeField] protected string variantName;
        [SerializeField] protected GenderEnum applicableGender = GenderEnum.none;
        [SerializeField] protected ItemColorPermutationEnum applicableColorPermutation = ItemColorPermutationEnum.none;
        [SerializeField] protected SpriteLibraryAsset spriteLibraryAsset;

        // only for editor selection convenience, not used at runtime, hashset used for runtime checks
        [SerializeField] protected List<RacesEnum> applicableRaces = new() { RacesEnum.none };

        private HashSet<RacesEnum> _applicableRacesSet; // for faster lookup and caching

        public string VariantName => this.variantName;

        /// <summary>
        /// Validate the asset configuration
        /// </summary>
        protected virtual void OnValidate()
        {
            this._applicableRacesSet = this.applicableRaces.AsValueEnumerable().ToHashSet();

            if (this.applicableGender == GenderEnum.none)
            {
                MyLogger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableGender set to 'none'");
            }
            if (this.applicableColorPermutation == ItemColorPermutationEnum.none)
            {
                MyLogger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableColorPermutation set to 'none'");
            }
            if ((this._applicableRacesSet.Count == 1 && this._applicableRacesSet.AsValueEnumerable().First() == RacesEnum.none) ||
             this._applicableRacesSet.Contains(RacesEnum.none))
            {
                MyLogger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has no applicableRaces defined");

            }
        }

        /// <summary>
        /// Unique ID for this library definition
        /// Useful for lookups and caching while reading from disk using Addressables
        /// </summary>
        public abstract string LibraryId { get; }


        protected abstract bool IsApplicable<TPart>(
            GenderEnum gender,
            TPart tpart,
            RacesEnum race
        ) where TPart : System.Enum;

        public virtual bool TryGetResolvedLibrary<TPart>(
            GenderEnum gender,
            TPart tpart,
            RacesEnum race,
            out SpriteLibraryAsset lib
        ) where TPart : System.Enum
        {
            lib = null;
            if (this.spriteLibraryAsset == null) return false;
            if (!IsApplicable(gender, tpart, race)) return false;

            lib = this.spriteLibraryAsset;
            return true;
        }


        protected bool GenderOk(GenderEnum gender)
        {
            return gender != GenderEnum.none
            && (this.applicableGender == GenderEnum.both || this.applicableGender == gender);
        }
        protected bool RaceOk(RacesEnum race)
        {
            if (race == RacesEnum.none) return false;

            // LAZY INITIALIZATION: 
            // If the set is null (first time use after load/compile), build it from the list.
            this._applicableRacesSet ??= new HashSet<RacesEnum>(applicableRaces);

            return this._applicableRacesSet.Contains(RacesEnum.All) || this._applicableRacesSet.Contains(race);
        }
        protected abstract bool PartOk<TPart>(TPart tpart) where TPart : System.Enum;

    }

}