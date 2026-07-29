using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Kope.Feature.PathFinding.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: COLUMNAR PARALLEL PRIMITIVE STREAMING (SOA LAYOUT + 128-BIT HEX KEYS)
     * ==============================================================================================
     * 
     * [The Problem: Multi-Level Struct Hierarchy & YAML Key Replication]
     * In standard Unity serialization, complex spatial types like `Vec2Int` and `BoundingBox` generate 
     * massive ASCII text markup. Every `Vec2Int` adds 2 lines (`x:` and `y:`), while every `BoundingBox` 
     * introduces 6 lines with nested `min` and `max` struct keys. Across tens of thousands of micro-nodes 
     * and macro region connections, this structural bloat inflates asset size to 5MB+ and thousands of lines.
     * 
     * [The Solution: 3-Way Structure-of-Arrays (SoA) for 128-Bit Key Pairs]
     * Because Unity cannot serialize native 128-bit primitives or dictionaries, `PathFindingGridData` avoids 
     * dictionary wrapper structs altogether. Instead, it uses a 3-way parallel columnar stream:
     * 1. `_keysHigh`: `ulong[]` containing packed Min.X / Min.Y 32-bit coordinates.
     * 2. `_keysLow`: `ulong[]` containing packed Max.X / Max.Y 32-bit coordinates.
     * 3. `_values`: `MacroSaveData[]` index-aligned directly with `_keysHigh` and `_keysLow`.
     * 
     * This layout forces Unity's C++ serializer to emit compact inline hex byte arrays for keys 
     * while granting full +/- 2.14 billion 32-bit coordinate precision per axis.
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
     * └── 128-Bit Columnar Key/Value Streams (SoA Layout)
     *     ├── _keysHigh: ulong[]            [Min.X/Y Packed 64-bit Hex Words]
     *     ├── _keysLow: ulong[]             [Max.X/Y Packed 64-bit Hex Words]
     *     └── _values: MacroSaveData[]      [Synchronized Macro Values]
     *          │
     *          ├── Micro-Node Columnar Coords & Flag Streams
     *          │   ├── _mPosX: int[]        [Local X Positions -> Hex Stream]
     *          │   ├── _mPosY: int[]        [Local Y Positions -> Hex Stream]
     *          │   └── _flags: byte[]       [Bitmasks / Static Obstacles]
     *          │
     *          ├── Macro Region Metadata
     *          │   ├── _tt: TerrainType
     *          │   └── _trav: MovementCapability
     *          │
     *          └── Outgoing Connection Streams (MacroConnectionSaveData)
     *              ├── Parallel Target Bounding Box Column Arrays
     *              │   ├── _minX: int[]
     *              │   ├── _minY: int[]
     *              │   ├── _maxX: int[]
     *              │   └── _maxY: int[]
     *              ├── _trav: MovementCapability[]
     *              └── _narr: byte[]
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

		[SerializeField] private MovementCapability[] _trav;
		[SerializeField] private byte[] _narr;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates the parallel primitive coordinate arrays (<c>_minX</c>, <c>_minY</c>, <c>_maxX</c>, <c>_maxY</c>) 
		/// into strongly-typed <see cref="BoundingBox"/> domain objects on demand.
		/// </summary>
		public readonly List<BoundingBox> TargetRegions =>
			SaveDataSerializer.FromIntArrayPairsToBoundingBoxList(this._minX, this._minY, this._maxX, this._maxY);

		public readonly MovementCapability[] AllowedTraversal => this._trav;
		public readonly byte[] IsNarrativelyAccessible => this._narr;

		#endregion

		#region Constructors

		public MacroConnectionSaveData(
			List<BoundingBox> targetRegion,
			List<MovementCapability> allowedTraversal,
			List<byte> isNarrativelyAccessible) {

			((this._minX, this._minY), (this._maxX, this._maxY)) =
				SaveDataSerializer.FromBoundingBoxListToIntArrayPairs(targetRegion ?? new());

			this._trav = allowedTraversal != null ? allowedTraversal.ToArray() : Array.Empty<MovementCapability>();
			this._narr = isNarrativelyAccessible != null ? isNarrativelyAccessible.ToArray() : Array.Empty<byte>();
		}

		public MacroConnectionSaveData(
			List<BoundingBox> targetRegion,
			MovementCapability[] allowedTraversal,
			byte[] isNarrativelyAccessible) {

			((this._minX, this._minY), (this._maxX, this._maxY)) =
				SaveDataSerializer.FromBoundingBoxListToIntArrayPairs(targetRegion ?? new());

			this._trav = allowedTraversal ?? Array.Empty<MovementCapability>();
			this._narr = isNarrativelyAccessible ?? Array.Empty<byte>();
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

		[SerializeField] private byte[] _flags;

		[SerializeField] private TerrainType _tt;
		[SerializeField] private MovementCapability _trav;
		[SerializeField] private MacroConnectionSaveData _conns;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates parallel integer coordinate streams (<c>_mPosX</c>, <c>_mPosY</c>) 
		/// back into strongly-typed <see cref="Vec2Int"/> domain positions.
		/// </summary>
		public readonly List<Vec2Int> MacroRegionAnchorPoints =>
			SaveDataSerializer.FromIntArraysToVec2(this._mPosX, this._mPosY);

		public readonly byte[] MacroRegionStaticObstacleFlags => this._flags;
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

			this._flags = macroRegionStaticObstacleFlags != null ? macroRegionStaticObstacleFlags.ToArray() : Array.Empty<byte>();
			this._tt = terrainType;
			this._trav = allowedTraversal;
			this._conns = neighboringRegionConnections;
		}

		public MacroSaveData(
			List<Vec2Int> macroRegionAnchorPoints,
			byte[] macroRegionStaticObstacleFlags,
			TerrainType terrainType,
			MovementCapability allowedTraversal,
			MacroConnectionSaveData neighboringRegionConnections) {

			(this._mPosX, this._mPosY) = SaveDataSerializer.FromVec2ToIntArrays(macroRegionAnchorPoints ?? new());

			this._flags = macroRegionStaticObstacleFlags ?? Array.Empty<byte>();
			this._tt = terrainType;
			this._trav = allowedTraversal;
			this._conns = neighboringRegionConnections;
		}

		#endregion
	}

	/// <summary>
	/// Root pathfinding serialization container.
	/// Stores 128-bit bit-packed BoundingBox keys in parallel primitive streams (<c>_keysHigh</c>, <c>_keysLow</c>)
	/// aligned with a parallel list of macro values (<c>_values</c>).
	/// </summary>
	[Serializable]
	public struct PathFindingGridData {

		#region Serialized Storage / Fields

		[SerializeField] private int[] _anchorsX;
		[SerializeField] private int[] _anchorsY;

		// Parallel 128-bit key streams + value array (Structure-of-Arrays)
		[SerializeField] private ulong[] _keysHigh;
		[SerializeField] private ulong[] _keysLow;
		[SerializeField] private MacroSaveData[] _values;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates parallel anchor coordinate arrays (<c>_anchorsX</c>, <c>_anchorsY</c>) 
		/// back into strongly-typed <see cref="Vec2Int"/> domain objects.
		/// </summary>
		public readonly List<Vec2Int> RegionAnchorPoints =>
			SaveDataSerializer.FromIntArraysToVec2(this._anchorsX, this._anchorsY);

		public readonly ulong[] KeysHigh => this._keysHigh;
		public readonly ulong[] KeysLow => this._keysLow;
		public readonly MacroSaveData[] Values => this._values;

		/// <summary>
		/// Re-hydrates the parallel key/value streams into a runtime dictionary indexed by <see cref="BoundingBox"/>.
		/// </summary>
		public readonly Dictionary<BoundingBox, MacroSaveData> ToBoundingBoxDictionary() {
			int count = this._values != null ? this._values.Length : 0;
			Dictionary<BoundingBox, MacroSaveData> dict = new(count);

			for (int i = 0; i < count; i++) {
				BoundingBox box = SaveDataSerializer.UnpackBoundingBox32(this._keysHigh[i], this._keysLow[i]);
				dict[box] = this._values[i];
			}

			return dict;
		}

		/// <summary>
		/// Re-hydrates the parallel key/value streams into a runtime dictionary indexed by packed <c>(ulong high, ulong low)</c> tuples.
		/// Useful for high-performance direct key lookups without instantiating <see cref="BoundingBox"/> structs.
		/// </summary>
		public readonly Dictionary<(ulong high, ulong low), MacroSaveData> ToTupleDictionary() {
			int count = this._values != null ? this._values.Length : 0;
			Dictionary<(ulong high, ulong low), MacroSaveData> dict = new(count);

			for (int i = 0; i < count; i++) {
				dict[(this._keysHigh[i], this._keysLow[i])] = this._values[i];
			}

			return dict;
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Direct columnar constructor for pre-split key/value streams.
		/// </summary>
		public PathFindingGridData(
			List<Vec2Int> regionAnchorPoints,
			List<ulong> keysHigh,
			List<ulong> keysLow,
			List<MacroSaveData> values) {

			(this._anchorsX, this._anchorsY) = SaveDataSerializer.FromVec2ToIntArrays(regionAnchorPoints ?? new());
			this._keysHigh = keysHigh != null ? keysHigh.ToArray() : Array.Empty<ulong>();
			this._keysLow = keysLow != null ? keysLow.ToArray() : Array.Empty<ulong>();
			this._values = values != null ? values.ToArray() : Array.Empty<MacroSaveData>();
		}

		/// <summary>
		/// Direct columnar constructor for pre-split array streams.
		/// </summary>
		public PathFindingGridData(
			List<Vec2Int> regionAnchorPoints,
			ulong[] keysHigh,
			ulong[] keysLow,
			MacroSaveData[] values) {

			(this._anchorsX, this._anchorsY) = SaveDataSerializer.FromVec2ToIntArrays(regionAnchorPoints ?? new());
			this._keysHigh = keysHigh ?? Array.Empty<ulong>();
			this._keysLow = keysLow ?? Array.Empty<ulong>();
			this._values = values ?? Array.Empty<MacroSaveData>();
		}

		/// <summary>
		/// Convenience constructor that accepts a strongly-typed domain dictionary indexed by <see cref="BoundingBox"/>,
		/// automatically bit-packing keys into 128-bit parallel high/low primitive streams via <c>SaveDataSerializer</c>.
		/// </summary>
		public PathFindingGridData(
			List<Vec2Int> regionAnchorPoints,
			Dictionary<BoundingBox, MacroSaveData> boundingBoxDict) {

			(this._anchorsX, this._anchorsY) = SaveDataSerializer.FromVec2ToIntArrays(regionAnchorPoints ?? new());

			int count = boundingBoxDict?.Count ?? 0;
			this._keysHigh = new ulong[count];
			this._keysLow = new ulong[count];
			this._values = new MacroSaveData[count];

			if (boundingBoxDict != null) {
				int index = 0;
				foreach (var kvp in boundingBoxDict) {
					var (high, low) = SaveDataSerializer.PackBoundingBox32(kvp.Key);
					this._keysHigh[index] = high;
					this._keysLow[index] = low;
					this._values[index] = kvp.Value;
					index++;
				}
			}
		}

		#endregion
	}
}