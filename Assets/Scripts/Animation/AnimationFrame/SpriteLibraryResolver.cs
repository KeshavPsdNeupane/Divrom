using UnityEngine;
using UnityEngine.U2D.Animation;

[ExecuteAlways]
public class TestSpriteLibraryResolver : MonoBehaviour
{
    [SerializeField] private SpriteAnimationLibraryAsset animationLibraryAsset;
    [SerializeField] private SpriteLibrary targetSpriteLibrary;

    [SerializeField] private Gender gender = Gender.male;
    [SerializeField] private EquipingPart equipingPart = EquipingPart.body;
    [SerializeField] private Races race = Races.human;

    private static readonly AnimationCategory[] Categories =
        (AnimationCategory[])System.Enum.GetValues(typeof(AnimationCategory));

    private bool resolved;

    private void OnEnable()
    {
        if (resolved) return;
        resolved = true;
        Resolve();
    }

    private void Resolve()
    {

        if (targetSpriteLibrary == null || animationLibraryAsset == null)
            return;


        if (!animationLibraryAsset.TryGetLibrary(
                gender,
                equipingPart,
                race,
                out SpriteLibraryAsset sourceLibrary))
            return;

        var dummyAsset = targetSpriteLibrary.spriteLibraryAsset;
        if (dummyAsset == null || ReferenceEquals(dummyAsset, sourceLibrary))
            return;

        foreach (var category in Categories)
        {
            string categoryName = category.ToString();
            var labels = dummyAsset.GetCategoryLabelNames(categoryName);
            if (labels == null) continue;

            foreach (var label in labels)
            {
                var sprite = sourceLibrary.GetSprite(categoryName, label);
                if (sprite != null)
                    targetSpriteLibrary.AddOverride(sprite, categoryName, label);
            }
        }
    }
}
