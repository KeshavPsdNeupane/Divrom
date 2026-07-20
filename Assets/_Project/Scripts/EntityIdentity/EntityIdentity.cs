using System;

namespace Kope.EntityIdentity {

	/// <summary>
	/// High-level classification used by manager systems to route entities
	/// into separate processing paths (e.g., AI loop vs. interactable loop) without 
	/// relying on expensive runtime type reflection (GetType/is).
	/// Bound to a byte (0-255) as this domain is strictly binary and will not scale.
	/// </summary>
	public enum EntityType : byte {
		MOB = 0,
		PROP = 1,
	}

	/// <summary>
	/// Defines an entity's movement capabilities, determining which terrain types or 
	/// environments it can traverse. 
	/// <para>This is a compile-time fixed set. Add new movement types here and recompile 
	/// as needed to extend the pathfinding system.</para>
	/// </summary>
	[Flags]
	public enum MovementCapability {
		/// <summary>
		/// Indicates the entity has no traversal capabilities and cannot move 
		/// through any terrain. Note: Unlike most enums where 0 is a default/fallback, 
		/// here 0 explicitly represents an inability to move.
		/// </summary>
		None = 0,
		/// <summary>Can traverse ground-based paths.</summary>
		Ground = 1 << 0,
		/// <summary>Can traverse water-based paths.</summary>
		Water = 1 << 1,
		/// <summary>Can traverse air-based paths.</summary>
		Air = 1 << 2,
	}

	/// <summary>
	/// Determines the tactical relationship and targeting matrix rules 
	/// between entities (e.g., friend vs. foe). Political factions or lore groups 
	/// should be tracked via a separate system.
	/// </summary>
	public enum EntityRelation : byte {
		PLAYER = 0,
		ENEMY = 1,
		NEUTRAL = 2,
	}

	/// <summary>
	/// Optimization hint for the spatial grid and physics subsystem. Allows
	/// partitioning of the world scene where STATIC entities are baked/cached into grid systems, 
	/// and DYNAMIC entities are assigned to regular tick updates.
	/// </summary>
	public enum EntityNature : byte {
		/// <summary>
		/// Means the entity will never move or change its state during runtime. This 
		/// allows the entity to be baked into spatial grids and cached for performance.
		/// </summary>
		STATIC = 0,
		/// <summary>
		/// Means the entity is expected to move or change its state during runtime. This
		/// allows the entity to be processed in regular tick updates and excluded from baked caches.
		/// </summary>
		DYNAMIC = 1,
	}

	/// <summary>
	/// Identifies the functional behavior archetype of a dynamic, interactive world object.
	/// Purely static scene objects (like houses or terrain walls) live outside the entity system
	/// and should not be assigned a PropType.
	/// </summary>
	public enum PropType : short {
		HEALTHPACK = 0,
		DUMMY = 1,
		BUFF = 2, // temp for now.
				  // below are not implemented yet but are reserved for future use
		BLESSING_ALTAR = 1000,
		TRAP = 1001,
	}

	/// <summary>
	/// Core gender assignment for game characters and asset definitions.
	/// <para>NOTE:</para>
	/// This enum is intentionally shared between character identities
	/// and asset definitions to support a simple compatibility check
	/// (e.g. character.Gender == asset.Gender) across systems such as character
	/// creation, equipment validation, and cosmetic filtering.
	///<br/><br/>
	/// While separate enums or a dedicated compatibility system could provide
	/// stricter domain separation, they would introduce additional mapping and
	/// maintenance overhead without providing meaningful benefits for this
	/// project's requirements.
	///<br/> <br/> 
	/// A single shared enum keeps the implementation straightforward, efficient,
	/// and easy to reason about.
	/// </summary>
	public enum GenderEnum : byte {
		MALE = 0,
		FEMALE = 1,
		/// <summary>
		/// Indicates that an asset is compatible with both male and female
		/// characters. This value is intended only for asset definitions and
		/// should not be used as a character gender. 
		/// </summary>
		NEUTRAL = 2,
	}

