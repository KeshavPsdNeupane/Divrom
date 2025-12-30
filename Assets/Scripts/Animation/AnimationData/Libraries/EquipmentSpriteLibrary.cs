using UnityEngine;

public class EquipmentSpriteLibrary : CustomSpriteLibraryDefination
{
    [Tooltip("Put the Equipment part this SpriteLibrary is associated with.")]
    [SerializeField] private EquipingPartEnum equpingBodyPart = EquipingPartEnum.none;
    public EquipingPartEnum EquipingBodyPart => equpingBodyPart;

    protected override void OnValidate()
    {
        if (this.equpingBodyPart == EquipingPartEnum.none)
        {
            Logger.Warn($"EquipmentSpriteLibrary '{this.name}' has equpingBodyPart set to 'none'");
        }
    }
}
