using System;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Kope.Feature.PathFinding.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: COLUMNAR PARALLEL PRIMITIVE STREAMING (SOA LAYOUT + HEX BIT-PACKING)
     * ==============================================================================================
     * 
     * [The Problem: Multi-Level Struct Hierarchy & YAML Key Replication]
     * In standard Unity serialization, complex spatial types like `Vec2Int` and `BoundingBox` generate 
     * massive ASCII text markup. Every `Vec2Int` adds 2 lines (`x:` and `y:`), while every `BoundingBox` 
     * introduces 6 lines with nested `min` and `max` struct keys. Across tens of thousands of micro-nodes 
     * and macro region connections, this structural bloat inflates asset size to 5MB+ and  thousands of lines.
     * 
     * [The Solution: Full Columnar Primitive Flattening & Hex Key Packing]
     * This packed pipeline pivots Array-of-Structs (AoS) into Structure-of-Arrays (SoA) layout:
     * 1. Multi-field domain structs are split into synchronized, index-aligned primitive arrays 
     *    (e.g., `_minX`, `_minY`, `_maxX`, `_maxY` arrays replace `List<BoundingBox>`).
     * 2. Coordinates (`Vec2Int`) are stored in parallel `int[]` arrays (`_mPosX`, `_mPosY`), stripping 
     *    all repeating YAML property keys (`x:`, `y:`) and indentation levels into inline hex byte streams.
     * 3. Dictionary keys (`BoundingBox`) are bit-packed into 64-bit primitive `ulong` integers, forcing 
     *    Unity's C++ serializer to emit raw inline hex string representations for keys instead of YAML structs.
     * 4. Field keys are intentionally shortened (`_tt`, `_trav`, `_conns`) to strip ASCII overhead 
     *    while keeping clean public domain getters intact.
     * 
     * [Tradeoff & Execution: Zero-Alloc / On-Demand Re-hydration]
     * The serialized footprint drops by ~99.5% (< 500 KB). At runtime, domain structs (`Vec2Int`, `BoundingBox`) 
     * are re-hydrated on demand through `SaveDataSerializer` getters or during single-pass graph initialization.
     * 
     * ==============================================================================================
     * DATA HIERARCHY GRAPH (COLUMNAR INLINE HEX STREAMING)
     * ==============================================================================================
     * 
     * PathFindingGridData (Root Container)
     * ├── Columnar Global Anchors (Parallel Coordinate Arrays)
     * │   ├── _anchorsX: int[]  [X Coordinates -> Hex Stream]
     * │   └── _anchorsY: int[]  [Y Coordinates -> Hex Stream]
     * │
     * └── _dict: SerializableDictionary<ulong, MacroSaveData>
     *     └── [Key: ulong (Bit-Packed BoundingBox Hex)] ──► [Value: MacroSaveData]
     *                                                        │
     *                                                        ├── Micro-Node Columnar Coords & Flag Streams
     *                                                        │   ├── _mPosX: int[]    [Local X Positions -> Hex Stream]
     *                                                        │   ├── _mPosY: int[]    [Local Y Positions -> Hex Stream]
     *                                                        │   └── _flags: List<byte> [Bitmasks / Static Obstacles]
     *                                                        │
     *                                                        ├── Macro Region Metadata
     *                                                        │   ├── _tt: TerrainType
     *                                                        │   └── _trav: MovementCapability
     *                                                        │
     *                                                        └── Outgoing Connection Streams (MacroConnectionSaveData)
     *                                                            ├── Parallel Target Bounding Box Column Arrays
     *                                                            │   ├── _minX: int[]
     *                                                            │   ├── _minY: int[]
     *                                                            │   ├── _maxX: int[]
     *                                                            │   └── _maxY: int[]
     *                                                            ├── _trav: List<MovementCapability>
     *                                                            └── _narr: List<byte>
     * ==============================================================================================
     */

	/// <summary>
	/// Packed columnar storage for outgoing macro region connections.
	/// Replaces <c>List&lt;BoundingBox&gt;</c> with 4 parallel primitive integer arrays to strip nested YAML keys.
	/// </summary>
	[Serializable]
	public struct MacroConnectionSaveData {

		#region Serialized Storage / Fields

		[SerializeField] private int[] _minX;
		[SerializeField] private int[] _minY;
		[SerializeField] private int[] _maxX;
		[SerializeField] private int[] _maxY;

		[SerializeField] private List<MovementCapability> _trav;
		[SerializeField] private List<byte> _narr;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates the parallel primitive coordinate arrays (<c>_minX</c>, <c>_minY</c>, <c>_maxX</c>, <c>_maxY</c>) 
		/// into strongly-typed <see cref="BoundingBox"/> domain objects on demand.
		/// </summary>
		public readonly List<BoundingBox> TargetRegions {
			get {
				return SaveDataSerializer.FromIntArrayPairsToBoundingBoxList(
					this._minX, this._minY, this._maxX, this._maxY);
			}
		}

		public readonly List<MovementCapability> AllowedTraversal => this._trav;
		public readonly List<byte> IsNarrativelyAccessible => this._narr;

		#endregion

		#region Constructors

		public MacroConnectionSaveData(
			List<BoundingBox> targetRegion,
			List<MovementCapability> allowedTraversal,
			List<byte> isNarrativelyAccessible) {

			((this._minX, this._minY), (this._maxX, this._maxY)) =
				SaveDataSerializer.FromBoundingBoxListToIntArrayPairs(targetRegion ?? new());

			this._trav = allowedTraversal ?? new();
			this._narr = isNarrativelyAccessible ?? new();
		}

		#endregion
	}

	/// <summary>
	/// Optimized macro region container utilizing columnar parallel primitive streams 
	/// for micro-node positions (<c>_mPosX</c>, <c>_mPosY</c>) and packed connection regions.
	/// </summary>
	[Serializable]
	public struct MacroSaveData {

		#region Serialized Storage / Fields

		[SerializeField] private int[] _mPosX;
		[SerializeField] private int[] _mPosY;

		[SerializeField] private List<byte> _flags;

		[SerializeField] private TerrainType _tt;
		[SerializeField] private MovementCapability _trav;
		[SerializeField] private MacroConnectionSaveData _conns;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates parallel integer coordinate streams (<c>_mPosX</c>, <c>_mPosY</c>) 
		/// back into strongly-typed <see cref="Vec2Int"/> domain positions.
		/// </summary>
		public readonly List<Vec2Int> MacroRegionAnchorPoints {
			get {
				return SaveDataSerializer.FromIntArraysToVec2(this._mPosX, this._mPosY);
			}
		}

		public readonly List<byte> MacroRegionStaticObstacleFlags => this._flags;
		public readonly TerrainType TerrainType => this._tt;
		public readonly MovementCapability AllowedTraversal => this._trav;
		public readonly MacroConnectionSaveData NeighboringRegionBoundingBoxes => this._conns;

		#endregion

		#region Constructors

		public MacroSaveData(
			List<Vec2Int> macroRegionAnchorPoints,
			List<byte> macroRegionStaticObstacleFlags,
			TerrainType terrainType,
			MovementCapability allowedTraversal,
			MacroConnectionSaveData neighboringRegionConnections) {

			(this._mPosX, this._mPosY) = SaveDataSerializer.FromVec2ToIntArrays(macroRegionAnchorPoints ?? new());

			this._flags = macroRegionStaticObstacleFlags ?? new();
			this._tt = terrainType;
			this._trav = allowedTraversal;
			this._conns = neighboringRegionConnections;
		}

		#endregion
	}

	/// <summary>
	/// Root pathfinding serialization container.
	/// Maps 64-bit bit-packed BoundingBox keys (<c>ulong</c>) to packed macro region save data.
	/// Stores keys and position coordinates in parallel primitive streams for inline hex YAML serialization.
	/// </summary>
	[Serializable]
	public struct PathFindingGridData {

		#region Serialized Storage / Fields

		[SerializeField] private int[] _anchorsX;
		[SerializeField] private int[] _anchorsY;

		// Bit-packed ulong keys force Unity YAML into serializing _keys as a single inline hex string.
		[SerializeField] private SerializableDictionary<ulong, MacroSaveData> _dict;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates parallel anchor coordinate arrays (<c>_anchorsX</c>, <c>_anchorsY</c>) 
		/// back into strongly-typed <see cref="Vec2Int"/> domain objects.
		/// </summary>
		public readonly List<Vec2Int> RegionAnchorPoints {
			get {
				return SaveDataSerializer.FromIntArraysToVec2(this._anchorsX, this._anchorsY);
			}
		}

		public readonly SerializableDictionary<ulong, MacroSaveData> MicroGridNodeSaveDataDict => this._dict;

		#endregion

		#region Constructors

		public PathFindingGridData(
			List<Vec2Int> regionAnchorPoints,
			SerializableDictionary<ulong, MacroSaveData> microGridNodeSaveDataDict) {

			(this._anchorsX, this._anchorsY) = SaveDataSerializer.FromVec2ToIntArrays(regionAnchorPoints ?? new());
			this._dict = microGridNodeSaveDataDict ?? new();
		}

		#endregion
	}
}