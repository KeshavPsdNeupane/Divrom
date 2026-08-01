using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Kope.Feature.PathFinding.Node;

namespace Project.Scripts.Features.PathFindingOld.GraphManager {

	public enum PathFindingManagerType {
		PathfindingGraphManager = 0,
		/// <summary>
		/// Central manager orchestrating two-tier hierarchical graph operations.
		/// Flattened to allow cross-boundary optimizations, zero-allocation lookups, and shared buffers.
		/// This acts as the single source of truth for both Micro (tile-level) and Macro (region/room-level) pathing.
		/// </summary>
		PathfindingGraphManagerHybrid = 1
	}


	public interface IPathfindingGraphManager {

		int MacroNodeCount { get; }
		int MicroNodeCount { get; }

		#region Micro Node Operations

		/// <summary>
		/// Fast O(1) lookup for a Micro node by position.
		/// </summary>
		bool TryGetMicroNode(Vec2Int position, [MaybeNullWhen(false)] out MicroGridNode microNode);

		/// <summary>
		/// Zero-allocation retrieval of walkable neighbors adhering to diagonal/geometric rules.
		/// </summary>
		ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodesWithRules(
			Vec2Int position);


		#endregion

		#region Macro Node Operations

		/// <summary>
		/// Fast O(1) lookup for a Macro region node by its bounding box.
		/// </summary>
		bool TryGetMacroNode(BoundingBox box, [MaybeNullWhen(false)] out MacroGridNode macroNode);

		/// <summary>
		/// Reverse lookup to find which Macro region contains a specific Micro coordinate.
		/// </summary>
		bool TryGetMacroNodeFromPosition(Vec2Int position, [MaybeNullWhen(false)] out MacroGridNode macroNode);

		/// <summary>
		/// Aggregates corridor Micro positions for a set of Macro bounding boxes.
		/// </summary>
		void GetAllCorridorPositions(List<BoundingBox> macroNodes, HashSet<Vec2Int> corridorPositionsBuffer);

		#endregion

		#region Macro Adjacency & Traversal Operations

		/// <summary>
		/// Retrieves outbound macro connection data matching movement capabilities.
		/// </summary>
		bool GetNeighboringMacroNodesConnectionData(
			BoundingBox box,
			out ReadOnlySpan<MacroConnectionData> connections);

		/// <summary>
		/// Dynamically updates narrative accessibility (e.g., doors/switches) between Macro regions.
		/// </summary>
		void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true);

		#endregion



#if UNITY_EDITOR
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)>
		GiveRandomTestPoints(int randomPathCount, int seed = 0);
#endif
	}
}