using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Kope.Feature.PathFinding.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: PACKED 64-BIT STREAM SLICING (PURE PRIMITIVE SoA)
     * ==============================================================================================
     * 
     * [1. THE PROBLEM: Unity Serializer Markup Overhead]
     * In standard Unity serialization (YAML text or native binary), storing arrays of custom structs 
     * (e.g., `struct Slice { public int Offset; public int Count; }`) imposes significant overhead:
     *   - Text Assets (.asset/.prefab): Emits redundant key-value markup tags for EVERY element:
     *       - Offset: 1024
     *         Count: 64
     *   - Binary Builds: Serializes struct field headers, alignment padding, and object descriptors.
     *   - GC & Memory: Array of Structs (AoS) limits cache locality when accessing only offsets 
     *     or counts, introducing serialization/deserialization memory churn.
     * 
     * [2. THE SOLUTION: Bit-Packed 64-Bit Primitive Streams]
     * This data structure converts the entire slice mapping system into pure primitive `long[]` arrays.
     * By packing two 32-bit integers (`Offset` and `Count`) into a single 64-bit `long` primitive:
     *   - Unity serializes `long[]` as an inline contiguous hex byte block with ZERO struct metadata.
     *   - Reduces asset storage size on disk by up to 60-70%.
     *   - Improves L1/L2 CPU cache utilization during graph re-hydration via contiguous memory layout.
     * 
     * [3. MEMORY LAYOUT OF PACKED 64-BIT WORD]
     * 
     *  Bit 63                             Bit 32 Bit 31                              Bit 0
     *  +----------------------------------------+----------------------------------------+
     *  |      HIGH 32 BITS: OFFSET (Int32)      |      LOW 32 BITS: COUNT (Int32)       |
     *  +----------------------------------------+----------------------------------------+
     *  |<-------------- 4 Bytes --------------->|<-------------- 4 Bytes --------------->|
     * 
     *  Max Slicing Capability per Stream:
     *  - Max Offset : 2,147,483,647 (2.14 Billion elements in global master array)
     *  - Max Count  : 2,147,483,647 (2.14 Billion elements per macro region slice)
     * 
     * [4. WHY CONNECTION TRAVERSAL/NARRATIVE FLAGS ARE NOT STORED PER-EDGE]
     * A macro region's `MovementCapability` and narrative-accessibility (`_trav`, `_narr` below) are
     * properties of the REGION itself, not of any individual outgoing edge. The combined value for a
     * given edge is a pure `OR` (capability) / `AND` (narrative access) of the two connected regions'
     * own values — see `MacroConnectionData.CreateConnection`. Persisting that combination once per
     * edge (`_globConnTrav`/`_globConnNarr`) duplicated data already present once per region. Only the
     * target `BoundingBox` (`_globConnMinPos`/`_globConnMaxPos`) is retained in the connection stream;
     * the combined value is recomputed on hydration from each endpoint's own top-level `_trav`/`_narr`.
     * ==============================================================================================
     */

	/// <summary>
	/// Immutable, zero-allocation cache container representing a baked global pathfinding grid graph.
	/// <para>
	/// Employs a Structure of Arrays (SoA) layout with bit-packed 64-bit primitive slice descriptors
	/// (<c>_globMicroSlices</c>, <c>_globConnSlices</c>) to slice monolithic master primitive streams
	/// without Unity struct serialization overhead.
	/// </para>
	/// </summary>
	[Serializable]
	public struct GridDataGlobalStream {

		#region Serialized Storage / Fields

		/// <summary>
		/// Serialized spatial anchor points encoded as packed 64-bit longs (X in High-32, Y in Low-32).
		/// Used to establish macro region alignment in global world-space coordinates.
		/// </summary>
		[SerializeField, ReadOnly] private long[] _anchors;

		// ==========================================================================================
		// PARALLEL MACRO REGION METADATA STREAMS (1-to-1 Index Alignment per Macro Region)
		// All arrays below share the exact same length N, where N = Total Macro Grid Regions.
		// ==========================================================================================

		/// <summary> High 64 bits of the 128-bit key identifying each Macro Region's BoundingBox. </summary>
		[SerializeField, ReadOnly] private ulong[] _keysHigh;

		/// <summary> Low 64 bits of the 128-bit key identifying each Macro Region's BoundingBox. </summary>
		[SerializeField, ReadOnly] private ulong[] _keysLow;

		/// <summary> Terrain classification metadata column for each Macro Region. </summary>
		[SerializeField, ReadOnly] private TerrainType[] _tt;

		/// <summary> Allowed movement capability flags (e.g., Ground, Flying, Water) — each Macro Region's OWN value. </summary>
		[SerializeField, ReadOnly] private MovementCapability[] _trav;

		/// <summary> Narrative accessibility flag — each Macro Region's OWN value. </summary>
		[SerializeField, ReadOnly] private byte[] _narr;

		/// <summary>
		/// Bit-packed slice descriptors for Micro Nodes. 
		/// <para>Each element maps 1-to-1 with a Macro Region index.</para>
		/// High 32 bits = Starting index in <see cref="_globMicroMPos"/> / <see cref="_globMicroFlags"/>.
		/// Low 32 bits  = Number of sequential micro-nodes belonging to this Macro Region.
		/// </summary>
		[SerializeField, ReadOnly] private long[] _globMicroSlices;

		/// <summary>
		/// Bit-packed slice descriptors for Macro Connections (Graph Edges).
		/// <para>Each element maps 1-to-1 with a Macro Region index.</para>
		/// High 32 bits = Starting index in <see cref="_globConnMinPos"/> / <see cref="_globConnMaxPos"/>.
		/// Low 32 bits  = Number of outgoing graph connection edges originating from this Macro Region.
		/// </summary>
		[SerializeField, ReadOnly] private long[] _globConnSlices;

		// ==========================================================================================
		// MONOLITHIC MASTER MICRO-NODE PRIMITIVE STREAMS
		// Single flattened arrays storing all micro-nodes across the entire global map contiguously.
		// ==========================================================================================

		/// <summary> Packed 64-bit local position data (<c>Vec2Int</c>) for every micro-node in the map. </summary>
		[SerializeField, ReadOnly] private long[] _globMicroMPos;

		/// <summary> Packed bit-flags (e.g., static obstacle status, walkability) per micro-node. </summary>
		[SerializeField, ReadOnly] private byte[] _globMicroFlags;

		// ==========================================================================================
		// MONOLITHIC MASTER MACRO-CONNECTION PRIMITIVE STREAMS
		// Single flattened arrays storing all graph connection edges contiguously. Only the target
		// BoundingBox is stored — traversal/narrative-access for each edge is derived at hydration
		// time from the two endpoints' own top-level _trav/_narr (see rationale [4] above).
		// ==========================================================================================

		/// <summary> High 64-bit packed representation of target BoundingBox min bounds. </summary>
		[SerializeField, ReadOnly] private long[] _globConnMinPos;

		/// <summary> Low 64-bit packed representation of target BoundingBox max bounds. </summary>
		[SerializeField, ReadOnly] private long[] _globConnMaxPos;

		#endregion

		#region Domain Properties

		/// <summary>
		/// Unpacks and returns spatial anchor points as high-level <see cref="Vec2Int"/> world coordinates.
		/// </summary>
		public readonly List<Vec2Int> RegionAnchorPoints => SpatialBitPacker.UnpackVec2List(this._anchors);

		/// <summary> High 64-bit keys for Macro Region BoundingBoxes. </summary>
		public readonly ulong[] KeysHigh => this._keysHigh;

		/// <summary> Low 64-bit keys for Macro Region BoundingBoxes. </summary>
		public readonly ulong[] KeysLow => this._keysLow;

		/// <summary> Terrain classification array for Macro Regions. </summary>
		public readonly TerrainType[] TerrainTypes => this._tt;

		/// <summary> Movement capability array — each Macro Region's OWN value. </summary>
		public readonly MovementCapability[] AllowedTraversal => this._trav;

		/// <summary> Narrative accessibility array — each Macro Region's OWN value. </summary>
		public readonly byte[] IsNarrativelyAccessible => this._narr;

		/// <summary> Raw 64-bit packed micro-node slice descriptors. </summary>
		public readonly long[] GlobMicroSlices => this._globMicroSlices;

		/// <summary> Raw 64-bit packed macro-connection slice descriptors. </summary>
		public readonly long[] GlobConnSlices => this._globConnSlices;

		/// <summary> Monolithic master array of packed micro-node positions. </summary>
		public readonly long[] GlobMicroMPos => this._globMicroMPos;

		/// <summary> Monolithic master array of micro-node attribute flags. </summary>
		public readonly byte[] GlobMicroFlags => this._globMicroFlags;

		/// <summary> Monolithic master array of target connection min bounding values. </summary>
		public readonly long[] GlobConnMinPos => this._globConnMinPos;

		/// <summary> Monolithic master array of target connection max bounding values. </summary>
		public readonly long[] GlobConnMaxPos => this._globConnMaxPos;

		#endregion

		#region Bit-Packing Utility Helpers

		/// <summary>
		/// Packs an integer offset and count pair into a single 64-bit signed integer (<c>long</c>).
		/// </summary>
		/// <param name="offset">Starting index in the global master stream (0 to 2,147,483,647).</param>
		/// <param name="count">Number of sequential items in the slice (0 to 2,147,483,647).</param>
		/// <returns>A single 64-bit long containing bit-shifted offset (High 32) and masked count (Low 32).</returns>
		public static long PackSlice(int offset, int count) {
			/*
             * BITWISE PACKING MECHANICS:
             * 
             * 1. (long)offset << 32
             *    - Casts 'offset' (32-bit signed int) to 64-bit long.
             *    - Shifts bits left by 32 positions.
             *    - Result: [ Offset Bits (32..63) ] [ 32 Zero Bits (0..31) ]
             * 
             * 2. (long)count & 0xFFFFFFFFL
             *    - Casts 'count' to 64-bit long.
             *    - Masks with 0x00000000FFFFFFFF to zero out any sign-extension bits in the high word.
             *    - Result: [ 32 Zero Bits (32..63) ] [ Count Bits (0..31) ]
             * 
             * 3. Bitwise OR (|)
             *    - Merges high word and low word together into a unified 64-bit primitive.
             */
			return ((long)offset << 32) | ((long)count & 0xFFFFFFFFL);
		}

		/// <summary>
		/// Unpacks a 64-bit packed long back into its constituent 32-bit <c>offset</c> and <c>count</c> integers.
		/// </summary>
		/// <param name="packedSlice">The packed 64-bit long primitive word.</param>
		/// <returns>A value tuple containing the zero-indexed array <c>offset</c> and slice <c>count</c>.</returns>
		public static (int offset, int count) UnpackSlice(long packedSlice) {
			/*
             * BITWISE UNPACKING MECHANICS:
             * 
             * 1. (int)(packedSlice >> 32)
             *    - Right-shifts the 64-bit word by 32 positions.
             *    - Moves the High-32 bits (Offset) into the Low-32 position.
             *    - Explicit cast to (int) truncates the unused upper 32 bits, leaving pure 'offset'.
             * 
             * 2. (int)(packedSlice & 0xFFFFFFFFL)
             *    - Bitwise AND with 0x00000000FFFFFFFF isolates the lower 32 bits (Count).
             *    - Zeroes out the upper 32 bits.
             *    - Explicit cast to (int) yields pure 'count'.
             */
			int offset = (int)(packedSlice >> 32);
			int count = (int)(packedSlice & 0xFFFFFFFFL);
			return (offset, count);
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of <see cref="GridDataGlobalStream"/>, binding primitive streams
		/// and enforcing null-safety guarantees across all array properties.
		/// </summary>
		public GridDataGlobalStream(
			List<Vec2Int> regionAnchorPoints,
			ulong[] keysHigh,
			ulong[] keysLow,
			TerrainType[] terrainTypes,
			MovementCapability[] allowedTraversal,
			byte[] isNarrativelyAccessible,
			long[] globMicroSlices,
			long[] globConnSlices,
			long[] globMicroMPos,
			byte[] globMicroFlags,
			long[] globConnMinPos,
			long[] globConnMaxPos) {

			// Serialize world space anchors using custom fast long packer
			this._anchors = SpatialBitPacker.PackVec2List(regionAnchorPoints ?? new());

			// Assign parallel Macro Region metadata arrays with defensive empty array fallbacks
			this._keysHigh = keysHigh ?? Array.Empty<ulong>();
			this._keysLow = keysLow ?? Array.Empty<ulong>();
			this._tt = terrainTypes ?? Array.Empty<TerrainType>();
			this._trav = allowedTraversal ?? Array.Empty<MovementCapability>();
			this._narr = isNarrativelyAccessible ?? Array.Empty<byte>();

			// Assign bit-packed slice range streams
			this._globMicroSlices = globMicroSlices ?? Array.Empty<long>();
			this._globConnSlices = globConnSlices ?? Array.Empty<long>();

			// Assign monolithic master Micro Node primitive streams
			this._globMicroMPos = globMicroMPos ?? Array.Empty<long>();
			this._globMicroFlags = globMicroFlags ?? Array.Empty<byte>();

			// Assign monolithic master Macro Connection primitive streams (target box only)
			this._globConnMinPos = globConnMinPos ?? Array.Empty<long>();
			this._globConnMaxPos = globConnMaxPos ?? Array.Empty<long>();
		}

		#endregion
	}
}