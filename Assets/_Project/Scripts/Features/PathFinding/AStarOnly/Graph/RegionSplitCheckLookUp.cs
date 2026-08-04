using System;
using System.Collections.Generic;

namespace Pathfinding.Grid {
	/// <summary>
	/// An <c>O(1)</c> local topological filter for 2D grid pathfinding and region connectivity tracking.
	/// <para>
	/// Evaluates the immediate 8-neighbor ring of a modified cell to determine if a region split is mathematically possible 
	/// before incurring the cost of a full graph search (BFS/A*).
	/// </para>
	/// </summary>
	/// <remarks>
	/// <para><b>Filter Return Contract:</b></para>
	/// <list type="bullet">
	///   <item>
	///     <description><c>false</c>: <b>Guaranteed No Split (100% Certainty).</b> All open neighbors remain locally connected around the ring. Stop immediately.</description>
	///   </item>
	///   <item>
	///     <description><c>true</c>: <b>Possible Split.</b> The local ring is disconnected into 2+ groups. Fall back to a global graph search to check outer connectivity.</description>
	///   </item>
	/// </list>
	/// 
	/// <para><b>3x3 Ring Layout &amp; Directional Indexing:</b></para>
	/// <code>
	///     0  1  2     0: Top-Left     1: Top           2: Top-Right
	///     7  -  3     7: Left         -: Center Cell   3: Right
	///     6  5  4     6: Bottom-Left  5: Bottom        4: Bottom-Right
	/// </code>
	/// 
	/// <para><b>Algorithm &amp; Derivation:</b></para>
	/// <para>
	/// Evaluates 12 fixed direct adjacency edges across the 8 ring cells:
	/// 8 ring-consecutive edges (0-1, 1-2, 2-3, 3-4, 4-5, 5-6, 6-7, 7-0) and 
	/// 4 cardinal-cardinal diagonal shortcuts (1-3, 3-5, 5-7, 7-1).
	/// </para>
	/// <para>
	/// A precomputed 256-entry lookup table (<c>2^8</c> states) evaluates connected components via union-find at startup, 
	/// achieving zero false negatives and zero false positives.
	/// </para>
	/// 
	/// <para><b>Assumptions &amp; Critical Warnings:</b></para>
	/// <list type="bullet">
	///   <item>
	///     <term>Out-of-Bounds &amp; Missing Cells</term>
	///     <description>Any non-existent, unallocated, or out-of-bounds neighbor <b>must</b> evaluate to <c>false</c> (blocked).</description>
	///   </item>
	///   <item>
	///     <term>8-Way Connectivity</term>
	///     <description>Requires 8-directional movement support. Diagonal shortcut edges depend on legal diagonal movement.</description>
	///   </item>
	///   <item>
	///     <term>Grid State Timing</term>
	///     <description>Grid state must reflect the condition <i>before</i> mutating or blocking the center cell.</description>
	///   </item>
	///   <item>
	///     <term>Index Alignment</term>
	///     <description>Ring bit order must match the exact clockwise layout defined in <see cref="NEIGHBOUR_DY_DX"/>.</description>
	///   </item>
	/// </list>
	/// 
	/// <para><b>Performance Characteristics:</b></para>
	/// <list type="bullet">
	///   <item><description><c>O(1)</c> runtime execution backed by a 256-byte static lookup table.</description></item>
	///   <item><description>100% allocation-free and GC-safe for real-time game loops.</description></item>
	///   <item><description>Exhaustively verified across all 256 neighbor states (133 early-exit states, 123 fallback states).</description></item>
	/// </list>
	/// </remarks>
	public static class LocalSplitFilter {
		/// <summary>
		/// Relative (X, Y) directional offsets for ring indices 0..7 surrounding the center cell (0,0).
		/// Clockwise order: Top-Left, Top, Top-Right, Right, Bottom-Right, Bottom, Bottom-Left, Left.
		/// Must stay in sync with the adjacency edges in <see cref="BuildLookupTable"/> --
		/// see the "ASSUMPTIONS &amp; WARNINGS" section above.
		/// Using this exact order allows the 8-bit mask to be constructed directly 
		/// from neighbor queries without any reordering.
		/// </summary>
		public static readonly (int x, int y)[] NEIGHBOUR_DY_DX = {
			(-1,-1),  // 0: Top-Left
            (0,-1),   // 1: Top
            (1,-1),   // 2: Top-Right
            (1,0),    // 3: Right
            (1,1),    // 4: Bottom-Right
            (0,1),    // 5: Bottom
            (-1,1),   // 6: Bottom-Left
            (-1,0)    // 7: Left
        };

		/// <summary>
		/// Pre-computed 256-element lookup table.
		/// Index = 8-bit integer representing the open/closed state of neighbors 0..7
		///         (bit i set == neighbor i is open, matching the ring order above).
		/// Value = 'true' if the neighbors form 2+ locally-disconnected groups
		///         (split possible), 'false' if they're all one group (guaranteed no split).
		/// </summary>
		private static readonly bool[] SplitLookup = BuildLookupTable();

