using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEditor;

/// <summary>
/// Resolves and applies SpriteLibraryAssets for base character body regions and equipment.
/// "Static" refers to pre-provided assets instead of dynamic runtime lookup (like via Addressables).
/// Handles both runtime and editor preview.
/// </summary>
[DisallowMultipleComponent]
public class StaticAnimationLibraryResolver : MonoBehaviour
{
    /// <summary>Current race of the character for asset resolution.</summary>
    [SerializeField] private RacesEnum race = RacesEnum.human;

    /// <summary>Current gender of the character for asset resolution.</summary>
    [SerializeField] private GenderEnum gender = GenderEnum.male;

    #region Base Character / BodyRegion

    /// <summary>List of body region sprite libraries to resolve.</summary>
    [Header("\nBase Character Library Settings")]
    [Tooltip("Libraries that need to be resolved for base body regions")]
    [SerializeField] private List<BodyRegionSpriteLibrary> baseCharacterLibraries;

    /// <summary>List of body region animation assets to use for resolving libraries.</summary>
    [Tooltip("Assets that can be used to resolve base body regions")]
    [SerializeField] private List<BodyRegionAnimationLibraryAsset> baseCharacterAssets;

    /// <summary>Lookup dictionary for fast access to body region sprite libraries.</summary>
    private readonly Dictionary<BodyRegionEnum, BodyRegionSpriteLibrary> baseCharacterLibrariesDict = new();

    /// <summary>Lookup dictionary for fast access to body region animation assets.</summary>
    private readonly Dictionary<BodyRegionEnum, BodyRegionAnimationLibraryAsset> baseCharacterAssetsDict = new();

    #endregion

    #region Equipment

    /// <summary>List of equipment sprite libraries to resolve.</summary>
    [Header("\nEquipment Resolution Settings")]
    [Tooltip("Libraries that need to be resolved for equipment")]
    [SerializeField] private List<EquipmentSpriteLibrary> equipmentLibraries;

    /// <summary>List of equipment animation assets to use for resolving libraries.</summary>
    [Tooltip("Assets that can be used to resolve equipment")]
    [SerializeField] private List<EquipmentAnimationLibraryAsset> equipmentAssets;

    /// <summary>Lookup dictionary for fast access to equipment sprite libraries.</summary>
    private readonly Dictionary<EquipingPartEnum, EquipmentSpriteLibrary> equipmentLibrariesDict = new();

    /// <summary>Lookup dictionary for fast access to equipment animation assets.</summary>
    private readonly Dictionary<EquipingPartEnum, EquipmentAnimationLibraryAsset> equipmentAssetsDict = new();

    #endregion

    /// <summary>Flag to avoid double-resolving assets.</summary>
    private bool isResolved = false;

    #region Editor Live Updates
#if UNITY_EDITOR
    /// <summary>
    /// Called by Unity when serialized fields are changed in the editor.
    /// Builds dictionaries, clears overrides, and schedules ResolveAllAssets safely after the frame.
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            BuildAllDictionaries();
            ClearAllOverrides();
            EditorApplication.delayCall += () =>
            {
                if (this != null) { ResolveAllAssets(); }
            };
        }
    }
