using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFindingOld.Node;

namespace Project.Scripts.Features.PathFindingOld.GraphManager {

	/// <summary>
	/// Worker handling room/region-level (macro) pathfinding logic. Operates on data passed by PathFindingGridManager.
	/// </summary>
	public class MacroGraphWorker {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetNode(
					Dictionary<BoundingBox, MacroGridNode> macroNodes,
					BoundingBox box,
					[MaybeNullWhen(false)] out MacroGridNode macroNode) {

			return macroNodes.TryGetValue(box, out macroNode);
		}

		public bool TryGetNodeFromPosition(
			Dictionary<BoundingBox, MacroGridNode> macroNodes,
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			Vec2Int position,
			[MaybeNullWhen(false)] out MacroGridNode macroNode) {

			macroNode = null;
			if (microNodes.TryGetValue(position, out MicroGridNode micro) && micro.ParentBBox != null) {
				return macroNodes.TryGetValue(micro.ParentBBox, out macroNode);
			}
			return false;
		}

		public void GetAllCorridorPositions(
			Dictionary<BoundingBox, MacroGridNode> macroNodes,
			List<BoundingBox> queryBoxes,
			HashSet<Vec2Int> corridorPositionsBuffer) {

			corridorPositionsBuffer.Clear();

			for (int i = 0; i < queryBoxes.Count; i++) {
				if (macroNodes.TryGetValue(queryBoxes[i], out MacroGridNode node)) {
					ReadOnlySpan<Vec2Int> positions = node.MicroGridNodePositions;
					for (int j = 0; j < positions.Length; j++) {
						corridorPositionsBuffer.Add(positions[j]);
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool GetNeighboringNodesConnectionData(
			Dictionary<BoundingBox, MacroConnectionData[]> adjacencyDict,
			BoundingBox box,
			out ReadOnlySpan<MacroConnectionData> connections) {

			connections = default;
			if (!adjacencyDict.TryGetValue(box, out var list) || list.Length == 0) return false;
			connections = list;
			return true;
		}

		public void SetNarrativeAccess(
			Dictionary<BoundingBox, MacroConnectionData[]> adjacencyDict,
			BoundingBox from,
			BoundingBox to,
			bool isAccessible,
			bool isBidirectional = true) {

			ToggleConnectionAccess(adjacencyDict, from, to, isAccessible);
			if (isBidirectional) {
				ToggleConnectionAccess(adjacencyDict, to, from, isAccessible);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ToggleConnectionAccess(
			Dictionary<BoundingBox, MacroConnectionData[]> adjacencyDict,
			BoundingBox from,
			BoundingBox to,
			bool isAccessible) {

			if (adjacencyDict.TryGetValue(from, out var connections)) {
				for (int i = 0; i < connections.Length; i++) {
					if (connections[i].ToBound == to) {
						connections[i] = connections[i].WithNarrativeAccess(isAccessible);
						break;
					}
				}
			}
		}
	}
}