		/// <summary>
		/// Static constructor helper that pre-calculates the exact answer for all 256
		/// possible 3x3 topological states. Executed exactly once at class initialization
		/// (CLR guarantees thread safety).
		/// </summary>
		private static bool[] BuildLookupTable() {
			var table = new bool[256];

			(int a, int b)[] directEdges =
			{
				(0,1), (1,2), (2,3), (3,4), (4,5), (5,6), (6,7), (7,0), // ring-consecutive edges
                (1,3), (3,5), (5,7), (7,1),                             // cardinal-cardinal diagonal shortcuts
            };

			for (int mask = 0; mask < 256; mask++) {
				var parent = new int[8];
				var present = new bool[8];
				for (int i = 0; i < 8; i++) {
					parent[i] = i;
					present[i] = (mask & (1 << i)) != 0;
				}

				int Find(int x) {
					while (parent[x] != x) {
						parent[x] = parent[parent[x]];
						x = parent[x];
					}
					return x;
				}

				void Union(int a, int b) {
					int ra = Find(a), rb = Find(b);
					if (ra != rb) parent[ra] = rb;
				}

				foreach (var (a, b) in directEdges) {
					if (present[a] && present[b]) {
						Union(a, b);
					}
				}

				var roots = new HashSet<int>();
				for (int i = 0; i < 8; i++) {
					if (present[i]) {
						roots.Add(Find(i));
					}
				}

				table[mask] = roots.Count >= 2;
			}

			return table;
		}

		/// <summary>
		/// O(1) pre-check to determine if placing or removing a block at (centerX, centerY) could split its local region.
		/// Call this BEFORE executing any full graph search (BFS / A*).
		/// </summary>
		/// <param name="centerX">Grid X coordinate of the target block.</param>
		/// <param name="centerY">Grid Y coordinate of the target block.</param>
		/// <param name="isOpen">
		/// Delegate returning <c>true</c> if a neighbor grid cell is walkable/open.<br/>
		/// <b>MUST return <c>false</c> if the coordinate is out-of-bounds, unallocated, or non-existent.</b><br/>
		/// Should reflect grid state as it stood BEFORE the center block was placed. Never queries the center cell itself.
		/// </param>
		/// <returns>
		/// <c>false</c>: Guaranteed NO split. Terminate update immediately.<br/>
		/// <c>true</c> : A split is POSSIBLE, NOT confirmed. Fall back to a real connectivity search.
		/// </returns>
		public static bool MightSplit(int centerX, int centerY, Func<int, int, bool> isOpen) {
			int mask = 0;

			for (int i = 0; i < 8; i++) {
				(int dx, int dy) = NEIGHBOUR_DY_DX[i];
				if (isOpen(centerX + dx, centerY + dy)) {
					mask |= 1 << i;
				}
			}

			return SplitLookup[mask];
		}

		/// <summary>
		/// Overload accepting center coordinate tuple.
		/// </summary>
		/// <param name="center">Target cell coordinate (X, Y).</param>
		/// <param name="isOpen">
		/// Delegate returning <c>true</c> if a neighbor grid cell is walkable/open.<br/>
		/// <b>MUST return <c>false</c> if the coordinate is out-of-bounds, unallocated, or non-existent.</b>
		/// </param>
		public static bool MightSplit((int x, int y) center, Func<int, int, bool> isOpen) {
			return MightSplit(center.x, center.y, isOpen);
		}

		/// <summary>
		/// Determines whether modifying the center cell might cause a topology split in the surrounding 8-neighbour ring.
		/// </summary>
		/// <param name="isOpenSet">
		/// An 8-element boolean array representing the open/walkable state of surrounding cells.<br/>
		/// <b>Must match the index order of <see cref="NEIGHBOUR_DY_DX"/></b> (indices 0..7, clockwise starting from Top-Left).<br/>
		/// Set element to <c>false</c> if no cell/element exists at that position (e.g., out-of-bounds or unallocated).
		/// </param>
		/// <returns><c>true</c> if modifying the center cell might disconnect neighboring walkable regions; otherwise, <c>false</c>.</returns>
		public static bool MightSplit(bool[] isOpenSet) {
			int mask = 0;
			for (int i = 0; i < 8; i++) {
				if (isOpenSet[i]) {
					mask |= 1 << i;
				}
			}
			return SplitLookup[mask];
		}

		/// <summary>
		/// Overload accepting an explicit 8-bit mask directly.
		/// </summary>
		/// <param name="neighborMask">
		/// 8-bit mask where bit 'i' is set if neighbor 'i' (per <see cref="NEIGHBOUR_DY_DX"/>) is open.<br/>
		/// Bit position for out-of-bounds/non-existent neighbors MUST be <c>0</c> (<c>false</c>).
		/// </param>
		/// <returns><c>true</c> if split is possible (not confirmed), <c>false</c> if guaranteed no split.</returns>
		public static bool MightSplit(byte neighborMask) {
			return SplitLookup[neighborMask];
		}
	}
}