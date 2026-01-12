using UnityEngine;
using Kope.Core.CompilerServices;
namespace Kope.ModularSpriteAnimation.Runtime
{
    public class BodyRegionSpriteLibrary : CustomSpriteLibraryDefination
    {
        [Tooltip("Put the body region this SpriteLibrary is associated with.")]
        [SerializeField] private BodyRegionEnum currentBodyRegion = BodyRegionEnum.none;
        public BodyRegionEnum CurrentBodyRegion => currentBodyRegion;

        protected override void OnValidate()
        {
            if (this.currentBodyRegion == BodyRegionEnum.none)
            {
                MyLogger.Warn($"BodyRegionSpriteLibrary '{this.name}' has bodyRegion set to 'none'");
            }
        }
    }
}