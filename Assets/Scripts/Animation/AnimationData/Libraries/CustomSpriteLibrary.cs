using UnityEngine.U2D.Animation;

/// <summary>
/// Custom SpriteLibrary that includes body part information and a method to clear overrides.
/// Used to manage sprite overrides for different equipping parts. While creating character customization systems.
/// </summary>
public abstract class CustomSpriteLibraryDefination : SpriteLibrary
{
    public void ClearOverride(SpriteLibraryAsset defaultAsset)
    {
        this.spriteLibraryAsset = defaultAsset;
        RefreshSpriteResolvers();
    }
    protected abstract void OnValidate();
}
