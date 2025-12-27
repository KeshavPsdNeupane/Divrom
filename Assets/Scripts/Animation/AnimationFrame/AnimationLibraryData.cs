
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

public enum Gender { male, female, both }
public enum EquipingPart { head, hair, helmet, ear, neck, body, arm, hand, torso, leg, feet, weapon, }

public enum ItemColorPermutation { none, red, blue, green, yellow, purple, orange, black, white, grey, brown, pink, cyan, magenta, lime, navy, teal, maroon, olive, silver, gold, bronze }

public enum AnimationCategory { spell, thrust, walk, idle, swing, shoot, death }

// here All mean this item is applicable to all races
public enum Races { human, elf, dwarf, orc, goblin, troll, undead, giant, dragonborn, halfling, gnome, fairy, vampire, werewolf, All }



[CreateAssetMenu(fileName = "New Animation Library", menuName = "Animation/Animation Library Data")]
public class SpriteAnimationLibraryAsset : ScriptableObject
{
    [SerializeField] private string partName;
    [SerializeField] private Gender applicableGender;
    [SerializeField] private EquipingPart applicableEquipingPart;
    [SerializeField] private ItemColorPermutation applicableColorPermutation;
    [SerializeField] private SpriteLibraryAsset spriteLibraryAsset;
    [SerializeField] private List<Races> applicableRaces;

    public string PartName => partName;
    public Gender ApplicableGender => applicableGender;
    public EquipingPart ApplicableEquipingPart => applicableEquipingPart;
    public ItemColorPermutation ApplicableColorPermutation => applicableColorPermutation;
    public SpriteLibraryAsset SpriteLibraryAsset => spriteLibraryAsset;
    public List<Races> ApplicableRaces => applicableRaces;

    public string LibraryName =>
    applicableGender.ToString() + "_" + applicableEquipingPart.ToString() + "_" + this.partName + "_" + applicableColorPermutation.ToString();

    private bool IsApplicable(Gender gender, EquipingPart equipingPart, Races race)
    {
        bool genderOk = applicableGender == Gender.both || applicableGender == gender;
        bool partOk = applicableEquipingPart == equipingPart;
        bool raceOk = applicableRaces.Contains(Races.All) || applicableRaces.Contains(race);

        if (!genderOk) Logger.LogError($"Gender mismatch: {gender} != {applicableGender}");
        if (!partOk) Logger.LogError($"EquipingPart mismatch: {equipingPart} != {applicableEquipingPart}");
        if (!raceOk) Logger.LogError($"Race mismatch: {race} not in {string.Join(", ", applicableRaces)}");

        return genderOk && partOk && raceOk;
    }


    public bool TryGetLibrary(Gender gender, EquipingPart equipingPart, Races race, out SpriteLibraryAsset spriteLibraryAsset)
    {
        spriteLibraryAsset = null;
        // Check if this library data is applicable for the given parameters
        if (!IsApplicable(gender, equipingPart, race)) return false;

        if (this.spriteLibraryAsset == null) return false;

        spriteLibraryAsset = this.spriteLibraryAsset;
        return true;
    }

}
