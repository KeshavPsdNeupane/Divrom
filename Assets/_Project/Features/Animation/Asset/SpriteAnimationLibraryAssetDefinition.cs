using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using UnityEngine;

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
    orange = 5, brown = 6,
    // metallic colors 1000 to 1999
    ceramic = 1000, gold = 1001, silver = 1002, bronze = 1003, steel = 1004,
    iron = 1005,
    // natural colors 2000 to 2999
    leather = 2000,

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
    [SerializeField] protected List<RacesEnum> applicableRaces = new() { RacesEnum.none };

    public string VariantName => variantName;
    public GenderEnum ApplicableGender => applicableGender;
    public ItemColorPermutationEnum ApplicableColorPermutation => applicableColorPermutation;
    public SpriteLibraryAsset SpriteLibraryAsset => spriteLibraryAsset;
    public List<RacesEnum> ApplicableRaces => applicableRaces;

    protected virtual void OnValidate()
    {
        if (this.applicableGender == GenderEnum.none)
        {
            Logger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableGender set to 'none'");
        }
        if (this.applicableColorPermutation == ItemColorPermutationEnum.none)
        {
            Logger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has applicableColorPermutation set to 'none'");
        }
        if ((this.applicableRaces.Count == 1 && this.applicableRaces[0] == RacesEnum.none) ||
         this.applicableRaces.Contains(RacesEnum.none))
        {
            Logger.Warn($"SpriteAnimationLibraryAssetDefinition '{this.name}' has no applicableRaces defined");
        }

    }



    public abstract string LibraryId { get; }
    // this enum int is useful for indexing into dictonaries so i can build template method for building dictonaries
    // and overrighting assets on library resolvers

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
        if (!IsApplicable(gender, tpart, race)) return false;
        if (spriteLibraryAsset == null) return false;

        lib = spriteLibraryAsset;
        return true;
    }
}
