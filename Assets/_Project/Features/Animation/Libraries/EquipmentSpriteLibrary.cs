using UnityEngine;

public class EquipmentSpriteLibrary : CustomSpriteLibraryDefination
{
    [Tooltip("Put the Equipment part this SpriteLibrary is associated with.")]
    [SerializeField] private EquipingPartEnum currentBodyEquipmentPart = EquipingPartEnum.none;
    public EquipingPartEnum CurrentBodyEquipmentPart => currentBodyEquipmentPart;

    protected override void OnValidate()
    {
        if (this.currentBodyEquipmentPart == EquipingPartEnum.none)
        {
            Logger.Warn($"EquipmentSpriteLibrary '{this.name}' has currentBodyEquipmentPart set to 'none'");
        }
    }
}
