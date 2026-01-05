using System;
using System.Collections.Generic;
using UnityEngine;
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
/// This mapping is based on keyword matching within the weapon type names.
/// Weapons with similar characteristics share the same animation state.
/// Important notes:
/// - Longer keywords are prioritized to avoid partial matches.
///   For example, "LongSword" is checked before "Sword", and "Crossbow" before "Bow".
/// - For new weapons, place them in the list in any convenient position, 
///   but make sure longer keywords come before shorter keywords if one is a superset of the other.
/// </summary>
/// 
public static class WeaponAnimationMapper
{
    private static readonly Dictionary<string, AnimationState> cached = new();
    private static readonly List<KeyValuePair<string, AnimationState>> keywordToAnimationList = new()
    {
        new("Bare", AnimationState.Swing),
        new("LongSword", AnimationState.Swing),
        new("Crossbow", AnimationState.Thrust),
        new("Bow", AnimationState.Shoot),
        new("Staff", AnimationState.Thrust),
        new("Sword", AnimationState.Swing),
        new("Dagger", AnimationState.Swing),
        new("Spear", AnimationState.Thrust),
    };

    static WeaponAnimationMapper()
    {
        keywordToAnimationList.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
        {
            GetAnimationType(wt);
        }
    }
    public static AnimationState GetAnimationType(string weaponName)
    {
        if (cached.TryGetValue(weaponName, out AnimationState cachedAnimation))
            return cachedAnimation;

        foreach (var kvp in keywordToAnimationList)
        {
            if (weaponName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                cached[weaponName] = kvp.Value;
                return kvp.Value;
            }
        }
        cached[weaponName] = AnimationState.None;
        return AnimationState.None;
    }
    public static AnimationState GetAnimationType(WeaponType weaponType)
    {
        return GetAnimationType(weaponType.ToString());
    }
}



[Serializable]
public class WeaponData
{
    private const int UNINITIALIZED_HASH = -999;
    [SerializeField] private string weaponName = "Bare Hands";
    [SerializeField] private WeaponType weaponType = WeaponType.Bare;

    [SerializeField] private float attackSpeed = 1.0f;
    private string assetID = ""; // for future use, used for database linking
    // snapshot
    private WeaponType lastWeaponType = default;

    private AnimationState primaryAttackAnimation = WeaponAnimationMapper.GetAnimationType(WeaponType.Bare);
    // cache
    private int primaryAttackAnimationHash = UNINITIALIZED_HASH;

    // Properties
    public string AssetID => assetID;
    public string WeaponName => weaponName;
    public WeaponType WeaponType => weaponType;
    public AnimationState PrimaryAttackAnimation => this.primaryAttackAnimation;
    public float AttackSpeed => attackSpeed;
    public int PrimaryAttackAnimationHash
    {
        get
        {
            if (primaryAttackAnimationHash == UNINITIALIZED_HASH && primaryAttackAnimation != AnimationState.None)
            {
                primaryAttackAnimationHash =
                    Animator.StringToHash(primaryAttackAnimation.ToString());
            }
            return primaryAttackAnimationHash;
        }
    }

    public WeaponData()
    {
        this.lastWeaponType = this.weaponType;
    }
    public WeaponData(string assetID,
        string weaponName,
        WeaponType weaponType,
        float attackSpeed)
    {
        this.assetID = assetID;
        this.weaponName = weaponName;
        this.weaponType = weaponType;
        this.attackSpeed = attackSpeed;
        this.lastWeaponType = this.weaponType;
        this.primaryAttackAnimation = WeaponAnimationMapper.GetAnimationType(weaponType);
    }

    public bool HasChanged()
    {
        if (this.primaryAttackAnimation == AnimationState.None)
            this.primaryAttackAnimation = WeaponAnimationMapper.GetAnimationType(this.weaponType);

        if (this.weaponType != this.lastWeaponType)
        {
            this.lastWeaponType = this.weaponType;
            this.primaryAttackAnimation = WeaponAnimationMapper.GetAnimationType(this.weaponType);
            this.primaryAttackAnimationHash = UNINITIALIZED_HASH;
            return true;
        }

        return false;
    }


}


