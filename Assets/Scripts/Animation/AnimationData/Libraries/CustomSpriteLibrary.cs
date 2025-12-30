using UnityEngine.U2D.Animation;

/// <summary>
/// Custom SpriteLibrary that includes body part information and a method to clear overrides.
/// Used to manage sprite overrides for different equipping parts. While creating character customization systems.
/// </summary>
public abstract class CustomSpriteLibraryDefination : SpriteLibrary
{
    // this enum int is useful for indexing into dictonaries so i can build template method for building dictonaries
    // and getting overriding assets on library resolvers
    public void ClearOverrides()
    {
        var categories = spriteLibraryAsset.GetCategoryNames();
        foreach (var category in categories)
        {
            var labels = spriteLibraryAsset.GetCategoryLabelNames(category);
            foreach (var label in labels)
            {
                RemoveOverride(category, label);
            }
        }

    }
}
