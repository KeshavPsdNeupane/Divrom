using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Kope.SpriteComposer2D {
	[DisallowMultipleComponent]
	public class StaticBaseCharacterAnimationLibraryResolver<TGender, TRace, TColorPermutation, TBodyPart, TEquipmentPart>
		: IEditorLibraryActiveable
		where TGender : System.Enum
		where TRace : System.Enum
		where TColorPermutation : System.Enum
		where TBodyPart : System.Enum
		where TEquipmentPart : System.Enum {
		#region Serialized Fields

		[Header("Character Settings")]
		[SerializeField] private TGender gender = default;
		[SerializeField] private TRace race = default;
		[SerializeField] private SpriteLibraryAsset defaultSpriteLibraryAsset;

		[Header("Base Character Library Settings")]
		[SerializeField] private List<CustomSpriteLibraryDefination<TBodyPart>> baseCharacterLibraries;
		[SerializeField] private List<SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TBodyPart>> baseCharacterAssets;

		[Header("Equipment Resolution Settings")]
		[SerializeField] private List<CustomSpriteLibraryDefination<TEquipmentPart>> equipmentLibraries;
		[SerializeField] private List<SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TEquipmentPart>> equipmentAssets;

		#endregion

		#region Private Dictionaries

		private readonly Dictionary<TBodyPart, CustomSpriteLibraryDefination<TBodyPart>> baseCharacterLibrariesDict = new();
		private readonly Dictionary<TBodyPart, SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TBodyPart>> baseCharacterAssetsDict = new();

		private readonly Dictionary<TEquipmentPart, CustomSpriteLibraryDefination<TEquipmentPart>> equipmentLibrariesDict = new();
		private readonly Dictionary<TEquipmentPart, SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TEquipmentPart>> equipmentAssetsDict = new();

		private bool isResolved = false;

		#endregion

		#region Unity Callbacks

#if UNITY_EDITOR
		private void OnValidate() {
			if (defaultSpriteLibraryAsset == null)
				Debug.LogWarning($"Default Sprite Library Asset is not assigned on {name}.");
			EditorApplication.delayCall += () => RefreshPreview();
		}
#endif

		private void Awake() {
			RefreshPreview();
		}

		#endregion

		#region Public API

		public void RefreshPreview() {
			BuildAllDictionaries();
			ClearAllOverrides();
			ResolveAllAssets();
		}

		#endregion

		#region Dictionary Builders

		private void BuildAllDictionaries() {
			BuildDictionaries(baseCharacterLibraries, baseCharacterAssets, baseCharacterLibrariesDict, baseCharacterAssetsDict);
			BuildDictionaries(equipmentLibraries, equipmentAssets, equipmentLibrariesDict, equipmentAssetsDict);
		}

		private void BuildDictionaries<TEnum, TLibrary, TAsset>(
			List<TLibrary> libraries,
			List<TAsset> assets,
			Dictionary<TEnum, TLibrary> libraryDict,
			Dictionary<TEnum, TAsset> assetDict
		)
			where TLibrary : CustomSpriteLibraryDefination<TEnum>
			where TAsset : SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TEnum>
			where TEnum : System.Enum {
			assetDict.Clear();
			foreach (var asset in assets) {
				if (asset != null)
					assetDict[asset.ApplicablePart] = asset;
			}

			libraryDict.Clear();
			foreach (var library in libraries) {
				if (library != null)
					libraryDict[library.PartType] = library;
			}
		}

		#endregion

		#region Resolve / Apply

		public void ResolveAllAssets() {
			if (isResolved) return;

			MapAssets(baseCharacterLibrariesDict, baseCharacterAssetsDict);
			MapAssets(equipmentLibrariesDict, equipmentAssetsDict);

			isResolved = true;
		}

		private void MapAssets<TEnum, TLibrary, TAsset>(
			Dictionary<TEnum, TLibrary> libraries,
			Dictionary<TEnum, TAsset> assets
		)
			where TLibrary : CustomSpriteLibraryDefination<TEnum>
			where TAsset : SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TEnum>
			where TEnum : System.Enum {
			foreach (var kvp in libraries) {
				if (assets.TryGetValue(kvp.Key, out var asset) &&
					asset.TryGetResolvedLibrary(gender, kvp.Key, race, out var resolved)) {
					kvp.Value.spriteLibraryAsset = resolved;
					kvp.Value.RefreshSpriteResolvers();
				}
			}
		}

		#endregion

		#region Clear Overrides

		public void ClearAllOverrides() {
			ClearOverrides(baseCharacterLibrariesDict);
			ClearOverrides(equipmentLibrariesDict);
			isResolved = false;
		}

		private void ClearOverrides<TEnum, TLibrary>(Dictionary<TEnum, TLibrary> libraries)
			where TLibrary : CustomSpriteLibraryDefination<TEnum>
			where TEnum : System.Enum {
			foreach (var lib in libraries.Values) {
				lib.ClearOverride(defaultSpriteLibraryAsset);
			}
		}

		#endregion

		#region Editor Debugging

#if UNITY_EDITOR
		public override void SetActiveCategoryAndLabel(string category, string label) {
			SetActiveLabel(baseCharacterLibrariesDict, category, label);
			SetActiveLabel(equipmentLibrariesDict, category, label);
		}

		private void SetActiveLabel<TLibrary, TEnum>(Dictionary<TEnum, TLibrary> libraries, string category, string label)
			where TLibrary : CustomSpriteLibraryDefination<TEnum>
			where TEnum : System.Enum {
			foreach (var lib in libraries.Values) {
				lib.SetActiveLabel(category, label);
			}
		}

#endif

		#endregion
	}
}