#endif
    #endregion

    /// <summary>
    /// Called on runtime initialization.
    /// Builds dictionaries, clears any previous overrides, and resolves assets.
    /// </summary>
    private void Awake()
    {
        BuildAllDictionaries();
        ClearAllOverrides();
        ResolveAllAssets();
    }

    #region Properties for Dynamic Changes

    /// <summary>Current character race. Changing this will automatically re-resolve assets.</summary>
    public RacesEnum Race
    {
        get => this.race;
        set
        {
            if (this.race == value) return;
            this.race = value;
            ClearAllOverrides();
            ResolveAllAssets();
        }
    }

    /// <summary>Current character gender. Changing this will automatically re-resolve assets.</summary>
    public GenderEnum Gender
    {
        get => this.gender;
        set
        {
            if (this.gender == value) return;
            this.gender = value;
            ClearAllOverrides();
            ResolveAllAssets();
        }
    }

    #endregion

    #region Dictionary Builders

    /// <summary>
    /// Builds both body region and equipment dictionaries from their respective lists.
    /// </summary>
    private void BuildAllDictionaries()
    {
        BuildDictionaries(
         baseCharacterLibraries,
         baseCharacterAssets,
         baseCharacterLibrariesDict,
         baseCharacterAssetsDict,
         library => library.BodyRegion,
         asset => asset.ApplicableBaseBody
       );
        BuildDictionaries(
         equipmentLibraries,
         equipmentAssets,
         equipmentLibrariesDict,
         equipmentAssetsDict,
         library => library.EquipingBodyPart,
         asset => asset.ApplicableEquipingPart
       );
    }

    /// <summary>
    /// Converts a list of libraries and a list of assets into lookup dictionaries.
    /// </summary>
    /// <typeparam name="TEnum">Enum type used as dictionary key.</typeparam>
    /// <typeparam name="TLibrary">Type of sprite library.</typeparam>
    /// <typeparam name="TAsset">Type of animation asset.</typeparam>
    /// <param name="libraries">List of sprite libraries.</param>
    /// <param name="assets">List of animation assets.</param>
    /// <param name="libraryDict">Output dictionary for libraries.</param>
    /// <param name="assetDict">Output dictionary for assets.</param>
    /// <param name="libraryKeySelector">Function to select key from library.</param>
    /// <param name="assetKeySelector">Function to select key from asset.</param>
    private void BuildDictionaries<TEnum, TLibrary, TAsset>(
        List<TLibrary> libraries,
        List<TAsset> assets,
        Dictionary<TEnum, TLibrary> libraryDict,
        Dictionary<TEnum, TAsset> assetDict,
        System.Func<TLibrary, TEnum> libraryKeySelector,
        System.Func<TAsset, TEnum> assetKeySelector
    )
        where TLibrary : CustomSpriteLibraryDefination
        where TAsset : SpriteAnimationLibraryAssetDefinition
        where TEnum : System.Enum
    {
        assetDict.Clear();
        foreach (var asset in assets)
        {
            if (asset != null)
                assetDict[assetKeySelector(asset)] = asset;
        }

        libraryDict.Clear();
        foreach (var library in libraries)
        {
            if (library != null)
                libraryDict[libraryKeySelector(library)] = library;
        }
    }
    #endregion

    #region Resolve Assets

    /// <summary>Resolves all assets and applies them to their corresponding libraries.</summary>
    public void ResolveAllAssets()
    {
        if (isResolved) return;
        MapAllAssets();
        isResolved = true;
    }

    /// <summary>Maps body region and equipment assets to their libraries.</summary>
    private void MapAllAssets()
    {
        MapAssets(this.baseCharacterLibrariesDict, this.baseCharacterAssetsDict);
        MapAssets(this.equipmentLibrariesDict, this.equipmentAssetsDict);
    }

    /// <summary>
    /// Generic mapping function that applies resolved assets to libraries.
    /// </summary>
    /// <typeparam name="TEnum">Enum used as dictionary key.</typeparam>
    /// <typeparam name="TLibrary">Type of library.</typeparam>
    /// <typeparam name="TAsset">Type of asset.</typeparam>
    /// <param name="libraries">Dictionary of libraries.</param>
    /// <param name="assets">Dictionary of assets.</param>
    private void MapAssets<TEnum, TLibrary, TAsset>(
        Dictionary<TEnum, TLibrary> libraries,
        Dictionary<TEnum, TAsset> assets
    )
        where TLibrary : CustomSpriteLibraryDefination
        where TAsset : SpriteAnimationLibraryAssetDefinition
        where TEnum : System.Enum
    {
        foreach (var kvp in libraries)
        {
            if (assets.TryGetValue(kvp.Key, out var asset) &&
                asset.TryGetResolvedLibrary(this.gender, kvp.Key, this.race, out var resolvedAsset))
            {
                ApplyOverrides(resolvedAsset, kvp.Value);
            }
        }
    }

    /// <summary>Applies a SpriteLibraryAsset to a library and refreshes its sprite resolvers.</summary>
    /// <param name="asset">The asset to apply.</param>
    /// <param name="library">The library to apply it to.</param>
    private void ApplyOverrides(SpriteLibraryAsset asset, CustomSpriteLibraryDefination library)
    {
        if (asset == null || library == null) return;

        library.spriteLibraryAsset = asset;
        library.RefreshSpriteResolvers();
    }

    #endregion

    #region Clear Overrides

    /// <summary>Clears all sprite overrides for body and equipment libraries.</summary>
    public void ClearAllOverrides()
    {
        foreach (var library in baseCharacterLibraries)
        {
            library?.ClearOverrides();
        }
        foreach (var library in equipmentLibraries)
        {
            library?.ClearOverrides();
        }

        isResolved = false;
    }

    #endregion

    #region Runtime Equipment API

    /// <summary>Equips an item and applies its resolved asset immediately.</summary>
    /// <param name="part">Equipment part to equip.</param>
    /// <param name="newAsset">Animation library asset to equip.</param>
    public void EquipItem(EquipingPartEnum part, EquipmentAnimationLibraryAsset newAsset)
    {
        if (newAsset == null) return;

        equipmentAssetsDict[part] = newAsset;

        if (equipmentLibrariesDict.TryGetValue(part, out var library))
        {
            if (newAsset.TryGetResolvedLibrary(gender, part, race, out var resolvedAsset))
            {
                ApplyOverrides(resolvedAsset, library);
            }
        }
    }

    /// <summary>Unequips a specific item and clears its overrides.</summary>
    /// <param name="part">Equipment part to unequip.</param>
    public void UnequipItem(EquipingPartEnum part)
    {
        if (equipmentLibrariesDict.TryGetValue(part, out var library))
        {
            library.ClearOverrides();
        }
        equipmentAssetsDict.Remove(part);
    }

    /// <summary>Unequips all items and clears all equipment overrides.</summary>
    public void UnequipAll()
    {
        foreach (var kvp in equipmentLibrariesDict)
        {
            kvp.Value.ClearOverrides();
        }
        equipmentAssetsDict.Clear();
    }

    #endregion
}
