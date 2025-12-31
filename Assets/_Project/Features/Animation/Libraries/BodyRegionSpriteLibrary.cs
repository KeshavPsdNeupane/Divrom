using UnityEngine;

public class BodyRegionSpriteLibrary : CustomSpriteLibraryDefination
{
    [Tooltip("Put the body region this SpriteLibrary is associated with.")]
    [SerializeField] private BodyRegionEnum bodyRegion = BodyRegionEnum.none;
    public BodyRegionEnum BodyRegion => bodyRegion;

    protected override void OnValidate()
    {
        if (this.bodyRegion == BodyRegionEnum.none)
        {
            Logger.Warn($"BodyRegionSpriteLibrary '{this.name}' has bodyRegion set to 'none'");
        }
    }
}
