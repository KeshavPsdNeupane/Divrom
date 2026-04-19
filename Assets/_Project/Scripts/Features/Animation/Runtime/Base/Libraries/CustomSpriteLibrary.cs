using UnityEngine.U2D.Animation;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.SpriteComposer2D {
	/// <summary>
	/// Generic Custom Sprite Library Definition.
	/// Used to manage sprite overrides for different parts.
	/// Tpart: Any Enum representing parts can be used here.
	/// but 0 in the enum should represent 'none' or 'undefined' state.
	/// since 0 is being used as default value. for editing convenience and validation.
	/// other values should represent valid parts.
	/// </summary>
	/// <typeparam name="Tpart"></typeparam>
	[RequireComponent(typeof(SpriteResolver), typeof(SetSpriteToPivot))]
	public class CustomSpriteLibraryDefination<Tpart> :
	SpriteLibrary where Tpart : System.Enum {
		[Tooltip("Put the part this SpriteLibrary is associated with.")]
		[SerializeField] protected Tpart partType = default;

		public Tpart PartType => partType;

		[SerializeField] private SpriteResolver resolver;
		public void ClearOverride(SpriteLibraryAsset defaultAsset) {
			this.spriteLibraryAsset = defaultAsset;
			RefreshSpriteResolvers();
		}


		protected virtual void OnValidate() {
#if UNITY_EDITOR
			// Skip this warning if the object is still prefab default or not yet serialized
			if (PrefabUtility.IsPartOfPrefabAsset(this)) return;
#endif
			if (EqualityComparer<Tpart>.Default.Equals(this.partType, default)) {
				Debug.LogWarning(
					$"BodyRegionSpriteLibrary '{name}' has bodyRegion set to 'none'"
				);
			}
		}

		public void SetActiveLabel(string category, string label) {
			if (this.resolver != null) {
				this.resolver.SetCategoryAndLabel(category, label);
				RefreshSpriteResolvers();

			}
		}
	}
}