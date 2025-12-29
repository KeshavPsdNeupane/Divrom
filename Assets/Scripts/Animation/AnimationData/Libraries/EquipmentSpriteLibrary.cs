using UnityEngine;

public class EquipmentSpriteLibrary : CustomSpriteLibraryDefination
{
    [Tooltip("Put the Equipment part this SpriteLibrary is associated with.")]
    [SerializeField] private EquipingPartEnum equpingBodyPart = EquipingPartEnum.none;
    public EquipingPartEnum EquipingBodyPart => equpingBodyPart;
}
