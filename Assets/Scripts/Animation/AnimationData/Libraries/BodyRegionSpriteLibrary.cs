using UnityEngine;

public class BodyRegionSpriteLibrary : CustomSpriteLibraryDefination
{
    [Tooltip("Put the body region this SpriteLibrary is associated with.")]
    [SerializeField] private BodyRegionEnum bodyRegion;
    public BodyRegionEnum BodyRegion => bodyRegion;
}
