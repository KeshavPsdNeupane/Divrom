using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEditor;
using Unity.VisualScripting;

/// <summary>
/// Resolves and applies SpriteLibraryAssets for base character body regions and equipment.
/// "Static" refers to pre-provided assets instead of dynamic runtime lookup (like via Addressables).
/// Handles both runtime and editor preview.
/// </summary>
[DisallowMultipleComponent]
public class StaticAnimationLibraryResolver : MonoBehaviour
{
    #region Serialized Fields

    [Header("Character Settings")]
    [Tooltip("Current race of the character for asset resolution.")]
    [SerializeField] private RacesEnum race = RacesEnum.none;

    [Tooltip("Current gender of the character for asset resolution.")]
    [SerializeField] private GenderEnum gender = GenderEnum.none;

    [Tooltip("Default SpriteLibraryAsset used when clearing overrides.")]
    [SerializeField] private SpriteLibraryAsset defaultSpriteLibraryAsset;

    [Header("\nBase Character Library Settings")]
    [Tooltip("Libraries that need to be resolved for base body regions")]
    [SerializeField] private List<BodyRegionSpriteLibrary> baseCharacterLibraries;

    [Tooltip("Assets that can be used to resolve base body regions")]
    [SerializeField] private List<BodyRegionAnimationLibraryAsset> baseCharacterAssets;

    [Header("\nEquipment Resolution Settings")]
    [Tooltip("Libraries that need to be resolved for equipment")]
    [SerializeField] private List<EquipmentSpriteLibrary> equipmentLibraries;

    [Tooltip("Assets that can be used to resolve equipment")]
    [SerializeField] private List<EquipmentAnimationLibraryAsset> equipmentAssets;

    #endregion

    #region Private Fields

    private readonly Dictionary<BodyRegionEnum, BodyRegionSpriteLibrary> baseCharacterLibrariesDict = new();
    private readonly Dictionary<BodyRegionEnum, BodyRegionAnimationLibraryAsset> baseCharacterAssetsDict = new();

    private readonly Dictionary<EquipingPartEnum, EquipmentSpriteLibrary> equipmentLibrariesDict = new();
    private readonly Dictionary<EquipingPartEnum, EquipmentAnimationLibraryAsset> equipmentAssetsDict = new();

    private bool isResolved = false;

    private SpriteLibraryAsset _tempDefaultAsset;

    #endregion

    #region Unity Callbacks

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            BuildAllDictionaries();

            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    ClearAllOverrides();
                    ResolveAllAssets();
                }
            };
        }
        if (this.defaultSpriteLibraryAsset == null)
        {
            Logger.Warn($"Default Sprite Library Asset is not assigned on {this.name}.");
        }
        if (this.gender == GenderEnum.none)
        {
            Logger.Warn($"Gender is set to 'none' on {this.name}. This may lead to incorrect asset resolution.");
        }
        if (this.race == RacesEnum.none)
        {
            Logger.Warn($"Race is set to 'none' on {this.name}. This may lead to incorrect asset resolution.");
        }
    }
#endif



    private void Awake()
    {
        BuildAllDictionaries();
        ClearAllOverrides();
        ResolveAllAssets();
    }

    #endregion

    #region Properties

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

    public void ResolveAllAssets()
    {
        if (isResolved) return;
        MapAllAssets();
        isResolved = true;
    }

    private void MapAllAssets()
    {
        MapAssets(baseCharacterLibrariesDict, baseCharacterAssetsDict);
        MapAssets(equipmentLibrariesDict, equipmentAssetsDict);
    }

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
                asset.TryGetResolvedLibrary(gender, kvp.Key, race, out var resolvedAsset))
            {
                ApplyOverride(resolvedAsset, kvp.Value);
            }
        }
    }

    private void ApplyOverride(SpriteLibraryAsset asset, CustomSpriteLibraryDefination library)
    {
        if (asset == null || library == null) return;

        library.spriteLibraryAsset = asset;
        library.RefreshSpriteResolvers();
    }

    #endregion

    #region Clear Overrides

    public void ClearAllOverrides()
    {
        ClearOverrides(this.baseCharacterLibrariesDict);
        ClearOverrides(this.equipmentLibrariesDict);
        isResolved = false;
    }

    private void ClearOverrides<TEnum, TLibrary>(Dictionary<TEnum, TLibrary> libraries)
        where TLibrary : CustomSpriteLibraryDefination
        where TEnum : System.Enum
    {
        foreach (var kvp in libraries)
        {
            kvp.Value.ClearOverride(GetSafeDefaultAsset());
        }
    }



    private SpriteLibraryAsset GetSafeDefaultAsset()
    {
        if (defaultSpriteLibraryAsset != null) return defaultSpriteLibraryAsset;

#if UNITY_EDITOR
        Logger.Warn("Default Sprite Library Asset is missing. Using temporary empty asset.");
#endif
        if (_tempDefaultAsset == null)
            _tempDefaultAsset = ScriptableObject.CreateInstance<SpriteLibraryAsset>();

        return _tempDefaultAsset;
    }

    #endregion

    #region Runtime Equipment API

    public void EquipItem(EquipingPartEnum part, EquipmentAnimationLibraryAsset newAsset)
    {
        if (newAsset == null) return;

        this.equipmentAssetsDict[part] = newAsset;

        if (this.equipmentLibrariesDict.TryGetValue(part, out var library))
        {
            if (newAsset.TryGetResolvedLibrary(gender, part, race, out var resolvedAsset))
            {
                ApplyOverride(resolvedAsset, library);
            }
        }
    }

    public void UnequipItem(EquipingPartEnum part)
    {
        if (!this.equipmentAssetsDict.ContainsKey(part) || !this.equipmentLibrariesDict.ContainsKey(part)) return;
        var lib = this.equipmentLibrariesDict[part];
        lib.ClearOverride(GetSafeDefaultAsset());
    }

    #endregion
}
