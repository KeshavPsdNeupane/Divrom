using System;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Interface;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.Tile {

	/// <summary>
	/// Represents terrain and tile classification.
	/// Values are grouped in blocks of 200 per biome to enable range-based biome checks and clean future expansion.
	/// </summary>
	public enum TileType : ushort {
		// ==========================================
		// 0 - 199: TEMPERATE / BASIC TERRAIN
		// ==========================================

		// this represent void or uninitialized tile, which should be treated as non-traversable by default
		// u can imagine this just like a "null" or "void" in MineCraft, where it represents empty 
		// space or uninitialized terrain
		VOID = 0,
		Grass = 1,
		Dirt = 2,
		Mud = 3,
		Stone = 4,
		Forest = 5,
		DenseForest = 6,
		Swamp = 7,
		Marsh = 8,
		Hills = 9,
		Mountain = 10,
		HighMountain = 11,
		Road = 12,
		Bridge = 13,

		// ==========================================
		// 200 - 399: AQUATIC / WATER BIOME
		// ==========================================
		ShallowWater = 200,
		DeepWater = 201,
		Ocean = 202,
		River = 203,
		Reef = 204,
		Waterfall = 205,
		KelpForest = 206,

		// ==========================================
		// 400 - 599: ARID / DESERT / BADLANDS BIOME
		// ==========================================
		Sand = 400,
		SandDune = 401,
		DesertRock = 402,
		Oasis = 403,
		Canyon = 404,
		DryCrackedEarth = 405,
		QuickSand = 406,

		// ==========================================
		// 600 - 799: TUNDRA / SNOW / ICE BIOME
		// ==========================================
		Snow = 600,
		DeepSnow = 601,
		Ice = 602,
		PackedIce = 603,
		FrozenLake = 604,
		Glacier = 605,
		SnowyMountain = 606,

		// ==========================================
		// 800 - 999: VOLCANIC / INFERNAL BIOME
		// ==========================================
		Lava = 800,
		LavaFlow = 801,
		Obsidian = 802,
		Ash = 803,
		Basalt = 804,
		MagmaCrater = 805,

		// ==========================================
		// 1000 - 1199: SUBTERRANEAN / CAVE BIOME
		// ==========================================
		CaveFloor = 1000,
		CaveWall = 1001,
		Chasm = 1002,
		CrystalCave = 1003,
		UndergroundRiver = 1004,
		MushroomForest = 1005,

		// ==========================================
		// 1200 - 1399: URBAN / BUILT STRUCTURES
		// ==========================================
		Cobblestone = 1200,
		WoodenPlanks = 1201,
		StoneWall = 1202,
		CastleFloor = 1203,
		Ruins = 1204,
		DungeonTile = 1205,

		// ==========================================
		// 1400 - 1599: FANTASY / MAGICAL / CORRUPTED
		// ==========================================
		CorruptedGround = 1400,
		BlightedForest = 1401,
		EnchantedGrass = 1402,
		VoidTile = 1403,
		PoisonSwamp = 1404
	}

	[Serializable]
	public struct TileTerrainData : ITerrainData<TileTerrainData> {
		/// <summary>
		/// Sentinel region ID assigned to tiles that do not belong to any traversable region.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Indicates that a tile is uninitialized, out-of-bounds, or intrinsically impassable, 
		/// rendering it completely excluded from pathfinding queries.
		/// </para>
		/// <para>
		/// Unlike valid region IDs (which isolate distinct, connected landmasses), this special 
		/// ID is shared universally across all non-traversable tiles, regardless of whether 
		/// they touch or are physically separated.
		/// </para>
		/// </remarks>
		public const ushort NON_TRAVERSABLE_REGION_ID = 0;
		[SerializeField, Tooltip(
			"Editor visualization color.\n" +
			"• Used for gizmos, editor grid overlays, and debugging visual paths.\n" +
			"• Alpha values below ~0.039 (10/255) default to fully opaque " +
			"to prevent accidental invisible tiles in the editor."
		)]
		private Color _tileColor;

		[SerializeField, Tooltip(
			"Semantic environment identity of this tile.\n" +
			"• Decouples environmental traits from physical pathing costs.\n" +
			"• Enables capability-based logic (e.g., fire entities avoiding water, frost entities avoiding lava).\n" +
			"• 'TileType.Void' denotes uninitialized or intrinsically impassable terrain. Unlike IsTraversable, " +
			"it provides semantic context for why a tile cannot be traversed."
		)]
		private TileType _tileType;

		[SerializeField, Tooltip(
			"Master traversal override toggle.\n" +
			"• Acts as a hard obstacle override (e.g., solid stone walls, outer boundaries).\n" +
			"• When FALSE, blocks ALL entities unconditionally, enabling immediate pathfinding rejection " +
			"before checking individual capabilities."
		)]
		private bool _isTraversable;

		[SerializeField, Tooltip(
			"Which movement capabilities are allowed to cross this tile.\n" +
			"• Bitmask flag specifying supported travel modes (Move, Swim, Fly).\n" +
			"• Can combine flags (e.g. Move | Swim) for hybrid terrain like shallow water."
		)]
		private MovementCapability _allowedCapabilities;

		[SerializeField, Range(0.1f, 10f), Tooltip(
			"Pathfinding cost multiplier when traversing on Ground (Move):\n" +
			"• 1.0 = Baseline standard cost\n" +
			"• < 1.0 = Preferred route (e.g. paved road, smooth stone)\n" +
			"• > 1.0 = Disfavored route (e.g. thick mud, steep hills)\n" +
			"• Modifies AI path evaluation weight (route desire), NOT physical character speed."
		)]
		private float _moveCostMultiplier;

		[SerializeField, Range(0.1f, 10f), Tooltip(
			"Pathfinding cost multiplier when traversing via Water (Swim):\n" +
			"• Defines path weight for swimming entities.\n" +
			"• Lower values encourage aquatic/swimming agents to route through water body channels."
		)]
		private float _swimCostMultiplier;

		[SerializeField, Range(0.1f, 10f), Tooltip(
			"Pathfinding cost multiplier when traversing via Air (Fly):\n" +
			"• Defines path weight for flying units.\n" +
			"• Allows flyers to prefer direct routes across chasms or mountains that block ground units."
		)]
		private float _flyCostMultiplier;

		// Minimum alpha threshold (10/255 ≈ 0.039) to prevent invisible tile rendering issues in the editor.
		private const float MIN_ALPHA_THRESHOLD = 10f / 255f;

		#region Properties

		public readonly Color TileColor {
			get {
				float alpha = (this._tileColor.a < MIN_ALPHA_THRESHOLD) ? 1f : this._tileColor.a;
				return new Color(this._tileColor.r, this._tileColor.g, this._tileColor.b, alpha);
			}
		}

		public readonly TileType TileType => this._tileType;

		/// <summary>Master toggle: returns true if this tile allows traversal at all.</summary>
		public readonly bool IsTraversable => this._isTraversable;

		public readonly MovementCapability AllowedCapabilities => this._allowedCapabilities;

		public readonly float MoveCostMultiplier => this._moveCostMultiplier;
		public readonly float SwimCostMultiplier => this._swimCostMultiplier;
		public readonly float FlyCostMultiplier => this._flyCostMultiplier;

		#endregion

		#region Constructors

		public TileTerrainData(
			Color tileColor,
			TileType biomeType,
			bool isTraversable = true,
			MovementCapability allowedCapabilities = MovementCapability.Move,
			float moveCost = 1f,
			float swimCost = 1f,
			float flyCost = 1f) {
			this._tileColor = tileColor;
			this._tileType = biomeType;
			this._isTraversable = isTraversable;
			this._allowedCapabilities = allowedCapabilities;
			this._moveCostMultiplier = moveCost;
			this._swimCostMultiplier = swimCost;
			this._flyCostMultiplier = flyCost;
		}

		#endregion

		#region Traversal Cost Evaluation

		/// <summary>
		/// Checks if an entity with the given capability can enter this tile.
		/// </summary>
		public readonly bool CanTraverse(MovementCapability agentCapability) {
			// Master override: if tile is not traversable, block everyone immediately
			if (!this._isTraversable) return false;

			return (this._allowedCapabilities & agentCapability) != MovementCapability.NoAbilityToMove;
		}

		/// <summary>
		/// Calculates the best traversal cost multiplier for an agent based on its capabilities.
		/// Returns float.PositiveInfinity if the tile is non-traversable or the agent lacks capabilities.
		/// </summary>
		/// <param name="agentCapability">The entity's movement capabilities (can be combined flags).</param>
		public readonly float GetCostMultiplier(MovementCapability agentCapability) {
			// 1. Master override check
			if (!this._isTraversable) {
				return float.PositiveInfinity;
			}

			MovementCapability validModes = this._allowedCapabilities & agentCapability;

			// 2. Capability check
			if (validModes == MovementCapability.NoAbilityToMove) {
				return float.PositiveInfinity;
			}

			// 3. Find the cheapest movement mode available to this agent for this tile
			float bestCost = float.MaxValue;

			if ((validModes & MovementCapability.Move) != 0 && this._moveCostMultiplier < bestCost) {
				bestCost = this._moveCostMultiplier;
			}
			if ((validModes & MovementCapability.Swim) != 0 && this._swimCostMultiplier < bestCost) {
				bestCost = this._swimCostMultiplier;
			}
			if ((validModes & MovementCapability.Fly) != 0 && this._flyCostMultiplier < bestCost) {
				bestCost = this._flyCostMultiplier;
			}

			return (bestCost == float.MaxValue) ? float.PositiveInfinity : bestCost;
		}

		#endregion

		#region Equality and Overrides

		public readonly bool Equals(TileTerrainData other) {
			return this._tileColor.Equals(other._tileColor) &&
				   this._tileType == other._tileType &&
				   this._isTraversable == other._isTraversable &&
				   this._allowedCapabilities == other._allowedCapabilities &&
				   this._moveCostMultiplier == other._moveCostMultiplier &&
				   this._swimCostMultiplier == other._swimCostMultiplier &&
				   this._flyCostMultiplier == other._flyCostMultiplier;
		}

		public override readonly bool Equals(object obj) {
			return obj is TileTerrainData other && this.Equals(other);
		}

		public override readonly int GetHashCode() {
			return HashCode.Combine(
				this._tileColor,
				this._tileType,
				this._isTraversable,
				this._allowedCapabilities,
				this._moveCostMultiplier,
				this._swimCostMultiplier,
				this._flyCostMultiplier
			);
		}

		public static bool operator ==(TileTerrainData left, TileTerrainData right) {
			return left.Equals(right);
		}

		public static bool operator !=(TileTerrainData left, TileTerrainData right) {
			return !left.Equals(right);
		}

		public override readonly string ToString() {
			return $"TileTerrainData(Biome: {this._tileType}, Traversable: {this._isTraversable}, Allowed: {this._allowedCapabilities}, " +
				   $"MoveCost: {this._moveCostMultiplier}, SwimCost: {this._swimCostMultiplier}, FlyCost: {this._flyCostMultiplier})";
		}

		#endregion
	}
}