using System.Collections.Generic;
using ZLinq;
using UnityEngine;
using Kope.Feature.PathFinding;

// shorthand aliases for dictionary types
using MacroDict = Kope.Core.Collections.SerializableDictionary<UnityEngine.Vector2Int, Kope.Feature.PathFinding.Tile.HHSIMacroPathFindingTile>;
using Kope.Feature.PathFinding.Utility;

/// <summary>
/// RegionExtractionAlgorithm is a stateless utility class responsible for identifying and grouping 
/// contiguous regions of macro tiles within a tilemap.<br/>
/// <br/>
/// It utilizes a depth-first search (DFS) flood-fill algorithm to explore adjacent tiles sharing 
/// an identical macro configuration, starting dynamically from an unvisited anchor tile. It collects 
/// all connected coordinates into a flat list, returning a dictionary that maps each anchor position 
/// to its corresponding region island. <br/>
/// <br/>
/// Because this class contains no internal instance state, it is fully thread-safe and can be 
/// instantiated and invoked repeatedly across multiple bake passes without retaining stale data.
/// </summary>
public class RegionExtractionAlgorithm {
	// Fixed 4-way orthogonal offsets, hoisted to a static readonly array so the DFS inner loop
	// never allocates an iterator. Before: yield-return GetNeighbors() implicitly allocated a
	// compiler-generated enumerator object per call despite its "zero-overhead" comment. Now: a
	// plain indexed for-loop over this array touches zero heap allocations per tile.
	private static readonly Vector2Int[] s_neighborOffsets = {
		new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
	};

	// =========================================================================
	// REGION EXTRACTION (FLOOD-FILL DISCOVERY)
	// =========================================================================
	// Maps each unique macro region (identified by its starting anchor position) 
	// to the complete collection of adjacent, matching tiles belonging to that zone. 
	// This isolates disconnected designer-painted clusters so they can be independently 
	// sliced into bounding boxes for macro-node generation.
	//
	// Optimization Note (anchor selection): Before, a separate "unvisited" HashSet was kept and
	// .First() was called on it repeatedly to pick the next anchor. HashSet enumeration always
	// restarts scanning from bucket zero, and since visited tiles are removed in arbitrary DFS
	// order (not front-to-back), this could degenerate into an O(n^2) scan on inputs with many
	// small regions (e.g. checkerboard-painted tiles). Now, all keys are snapshotted once into an
	// array and walked with a single forward-only index, skipping tiles already claimed by a
	// previous region. Anchor selection is now amortized O(n) across the whole extraction pass,
	// and the redundant unvisited set is dropped entirely — its membership was always derivable
	// from _macroTileDictionary + visitedTiles.
	//
	// Optimization Note (hashing): visitedTiles now uses Vector2IntComparer.Instance instead of
	// the default Vector2Int equality comparer. Before, the default GetHashCode() collided badly
	// on grid-adjacent coordinates, so HashSet lookups on tile data could silently degrade toward
	// O(n) per call. Now, the comparer's spatial-hash-style mixing spreads adjacent coordinates
	// across the full hash range, keeping Contains/Add close to true O(1) on real tile grids.
	public Dictionary<Vector2Int, List<Vector2Int>> Extract(MacroDict _macroTileDictionary) {
		var visitedTiles = new HashSet<Vector2Int>(_macroTileDictionary.Count, Vector2IntComparer.Instance);
		var allRegions = new Dictionary<Vector2Int, List<Vector2Int>>();

		// Zero-allocation snapshot of all tile coordinates via ZLinq's value-enumerable ToArray.
		var allKeys = _macroTileDictionary.Keys.AsValueEnumerable().ToArray();

		for (int i = 0; i < allKeys.Length; i++) {
			Vector2Int anchor = allKeys[i];
			if (visitedTiles.Contains(anchor)) continue;

			List<Vector2Int> singleRegionTiles = ExploreRegion(visitedTiles, _macroTileDictionary, anchor);

			if (singleRegionTiles.Count > 0) {
				allRegions[anchor] = singleRegionTiles;
			}
		}

		return allRegions;
	}

	// =========================================================================
	// LOCALIZED CONSECUTIVE TILE EXPLORATION (DFS FLOOD-FILL)
	// =========================================================================
	// Iteratively explores and groups adjacent tiles sharing an identical macro tile type.
	// Optimization Note: Only a single visitedTiles HashSet is needed now (the unvisited set 
	// was removed, see Extract() above, eliminating its per-tile Remove() calls). Neighbor 
	// offsets are read from a static array instead of an IEnumerable-yielding method, avoiding 
	// a per-tile iterator allocation in the hot path.
	private List<Vector2Int> ExploreRegion(
			HashSet<Vector2Int> visitedTiles,
			MacroDict _macroTileDictionary,
			Vector2Int anchorPos) {

		var regionTiles = new List<Vector2Int>();

		var stack = new Stack<Vector2Int>();
		stack.Push(anchorPos);

		// Cache the target reference tile once to prevent repeated dictionary lookups
		var targetTile = _macroTileDictionary[anchorPos];

		while (stack.Count > 0) {
			Vector2Int currentPos = stack.Pop();

			// Optimization: Since multiple paths can push the same unvisited neighbor onto 
			// the stack before it's processed, checking and marking it immediately upon 
			// pop prevents duplicate entries and redundant evaluations.
			if (!visitedTiles.Add(currentPos)) continue;

			regionTiles.Add(currentPos);

			for (int i = 0; i < s_neighborOffsets.Length; i++) {
				Vector2Int neighbor = currentPos + s_neighborOffsets[i];

				// Ensure neighbor hasn't been claimed yet and matches the exact same 
				// macro configuration before pushing onto the exploration stack.
				if (!visitedTiles.Contains(neighbor) &&
					_macroTileDictionary.TryGetValue(neighbor, out var neighborTile) &&
					neighborTile == targetTile) {

					stack.Push(neighbor);
				}
			}
		}

		return regionTiles;
	}
}