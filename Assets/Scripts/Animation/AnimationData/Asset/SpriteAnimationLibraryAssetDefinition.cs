using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "both" means this item is applicable to both genders
/// </summary>
public enum GenderEnum
{
    male,
    female,
    both
}

public enum ItemColorPermutationEnum
{
    none,
    red,
    blue,
    green,
    yellow,
    purple,
    orange,
    black,
    white,
    grey,
    brown,
    pink,
    cyan,
    magenta,
    lime,
    navy,
    teal,
    maroon,
    olive,
    silver,
    gold,
    bronze
}

/// <summary>
/// "All" means this item is applicable to all races
/// </summary>
public enum RacesEnum
{
    human,
    elf,
    dwarf,
    orc,
    goblin,
    troll,
    undead,
    giant,
    dragonborn,
    halfling,
    gnome,
    fairy,
    vampire,
    werewolf,
    All
}


public abstract class SpriteAnimationLibraryAssetDefinition : ScriptableObject
{
    [SerializeField] protected string variantName;
    [SerializeField] protected GenderEnum applicableGender;
    [SerializeField] protected ItemColorPermutationEnum applicableColorPermutation;
    [SerializeField] protected SpriteLibraryAsset spriteLibraryAsset;
    [SerializeField] protected List<RacesEnum> applicableRaces;

    public string VariantName => variantName;
    public GenderEnum ApplicableGender => applicableGender;
    public ItemColorPermutationEnum ApplicableColorPermutation => applicableColorPermutation;
    public SpriteLibraryAsset SpriteLibraryAsset => spriteLibraryAsset;
    public List<RacesEnum> ApplicableRaces => applicableRaces;

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
