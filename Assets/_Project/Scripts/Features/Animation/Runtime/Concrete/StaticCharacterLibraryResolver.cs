using Kope.SpriteComposer2D;

/// <summary>
/// Gender of the character for which the asset is applicable.
/// both means the asset can be used for male and female characters.
/// </summary>
public enum GenderEnum : short
{
    none = 0,
    male = 1,
    female = 2,
    both = 3,

}

/// <summary>
/// Different color permutations for items.
/// Grouped by ranges for default, metallic, and natural colors.
/// All is not included here since color permutation should be specific.
/// </summary>
public enum ItemColorPermutationEnum : short
{
    none = 0,
    // default color 1 to 999
    black = 1, white = 2, lime = 3, yellow = 4, blue = 5,
    red = 6, orange = 7, brown = 8, bluegrey = 9,
    // metallic colors 1000 to 1999
    ceramic = 1000, gold = 1001, silver = 1002, bronze = 1003, steel = 1004,
    iron = 1005, wood = 1006, copper = 1007,
    // natural colors 2000 to 2999
    leather = 2000, sandy = 2001, ginger = 2002,

}


/// <summary>
/// Different races supported.
/// Grouped by ranges to allow future insertions.
/// All means the asset is applicable to all races.
/// </summary>
public enum RacesEnum : short
{
    none = 0,
    All = 9999,
    // the listed enum are just the common varient for each race type

    // Humans (1 - 499), combineable with half-humans so total is 1 - 999
    // seperation is 10 since all are almost same termology
    human = 1,
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

public class StaticCharacterLibraryResolver
 : StaticBaseCharacterAnimationLibraryResolver<GenderEnum, RacesEnum, ItemColorPermutationEnum, BodyRegionEnum, EquipmentPartEnum>
{ }
