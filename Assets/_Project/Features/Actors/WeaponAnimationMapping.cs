using System;
using System.Collections.Generic;

public enum WeaponType
{
    Bare = -1,
    LongSword = 0,
    Sword = 1,
    Bow = 2,
    Crossbow = 3,
    Spear = 4,
    Dagger = 5,
    Staff = 6,
}

/// <summary>
/// Maps weapon types to their corresponding animation states.
/// 
/// This mapping is based on keyword matching within the weapon type names.
/// Weapons with similar characteristics share the same animation state.
/// 
/// Important notes:
/// - Longer keywords are prioritized to avoid partial matches.
///   For example, "LongSword" is checked before "Sword", and "Crossbow" before "Bow".
/// - For new weapons, place them in the list in any convenient position, 
///   but make sure longer keywords come before shorter keywords if one is a superset of the other.
/// </summary>
public static class WeaponAnimationMapper
{
    // Longer keywords first to avoid partial matches
    private static readonly List<KeyValuePair<string, AnimationState>> keywordToAnimation = new()
    {
        // No bare animation for Bare hands, so just  using Swing animation
        new KeyValuePair<string, AnimationState>("Bare", AnimationState.Swing),
        // LongSword includes "Sword", so it should be checked first
        new KeyValuePair<string, AnimationState>("LongSword", AnimationState.Swing),

        // Crossbow uses Thrust animation, as based on design choice,
        // since it involves a forward motion similar to thrusting
        new KeyValuePair<string, AnimationState>("Crossbow", AnimationState.Thrust),
        new KeyValuePair<string, AnimationState>("Bow", AnimationState.Shoot),
        // Staff also uses Thrust animation for spellcasting motions
        new KeyValuePair<string, AnimationState>("Staff", AnimationState.Thrust),
        new KeyValuePair<string, AnimationState>("Sword", AnimationState.Swing),
        new KeyValuePair<string, AnimationState>("Dagger", AnimationState.Swing),
        new KeyValuePair<string, AnimationState>("Spear", AnimationState.Thrust),
    };

    public static AnimationState GetAnimationType(WeaponType weaponType)
    {
        string weaponName = weaponType.ToString();

        foreach (var kvp in keywordToAnimation)
        {
            if (weaponName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return AnimationState.None;
    }
}
