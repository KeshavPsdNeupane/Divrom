using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using UnityEngine;

public enum GenderEnum { male, female, both }
public enum ItemColorPermutationEnum { none, red, blue, green, yellow, purple, orange, black, white, grey, brown, pink, cyan, magenta, lime, navy, teal, maroon, olive, silver, gold, bronze }

public enum AnimationCategoryEnum { spell, thrust, walk, idle, swing, shoot, death }

// here All mean this item is applicable to all races
public enum RacesEnum { human, elf, dwarf, orc, goblin, troll, undead, giant, dragonborn, halfling, gnome, fairy, vampire, werewolf, All }


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
