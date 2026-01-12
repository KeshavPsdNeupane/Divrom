using UnityEngine.U2D.Animation;
using UnityEngine;


namespace Kope.ModularSpriteAnimation
{
    /// <summary>
    /// Custom SpriteLibrary that includes body part information and a method to clear overrides.
    /// Used to manage sprite overrides for different equipping parts. While creating character customization systems.
    /// </summary>
    [RequireComponent(typeof(SpriteResolver), typeof(SetSpriteToPivot))]
    public abstract class CustomSpriteLibraryDefination : SpriteLibrary
    {
        [SerializeField] private SpriteResolver resolver;
        public void ClearOverride(SpriteLibraryAsset defaultAsset)
        {
            this.spriteLibraryAsset = defaultAsset;
            RefreshSpriteResolvers();
        }
        protected abstract void OnValidate();


        public void SetActiveLabel(string category, string label)
        {
            if (resolver != null)
            {
                resolver.SetCategoryAndLabel(category, label);
                RefreshSpriteResolvers();

            }
        }
    }
}