	/// <summary>
	/// Fixed-range identification system for character races, designed with intentional
	/// numerical padding blocks (e.g., 1000-1999 for Humanoids) separated by steps of 10 or 20.
	/// This spacing allows the system to support a simple 1:1 equippable compatibility check while 
	/// offering enough headroom to dynamically introduce dozens of sub-races without overlapping IDs.
	/// </summary>
	public enum RaceEnum : short {

		/// <summary>
		/// Special wildcard for asset definitions that are compatible with all races. Should not be used 
		/// for character identities, as it would break the 1:1 equippable compatibility check and introduce
		///  ambiguity into the system.
		/// </summary>
		All = 9999,

		// Humans (0 - 499)
		HUMAN = 0,
		BARBARIAN = 10,

		// Half-humans (500 - 999)
		HALFELF = 500,
		HALFWOLF = 510,
		HALFCAT = 520,

		// Humanoids (1000 - 1999)
		ELF = 1000,
		ORC = 1020,
		GOBLIN = 1040,
		TROLL = 1060,
		LIZARD = 1080,

		// Angels / Light (2000 - 2999)
		ANGEL = 2000,
		SPIRIT = 2020,
		FAIRY = 2040,

		// Demons / Dark (3000 - 3999)
		DEMON = 3000,
		VAMPIRE = 3020,
		WEREWOLF = 3040,
		UNDEAD = 3060,
	}

	/// <summary>
	/// Base immutable configuration contract representing shared baseline data 
	/// across every entity type. Implemented as a pure C# class to avoid the memory footprint 
	/// and Unity main-thread dependency of ScriptableObjects/MonoBehaviours.
	/// </summary>
	public abstract class EntityConfig {
		public string Name { get; }
		public EntityNature Nature { get; }

		protected EntityConfig(string name, EntityNature nature) {
			this.Name = name;
			this.Nature = nature;
		}
	}

	/// <summary>
	/// Specific configuration data model tailored exclusively for living actors (MOBs).
	/// Enforces structural integrity at creation by ensuring invalid database wildcards (like GenderEnum.neutral) 
	/// are caught immediately at runtime via guard assertions before corrupting any live systems.
	/// </summary>
	public class MobConfig : EntityConfig {
		public EntityRelation Relation { get; }
		public RaceEnum Race { get; }
		public GenderEnum Gender { get; }

		private const string GenderErrorMessage = "Entity Cannot have GenderEnum.neutral since it is reserved for asset definitions. Use GenderEnum.male or GenderEnum.female.";
		private const string RaceErrorMessage = "Entity Cannot have RaceEnum.All since it is reserved for asset definitions. Use a specific race enum value instead.";
		public MobConfig(string name, EntityRelation relation, RaceEnum raceEnum, GenderEnum genderEnum)
			: base(name, EntityNature.DYNAMIC) {
			this.Relation = relation;
			if (raceEnum == RaceEnum.All) {
				throw new System.ArgumentException(RaceErrorMessage);
			}
			this.Race = raceEnum;

			if (genderEnum == GenderEnum.NEUTRAL) {
				throw new System.ArgumentException(GenderErrorMessage);
			}
			this.Gender = genderEnum;

		}
	}
	/// <summary>
	/// Configuration data model dedicated exclusively to environmental and interactive items (PROPs).
	/// Strips away all irrelevant identity fields (relation, race, gender) to eliminate data sparsity 
	/// and drastically optimize memory footprints when thousands of static world items are instantiated.
	/// </summary>
	public class PropConfig : EntityConfig {
		public PropType PropType { get; }

		public PropConfig(string name, PropType propType, EntityNature nature = EntityNature.STATIC)
			: base(name, nature) {
			this.PropType = propType;
		}
	}
}