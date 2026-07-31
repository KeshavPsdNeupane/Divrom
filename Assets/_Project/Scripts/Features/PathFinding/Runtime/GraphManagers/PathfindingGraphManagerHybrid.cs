using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Internal layout tracking contiguous array slice indices for Macro nodes.
	/// </summary>
	internal readonly struct MacroStructure {
		public readonly int ConnectionStartIndex;
		public readonly int ConnectionCount;
		public readonly int MicroStartIndex;
		public readonly int MicroCount;

		public MacroStructure(int connStart, int connCount, int microStart, int microCount) {
			ConnectionStartIndex = connStart;
			ConnectionCount = connCount;
			MicroStartIndex = microStart;
			MicroCount = microCount;
		}
	}

	[Serializable]
	public class PathfindingGraphManagerHybrid : IPathfindingGraphManager {

		#region Reusable Buffers

		private static readonly Vec2Int[] CARDINAL_DIRECTIONS = new[] {
			Vec2Int.Up, Vec2Int.Down, Vec2Int.Left, Vec2Int.Right
		};

		private readonly MicroGridNode[] _neighborBuffer = new MicroGridNode[16];
		private readonly List<MacroConnectionData> _connectionListBuffer = new(16);
		private readonly HashSet<Vec2Int> _corridorPositions = new();

		#endregion

		#region Pure Flat Memory Storage

		// Contiguous flat arrays - primary source of truth
		private readonly MacroGridNode[] _macroNodes;
		private readonly MicroGridNode[] _microNodes;
		private readonly MacroStructure[] _macroStructures;
		private readonly MacroConnectionData[] _macroConnectionData;

		// Fast O(1) Position/Box -> Array Index Translation
		private readonly Dictionary<BoundingBox, int> _macroBoxToIndex;
		private readonly Dictionary<Vec2Int, int> _microPositionToIndex;

		public int MacroNodeCount => _macroNodes.Length;
		public int MicroNodeCount => _microNodes.Length;

		#endregion

		#region Constructor (Flattening & Index Baking)

		public PathfindingGraphManagerHybrid(
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			Dictionary<BoundingBox, MacroGridNode> macroNodes,
			Dictionary<BoundingBox, List<MacroConnectionData>> adjacencyDict) {

			int totalMacro = macroNodes.Count;
			int totalMicro = microNodes.Count;
			int totalConnections = 0;

			foreach (var kvp in adjacencyDict) {
				totalConnections += kvp.Value.Count;
			}

			_macroNodes = new MacroGridNode[totalMacro];
			_microNodes = new MicroGridNode[totalMicro];
			_macroStructures = new MacroStructure[totalMacro];
			_macroConnectionData = new MacroConnectionData[totalConnections];

			_macroBoxToIndex = new Dictionary<BoundingBox, int>(totalMacro);
			_microPositionToIndex = new Dictionary<Vec2Int, int>(totalMicro);

			int currentMacroIdx = 0;
			int currentMicroIdx = 0;
			int currentConnIdx = 0;

			foreach (var kvp in macroNodes) {
				var box = kvp.Key;
				var macroNode = kvp.Value;

				_macroBoxToIndex[box] = currentMacroIdx;
				_macroNodes[currentMacroIdx] = macroNode;

				// 1. Bake Connections into contiguous array slice
				int connStart = currentConnIdx;
				if (adjacencyDict.TryGetValue(box, out var connections)) {
					for (int i = 0; i < connections.Count; i++) {
						_macroConnectionData[currentConnIdx++] = connections[i];
					}
				}
				int connCount = currentConnIdx - connStart;

				// 2. Bake Micro Nodes into contiguous array slice
				int microStart = currentMicroIdx;
				ReadOnlySpan<Vec2Int> microPositions = macroNode.MicroGridNodePositions;

				for (int i = 0; i < microPositions.Length; i++) {
					Vec2Int microPos = microPositions[i];
					if (microNodes.TryGetValue(microPos, out var microNode)) {
						_microPositionToIndex[microPos] = currentMicroIdx;
						_microNodes[currentMicroIdx] = microNode;
						currentMicroIdx++;
					}
				}
				int microCount = currentMicroIdx - microStart;

				// 3. Store Macro Slice Metadata
				_macroStructures[currentMacroIdx] = new MacroStructure(
					connStart, connCount,
					microStart, microCount
				);

				currentMacroIdx++;
			}
		}

		#endregion

		#region Micro Node Operations

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetMicroNode(Vec2Int position, [MaybeNullWhen(false)] out MicroGridNode microNode) {
			if (_microPositionToIndex.TryGetValue(position, out int idx)) {
				microNode = _microNodes[idx];
				return true;
			}
			microNode = default;
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodes(Vec2Int position) {
			int count = 0;
			for (int i = 0; i < CARDINAL_DIRECTIONS.Length; i++) {
				Vec2Int neighborPos = position + CARDINAL_DIRECTIONS[i];

				if (_microPositionToIndex.TryGetValue(neighborPos, out int microIdx)) {
					MicroGridNode neighborNode = _microNodes[microIdx];
					if (!neighborNode.IsStaticObstacle) {
						this._neighborBuffer[count++] = neighborNode;
					}
				}
			}
			return this._neighborBuffer.AsSpan(0, count);
		}

		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodesWithRules(
			Vec2Int position,
			Vec2Int[] neighborOffsets,
			IReadOnlyDictionary<Vec2Int, (Vec2Int req1, Vec2Int req2)> neighborRules = null,
			HashSet<Vec2Int> visited = null) {

			int count = 0;
			for (int i = 0; i < neighborOffsets.Length; i++) {
				Vec2Int offset = neighborOffsets[i];
				Vec2Int neighborPos = position + offset;

				if (visited != null && visited.Contains(neighborPos)) continue;
				if (!TryGetMicroNode(neighborPos, out MicroGridNode neighborNode) || neighborNode.IsStaticObstacle) continue;

				if (neighborRules != null && neighborRules.TryGetValue(offset, out var requiredOffsets)) {
					Vec2Int reqPos1 = position + requiredOffsets.req1;
					Vec2Int reqPos2 = position + requiredOffsets.req2;

					if (!TryGetMicroNode(reqPos1, out var reqNode1) || reqNode1.IsStaticObstacle ||
						!TryGetMicroNode(reqPos2, out var reqNode2) || reqNode2.IsStaticObstacle) {
						continue;
					}
				}

				this._neighborBuffer[count++] = neighborNode;
			}
			return this._neighborBuffer.AsSpan(0, count);
		}

		#endregion

		#region Macro Node Operations

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetMacroNode(BoundingBox box, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			if (_macroBoxToIndex.TryGetValue(box, out int idx)) {
				macroNode = _macroNodes[idx];
				return true;
			}
			macroNode = null;
			return false;
		}

		public bool TryGetMacroNodeFromPosition(Vec2Int position, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			macroNode = null;
			if (_microPositionToIndex.TryGetValue(position, out int microIdx)) {
				var microNode = _microNodes[microIdx];
				if (microNode.ParentBBox != null && _macroBoxToIndex.TryGetValue(microNode.ParentBBox, out int macroIdx)) {
					macroNode = _macroNodes[macroIdx];
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Aggregates corridor positions by reading directly from contiguous flat micro array slices.
		/// </summary>
		public HashSet<Vec2Int> GetAllCorridorPositions(List<BoundingBox> macroNodes) {
			this._corridorPositions.Clear();

			for (int i = 0; i < macroNodes.Count; i++) {
				if (_macroBoxToIndex.TryGetValue(macroNodes[i], out int macroIdx)) {
					ref readonly var macro = ref _macroStructures[macroIdx];
					int end = macro.MicroStartIndex + macro.MicroCount;

					for (int j = macro.MicroStartIndex; j < end; j++) {
						this._corridorPositions.Add(_microNodes[j].Position);
					}
				}
			}

			return this._corridorPositions;
		}

		#endregion

		#region Macro Connections & Narrative Access

		public bool GetNeighboringMacroNodesConnectionData(
			BoundingBox box, MovementCapability capability,
			out IReadOnlyList<MacroConnectionData> connections) {

			connections = null;
			if (!_macroBoxToIndex.TryGetValue(box, out int macroIdx)) return false;

			ref readonly var macro = ref _macroStructures[macroIdx];
			this._connectionListBuffer.Clear();

			int end = macro.ConnectionStartIndex + macro.ConnectionCount;
			for (int i = macro.ConnectionStartIndex; i < end; i++) {
				ref readonly var conn = ref _macroConnectionData[i];
				if (conn.IsTraversable(capability)) {
					this._connectionListBuffer.Add(conn);
				}
			}

			connections = this._connectionListBuffer;
			return true;
		}

		public void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true) {
			ToggleConnectionAccess(from, to, isAccessible);
			if (isBidirectional) {
				ToggleConnectionAccess(to, from, isAccessible);
			}
		}

		private void ToggleConnectionAccess(BoundingBox from, BoundingBox to, bool isAccessible) {
			if (_macroBoxToIndex.TryGetValue(from, out int macroIdx)) {
				ref readonly var macro = ref _macroStructures[macroIdx];
				int end = macro.ConnectionStartIndex + macro.ConnectionCount;

				for (int i = macro.ConnectionStartIndex; i < end; i++) {
					if (_macroConnectionData[i].ToBound == to) {
						_macroConnectionData[i] = _macroConnectionData[i].WithNarrativeAccess(isAccessible);
						break;
					}
				}
			}
		}

		#endregion
#if UNITY_EDITOR
		/// <summary>
		/// Generates random micro coordinate pairs for pathfinding unit tests and benchmark evaluation.
		/// Directly samples the flat _microNodes array with zero heap allocations.
		/// </summary>
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)> GiveRandomTestPoints(int randomPathCount, int seed = 0) {
			if (this._microNodes == null || this._microNodes.Length < 2) yield break;

			int count = this._microNodes.Length;
			UnityEngine.Random.InitState(seed);

			for (int i = 0; i < randomPathCount; i++) {
				int startIdx = UnityEngine.Random.Range(0, count);
				int endIdx;
				do {
					endIdx = UnityEngine.Random.Range(0, count);
				} while (endIdx == startIdx && count > 1);

				yield return (this._microNodes[startIdx].Position, this._microNodes[endIdx].Position);
			}
		}
#endif
	}
}