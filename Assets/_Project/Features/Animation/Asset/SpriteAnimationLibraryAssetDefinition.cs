using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
    black = 0, lime = 1, yellow = 2, blue = 3, red = 4,
    orange = 5, brown = 6, bluegrey = 7,
    // metallic colors 1000 to 1999
    ceramic = 1000, gold = 1001, silver = 1002, bronze = 1003, steel = 1004,
    iron = 1005,
    // natural colors 2000 to 2999
    leather = 2000, shandy = 2001

}

/// <summary>
/// "All" means this item is applicable to all races
/// </summary>
public enum RacesEnum
{
    none = -1,
    human = 1, elf = 2, vampire = 3, werewolf = 4, orc = 5,
    goblin = 6, dragonborn = 7, troll = 8, undead = 9, halfwolf = 10,
    halfcat = 11, halfelf = 12, lizard = 13,
    All = 999,
}


public abstract class SpriteAnimationLibraryAssetDefinition : ScriptableObject
{
    [SerializeField] protected string variantName;
    [SerializeField] protected GenderEnum applicableGender = GenderEnum.none;
    [SerializeField] protected ItemColorPermutationEnum applicableColorPermutation = ItemColorPermutationEnum.none;
    [SerializeField] protected SpriteLibraryAsset spriteLibraryAsset;
    // ONLY for validation and editor purposes will be converted to HashSet for runtime use for performance 
    [SerializeField] protected List<RacesEnum> applicableRaces = new() { RacesEnum.none };
    private HashSet<RacesEnum> _applicableRacesSet; // for faster lookup and caching

    public string VariantName => this.variantName;

    /// <summary>
    /// Validate the asset configuration
    /// </summary>
    protected virtual void OnValidate()
    {
        this._applicableRacesSet = this.applicableRaces.ToHashSet();

        if (this.applicableGender == GenderEnum.none)
        {
            Logger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableGender set to 'none'");
        }
        if (this.applicableColorPermutation == ItemColorPermutationEnum.none)
        {
            Logger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableColorPermutation set to 'none'");
        }
        if ((this._applicableRacesSet.Count == 1 && this._applicableRacesSet.First() == RacesEnum.none) ||
         this._applicableRacesSet.Contains(RacesEnum.none))
        {
            Logger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has no applicableRaces defined");

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

