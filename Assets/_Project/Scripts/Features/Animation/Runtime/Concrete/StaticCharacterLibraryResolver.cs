using Kope.SpriteComposer2D;
using Kope.EntityIdentity;


/// <summary>
/// Different color permutations for items.
/// Grouped by ranges for default, metallic, and natural colors.
/// All is not included here since color permutation should be specific.
/// </summary>
public enum ItemColorPermutationEnum : short {
	// default color 0 to 999
	GREY = 0,
	BLACK = 1, WHITE = 2, LIME = 3, YELLOW = 4, BLUE = 5,
	RED = 6, ORANGE = 7, BROWN = 8, BLUEGREY = 9,
	// metallic colors 1000 to 1999
	CERAMIC = 1000, GOLD = 1001, SILVER = 1002, BRONZE = 1003, STEEL = 1004,
	IRON = 1005, WOOD = 1006, COPPER = 1007,
	// natural colors 2000 to 2999
	LEATHER = 2000, SANDY = 2001, GINGER = 2002,

}
public class StaticCharacterLibraryResolver
 : StaticBaseCharacterAnimationLibraryResolver<GenderEnum, RaceEnum, ItemColorPermutationEnum, BodyRegionEnum, EquipmentPartEnum> { }
