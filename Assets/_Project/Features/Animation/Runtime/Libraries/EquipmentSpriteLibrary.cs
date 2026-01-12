using Kope.Core.CompilerServices;
using UnityEngine;
namespace Kope.ModularSpriteAnimation.Runtime
{

    public class EquipmentSpriteLibrary : CustomSpriteLibraryDefination
    {
        [Tooltip("Put the Equipment part this SpriteLibrary is associated with.")]
        [SerializeField] private EquipmentPartEnum currentBodyEquipmentPart = EquipmentPartEnum.none;
        public EquipmentPartEnum CurrentBodyEquipmentPart => currentBodyEquipmentPart;

        protected override void OnValidate()
        {
            if (this.currentBodyEquipmentPart == EquipmentPartEnum.none)
            {
                MyLogger.Warn($"EquipmentSpriteLibrary '{this.name}' has currentBodyEquipmentPart set to 'none'");
            }
        }
    }
}