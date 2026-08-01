using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Kope.Feature.PathFindingOld.Utility {

	/// <summary>
	/// Optimization Note: Vector2Int's built-in GetHashCode() is "x.GetHashCode() ^ (y.GetHashCode() &lt;&lt; 2)".
	/// Before: for grid/tile data this collides constantly — e.g. every pair of tiles whose coordinates
	/// only differ by a small amount in x can land in the same hash bucket once the &lt;&lt;2 shift wraps
	/// values back on top of each other. Since RegionExtractionAlgorithm and GreedyRectanglePackingAlogorithm
	/// are both driven almost entirely by HashSet&lt;Vector2Int&gt;/Dictionary&lt;Vector2Int,_&gt; membership checks
	/// on exactly this kind of adjacent-tile data, that means their "O(1)" Contains/TryGetValue calls were
	/// quietly degrading into O(bucket-chain-length) walks on real tile grids.
	/// Now: this comparer mixes x and y using the large coprime multipliers from Teschner et al.'s spatial
	/// hashing scheme (73856093, 19349663) — a technique standard in game/graphics spatial-hash grids
	/// specifically because it spreads adjacent integer coordinates across the full 32-bit hash range
	/// instead of clustering them. Equality is still exact structural (x == x &amp;&amp; y == y), so passing this
	/// comparer into a HashSet/Dictionary constructor changes nothing about correctness or which keys are
	/// considered equal — it only changes which bucket a key lands in, so lookups spread out instead of
	/// chaining.
	/// </summary>
	public sealed class Vector2IntComparer : IEqualityComparer<Vector2Int> {
		public static readonly Vector2IntComparer Instance = new();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetHashCode(Vector2Int v) {
			unchecked {
				return (v.x * 73856093) ^ (v.y * 19349663);
			}
		}
	}
}