using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

[DisallowMultipleComponent]
public class StaticAnimationLibraryResolver : MonoBehaviour
{
    [SerializeField] private RacesEnum race = RacesEnum.human;
    [SerializeField] private GenderEnum gender = GenderEnum.male;

    #region Base Character / BodyRegion

    [Header("\nBase Character Library Settings")]
    [Tooltip("Libraries that need to be resolved for base body regions")]
    [SerializeField] private List<BodyRegionSpriteLibrary> baseCharacterLibraries;
    [Tooltip("Assets that can be used to resolve base body regions")]
    [SerializeField] private List<BodyRegionAnimationLibraryAsset> baseCharacterAssets;

    private readonly Dictionary<BodyRegionEnum, BodyRegionSpriteLibrary> baseCharacterLibrariesDict = new();
    private readonly Dictionary<BodyRegionEnum, BodyRegionAnimationLibraryAsset> baseCharacterAssetsDict = new();


    #endregion

    #region Equipment

    [Header("\nEquipment Resolution Settings")]
    [Tooltip("Libraries that need to be resolved for equipment")]
    [SerializeField] private List<EquipmentSpriteLibrary> equipmentLibraries;
    [Tooltip("Assets that can be used to resolve equipment")]
    [SerializeField] private List<EquipmentAnimationLibraryAsset> equipmentAssets;

    private readonly Dictionary<EquipingPartEnum, EquipmentSpriteLibrary> equipmentLibrariesDict = new();
    private readonly Dictionary<EquipingPartEnum, EquipmentAnimationLibraryAsset> equipmentAssetsDict = new();
    private static readonly AnimationCategoryEnum[] animationCategoryEnums = (AnimationCategoryEnum[])System.Enum.GetValues(typeof(AnimationCategoryEnum));

    #endregion

    private bool isResolved = false;

    #region Editor Live Updates
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            BuildAllDictionaries();
            MapAllAssets();
        }
    }
#endif
    #endregion


    private void Awake()
    {
        BuildAllDictionaries();
        ClearAllOverrides();
        ResolveAllAssets();
    }

    #region Properties for Dynamic Changes
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
        BuildBodyRegionDictionaries();
        BuildEquipmentDictionaries();
    }



    private void BuildBodyRegionDictionaries()
    {
        baseCharacterAssetsDict.Clear();
        foreach (var asset in baseCharacterAssets)
        {
            if (asset != null)
            {
                baseCharacterAssetsDict[asset.ApplicableBaseBody] = asset;
            }
        }

        baseCharacterLibrariesDict.Clear();
        foreach (var library in baseCharacterLibraries)
        {
            if (library != null)
            {
                baseCharacterLibrariesDict[library.BodyRegion] = library;
            }
        }
    }

    private void BuildEquipmentDictionaries()
    {
        equipmentAssetsDict.Clear();
        foreach (var asset in equipmentAssets)
        {
            if (asset != null)
                equipmentAssetsDict[asset.ApplicableEquipingPart] = asset;
        }

        equipmentLibrariesDict.Clear();
        foreach (var library in equipmentLibraries)
        {
            if (library != null)
                equipmentLibrariesDict[library.EquipingBodyPart] = library;
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
        MapBodyRegionAssets();
        MapEquipmentAssets();
    }

    private void MapBodyRegionAssets()
    {
        if (baseCharacterAssetsDict.Count == 0 || baseCharacterLibrariesDict.Count == 0) return;
        foreach (var kvp in baseCharacterLibrariesDict)
        {
            var region = kvp.Key;
            var library = kvp.Value;

            if (baseCharacterAssetsDict.TryGetValue(region, out var asset))
            {
                if (asset.TryGetResolvedLibrary(gender, region, race, out var resolvedAsset))
                {
                    ApplyOverrides(resolvedAsset, library);
                }
            }
        }
    }


    private void MapEquipmentAssets()
    {
        if (equipmentAssetsDict.Count == 0 || equipmentLibrariesDict.Count == 0) return;
        foreach (var kvp in equipmentLibrariesDict)
        {
            var part = kvp.Key;
            var library = kvp.Value;

            if (equipmentAssetsDict.TryGetValue(part, out var asset))
            {
                if (asset.TryGetResolvedLibrary(gender, part, race, out var resolvedAsset))
                {
                    ApplyOverrides(resolvedAsset, library);
                }
            }
        }
    }
    private void ApplyOverrides(SpriteLibraryAsset sourceAsset, CustomSpriteLibraryDefination library)
    {
        if (!library.spriteLibraryAsset)
        {
            Logger.Warn($"Sprite Library Asset is null in {library.name}");
            return;
        }
        foreach (var category in animationCategoryEnums)
        {
            string categoryName = category.ToString();
            var labels = library.spriteLibraryAsset.GetCategoryLabelNames(categoryName);
            if (labels == null) continue;

            foreach (var label in labels)
            {
                library.AddOverride(sourceAsset, categoryName, label);
            }
        }
    }


    #endregion

    #region Clear Overrides

    public void ClearAllOverrides()
    {
        foreach (var library in baseCharacterLibraries)
        {
            if (library != null)
                library.ClearOverrides();
        }
        foreach (var library in equipmentLibraries)
        {
            if (library != null)
                library.ClearOverrides();
        }

        isResolved = false;
    }

    #endregion

    #region Runtime Equipment API

    public void EquipItem(EquipingPartEnum part, EquipmentAnimationLibraryAsset newAsset)
    {
        if (newAsset == null) return;

        // Update the asset dictionary
        equipmentAssetsDict[part] = newAsset;

        // Apply the resolved sprites immediately
        if (equipmentLibrariesDict.TryGetValue(part, out var library))
        {
            if (newAsset.TryGetResolvedLibrary(gender, part, race, out var resolvedAsset))
            {
                ApplyOverrides(resolvedAsset, library);
            }
        }
    }

    public void UnequipItem(EquipingPartEnum part)
    {
        if (equipmentLibrariesDict.TryGetValue(part, out var library))
        {
            library.ClearOverrides();
        }
        equipmentAssetsDict.Remove(part);
    }

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
