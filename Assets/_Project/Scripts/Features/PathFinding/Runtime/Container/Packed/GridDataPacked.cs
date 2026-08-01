using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingOld.Node;
using UnityEngine;

namespace Kope.Feature.PathFindingOld.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: PACKED 64-BIT / 128-BIT PRIMITIVE STREAMING (SOA LAYOUT)
     * ==============================================================================================
     * 
     * [The Problem: Multi-Level Struct Hierarchy & YAML Key Replication]
     * In standard Unity serialization, complex spatial types like `Vec2Int` and `BoundingBox` generate 
     * massive ASCII text markup. Every `Vec2Int` adds 2 lines (`x:` and `y:`), while every `BoundingBox` 
     * introduces 6 lines with nested `min` and `max` struct keys.
     * 
     * [The Solution: Single `long` and Dual `long` Primitive Streams + SoA Metadata]
     * To eliminate structural bloat while maintaining full +/- 2.14 billion 32-bit coordinate precision per axis:
     * 
     * 1. Positions (`Vec2Int`): Packed into a single `long` (64-bit word: High-32 X, Low-32 Y).
     * 2. Bounding Boxes (`BoundingBox`): Packed into 2 `long` words (`_minPos` and `_maxPos` 64-bit pairs).
     * 3. Grid Keys & Region Metadata: Maintained as parallel columnar streams on `PathFindingGridData`:
     *    - 128-bit key pairs (`_keysHigh`, `_keysLow` ulong arrays)
     *    - Parallel enum/flag streams (`_tt` TerrainType[], `_trav` MovementCapability[], `_narr` byte[])
     *    - Synchronized value payloads (`_values` MacroSaveDataPacked[])
     * 
     * This layout forces Unity's C++ serializer to emit compact inline hex byte arrays for all spatial data.
     * 
     * [Why `_trav` / `_narr` live only at the top level]
     * A macro region's `MovementCapability` and narrative-accessibility are properties of the REGION,
     * not of any individual edge. Per-connection values are a pure `OR` (capability) / `AND` (narrative
     * access) combination of the two connected regions' own values (see `MacroConnectionData.CreateConnection`).
     * Persisting that combination per-edge is redundant — it's cheaper to store each region's own value
     * once and recompute the combined edge value on demand during hydration.
     * 
     * ==============================================================================================
     * DATA HIERARCHY GRAPH (PACKED PRIMITIVE STREAMING)
     * ==============================================================================================
     * 
     * PathFindingGridDataPacked (Root Container)
     * ├── Global Anchors (Packed 64-Bit Positions)
     * │   └── _anchors: long[]             [Packed X/Y Vec2Int -> Single Hex Stream]
     * │
     * └── 128-Bit Columnar Key/Value Streams (SoA Layout)
     *     ├── _keysHigh: ulong[]           [Min.X/Y Packed 64-bit Hex Words]
     *     ├── _keysLow: ulong[]            [Max.X/Y Packed 64-bit Hex Words]
     *     ├── _tt: TerrainType[]           [Macro Region Terrain Types]
     *     ├── _trav: MovementCapability[]  [Macro Region's OWN Movement Capability]
     *     ├── _narr: byte[]                [Macro Region's OWN Narrative Accessibility]
     *     └── _values: MacroSaveDataPacked[]     [Synchronized Macro Values]
     *          │
     *          ├── Micro-Node Position & Flag Streams
     *          │   ├── _mPos: long[]       [Local X/Y Packed Vec2Int -> Single Hex Stream]
     *          │   └── _flags: byte[]      [Bitmasks / Static Obstacles]
     *          │
     *          └── Outgoing Connection Streams (MacroConnectionSaveDataPacked)
     *              └── Target Bounding Box Dual Long Streams (2 Longs per Region) ONLY.
     *                  ├── _minPos: long[] [Packed Min.X/Y Vec2Int Hex Stream]
     *                  └── _maxPos: long[] [Packed Max.X/Y Vec2Int Hex Stream]
     *                  (traversal/narrative access for each edge are derived at hydrate time
     *                   from the top-level _trav/_narr of both endpoints, via CreateConnection)
     * ==============================================================================================
     */

	/// <summary>
	/// Packed columnar storage for outgoing macro region connections.
	/// Stores only the target region bounding boxes (<c>_minPos</c>, <c>_maxPos</c>) as bit-packed <c>long[]</c> arrays.
	/// Per-connection <c>MovementCapability</c> and narrative-access are intentionally NOT stored here —
	/// they were previously a pure combination (OR / AND) of the two connected regions' own top-level
	/// values, so they're redundant on disk. They're recomputed at hydration time via
	/// <c>MacroConnectionData.CreateConnection</c> using each endpoint's own data (see <see cref="GridDataPacked"/>).
	/// </summary>
	[Serializable]
	public struct MacroConnectionSaveDataPacked {

		#region Serialized Storage / Fields

		[SerializeField] private long[] _minPos;
		[SerializeField] private long[] _maxPos;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates the dual long coordinate streams (<c>_minPos</c>, <c>_maxPos</c>) 
		/// into strongly-typed <see cref="BoundingBox"/> domain objects on demand.
		/// </summary>
		public readonly List<BoundingBox> TargetRegions =>
			SpatialBitPacker.UnpackBoundingBoxList(this._minPos, this._maxPos);

		#endregion

		#region Constructors

		public MacroConnectionSaveDataPacked(List<BoundingBox> targetRegion) {
			(this._minPos, this._maxPos) = SpatialBitPacker.PackBoundingBoxList(targetRegion ?? new());
		}

		public MacroConnectionSaveDataPacked(BoundingBox[] targetRegion) {
			(this._minPos, this._maxPos) = SpatialBitPacker.PackBoundingBoxList(
				targetRegion != null ? new List<BoundingBox>(targetRegion) : new());
		}

		#endregion
	}

	/// <summary>
	/// Optimized macro region container utilizing 64-bit packed position streams (<c>_mPos</c>)
	/// and connection data. Region-level metadata (<c>TerrainType</c>, <c>MovementCapability</c>,
	/// narrative-accessibility) is stored in top-level parallel arrays on <see cref="GridDataPacked"/>.
	/// </summary>
	[Serializable]
	public struct MacroSaveDataPacked {

		#region Serialized Storage / Fields

		[SerializeField] private long[] _mPos;
		[SerializeField] private byte[] _flags;

		[SerializeField] private MacroConnectionSaveDataPacked _conns;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates the 64-bit packed position stream (<c>_mPos</c>) 
		/// back into strongly-typed <see cref="Vec2Int"/> domain positions.
		/// </summary>
		public readonly List<Vec2Int> MacroRegionAnchorPoints =>
			SpatialBitPacker.UnpackVec2List(this._mPos);

		public readonly byte[] MacroRegionStaticObstacleFlags => this._flags;
		public readonly MacroConnectionSaveDataPacked NeighboringRegionBoundingBoxes => this._conns;

		#endregion

		#region Constructors

		public MacroSaveDataPacked(
			List<Vec2Int> macroRegionAnchorPoints,
			List<byte> macroRegionStaticObstacleFlags,
			MacroConnectionSaveDataPacked neighboringRegionConnections) {

			this._mPos = SpatialBitPacker.PackVec2List(macroRegionAnchorPoints ?? new());
			this._flags = macroRegionStaticObstacleFlags != null ? macroRegionStaticObstacleFlags.ToArray() : Array.Empty<byte>();
			this._conns = neighboringRegionConnections;
		}

		public MacroSaveDataPacked(
			List<Vec2Int> macroRegionAnchorPoints,
			byte[] macroRegionStaticObstacleFlags,
			MacroConnectionSaveDataPacked neighboringRegionConnections) {

			this._mPos = SpatialBitPacker.PackVec2List(macroRegionAnchorPoints ?? new());
			this._flags = macroRegionStaticObstacleFlags ?? Array.Empty<byte>();
			this._conns = neighboringRegionConnections;
		}

		#endregion
	}

	/// <summary>
	/// Root pathfinding serialization container.
	/// Stores packed 64-bit anchor coordinates (<c>_anchors</c>), 128-bit bit-packed BoundingBox keys,
	/// and top-level parallel metadata arrays (<c>_tt</c>, <c>_trav</c>, <c>_narr</c>) aligned with <c>_values</c>.
	/// </summary>
	[Serializable]
	public struct GridDataPacked {

		#region Serialized Storage / Fields

		[SerializeField] private long[] _anchors;

		// Parallel 128-bit key streams + metadata columns + value array
		[SerializeField] private ulong[] _keysHigh;
		[SerializeField] private ulong[] _keysLow;

		[SerializeField] private TerrainType[] _tt;
		[SerializeField] private MovementCapability[] _trav;
		[SerializeField] private byte[] _narr;

		[SerializeField] private MacroSaveDataPacked[] _values;

		#endregion

		#region Domain Properties (On-Demand Re-hydration)

		/// <summary>
		/// Rehydrates the packed anchor position stream (<c>_anchors</c>) 
		/// back into strongly-typed <see cref="Vec2Int"/> domain objects.
		/// </summary>
		public readonly List<Vec2Int> RegionAnchorPoints =>
			SpatialBitPacker.UnpackVec2List(this._anchors);

		public readonly ulong[] KeysHigh => this._keysHigh;
		public readonly ulong[] KeysLow => this._keysLow;
		public readonly TerrainType[] TerrainTypes => this._tt;
		public readonly MovementCapability[] AllowedTraversal => this._trav;
		public readonly byte[] IsNarrativelyAccessible => this._narr;
		public readonly MacroSaveDataPacked[] Values => this._values;

		/// <summary>
		/// Re-hydrates the parallel key/value streams into a runtime dictionary indexed by <see cref="BoundingBox"/>.
		/// </summary>
		public readonly Dictionary<BoundingBox, MacroSaveDataPacked> ToBoundingBoxDictionary() {
			int count = this._values != null ? this._values.Length : 0;
			Dictionary<BoundingBox, MacroSaveDataPacked> dict = new(count);

			for (int i = 0; i < count; i++) {
				BoundingBox box = SpatialBitPacker.UnpackBoundingBoxUnsigned(this._keysHigh[i], this._keysLow[i]);
				dict[box] = this._values[i];
			}

			return dict;
		}

		/// <summary>
		/// Re-hydrates the parallel key/value streams into a runtime dictionary indexed by packed <c>(ulong high, ulong low)</c> tuples.
		/// Useful for high-performance direct key lookups without instantiating <see cref="BoundingBox"/> structs.
		/// </summary>
		public readonly Dictionary<(ulong high, ulong low), MacroSaveDataPacked> ToTupleDictionary() {
			int count = this._values != null ? this._values.Length : 0;
			Dictionary<(ulong high, ulong low), MacroSaveDataPacked> dict = new(count);

			for (int i = 0; i < count; i++) {
				dict[(this._keysHigh[i], this._keysLow[i])] = this._values[i];
			}

			return dict;
		}

		#endregion

		#region Constructors

		public GridDataPacked(
			List<Vec2Int> regionAnchorPoints,
			List<ulong> keysHigh,
			List<ulong> keysLow,
			List<TerrainType> terrainTypes,
			List<MovementCapability> allowedTraversal,
			List<byte> isNarrativelyAccessible,
			List<MacroSaveDataPacked> values) {

			this._anchors = SpatialBitPacker.PackVec2List(regionAnchorPoints ?? new());
			this._keysHigh = keysHigh != null ? keysHigh.ToArray() : Array.Empty<ulong>();
			this._keysLow = keysLow != null ? keysLow.ToArray() : Array.Empty<ulong>();
			this._tt = terrainTypes != null ? terrainTypes.ToArray() : Array.Empty<TerrainType>();
			this._trav = allowedTraversal != null ? allowedTraversal.ToArray() : Array.Empty<MovementCapability>();
			this._narr = isNarrativelyAccessible != null ? isNarrativelyAccessible.ToArray() : Array.Empty<byte>();
			this._values = values != null ? values.ToArray() : Array.Empty<MacroSaveDataPacked>();
		}

		public GridDataPacked(
			List<Vec2Int> regionAnchorPoints,
			ulong[] keysHigh,
			ulong[] keysLow,
			TerrainType[] terrainTypes,
			MovementCapability[] allowedTraversal,
			byte[] isNarrativelyAccessible,
			MacroSaveDataPacked[] values) {

			this._anchors = SpatialBitPacker.PackVec2List(regionAnchorPoints ?? new());
			this._keysHigh = keysHigh ?? Array.Empty<ulong>();
			this._keysLow = keysLow ?? Array.Empty<ulong>();
			this._tt = terrainTypes ?? Array.Empty<TerrainType>();
			this._trav = allowedTraversal ?? Array.Empty<MovementCapability>();
			this._narr = isNarrativelyAccessible ?? Array.Empty<byte>();
			this._values = values ?? Array.Empty<MacroSaveDataPacked>();
		}

		public GridDataPacked(
			List<Vec2Int> regionAnchorPoints,
			Dictionary<BoundingBox, MacroSaveDataPacked> boundingBoxDict,
			TerrainType[] terrainTypes,
			MovementCapability[] allowedTraversal,
			byte[] isNarrativelyAccessible) {

			this._anchors = SpatialBitPacker.PackVec2List(regionAnchorPoints ?? new());

			int count = boundingBoxDict?.Count ?? 0;
			this._keysHigh = new ulong[count];
			this._keysLow = new ulong[count];
			this._tt = terrainTypes ?? Array.Empty<TerrainType>();
			this._trav = allowedTraversal ?? Array.Empty<MovementCapability>();
			this._narr = isNarrativelyAccessible ?? Array.Empty<byte>();
			this._values = new MacroSaveDataPacked[count];

			if (boundingBoxDict != null) {
				int index = 0;
				foreach (var kvp in boundingBoxDict) {
					var (high, low) = SpatialBitPacker.PackBoundingBoxUnsigned(kvp.Key);
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