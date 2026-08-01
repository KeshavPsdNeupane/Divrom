using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFindingOld.Node;

namespace Project.Scripts.Features.PathFindingOld.GraphManager {

	/// <summary>
	/// Central manager owning all pathfinding data structures and buffers.
	/// Bundles internal state and delegates operations to Micro and Macro workers.
	/// </summary>
	[Serializable]
	public class PathFindingGridManager : IPathfindingGraphManager {

		#region Data State (Owned by PathFindingGridManager)

		// --- Micro Graph State ---
		private readonly Dictionary<Vec2Int, MicroGridNode> _microNodes;
		private readonly MicroGridNode[] _neighborBuffer = new MicroGridNode[16];
		private readonly MicroGridNode[] _fetchedNeighbors = new MicroGridNode[8];

		// --- Macro Graph State ---
		private readonly Dictionary<BoundingBox, MacroGridNode> _macroNodes;
		private readonly Dictionary<BoundingBox, MacroConnectionData[]> _adjacencyDict;

		// --- Workers ---
		private readonly MicroGraphWorker _microWorker;
		private readonly MacroGraphWorker _macroWorker;

		public int MacroNodeCount => this._macroNodes.Count;
		public int MicroNodeCount => this._microNodes.Count;


		#endregion

		#region Constructors

		public PathFindingGridManager() {
			this._microNodes = new Dictionary<Vec2Int, MicroGridNode>();
			this._macroNodes = new Dictionary<BoundingBox, MacroGridNode>();
			this._adjacencyDict = new Dictionary<BoundingBox, MacroConnectionData[]>();

			this._microWorker = new MicroGraphWorker();
			this._macroWorker = new MacroGraphWorker();
		}

		public PathFindingGridManager(
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			Dictionary<BoundingBox, MacroGridNode> macroNodes,
			Dictionary<BoundingBox, List<MacroConnectionData>> adjacencyDict) {

			this._microNodes = microNodes ?? new Dictionary<Vec2Int, MicroGridNode>();
			this._macroNodes = macroNodes ?? new Dictionary<BoundingBox, MacroGridNode>();
			this._adjacencyDict = adjacencyDict?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
								  ?? new Dictionary<BoundingBox, MacroConnectionData[]>();

			this._microWorker = new MicroGraphWorker();
			this._macroWorker = new MacroGraphWorker();
		}

		#endregion

		#region Micro Node API (Bundles Data -> Passes to Worker)

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetMicroNode(Vec2Int position, [MaybeNullWhen(false)] out MicroGridNode microNode) {
			return this._microWorker.TryGetNode(this._microNodes, position, out microNode);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodesWithRules(
			Vec2Int position) {

			return this._microWorker.GetWalkableNeighboringNodesWithRules(
				this._microNodes,
				this._neighborBuffer,
				this._fetchedNeighbors,
				position);
		}

		#endregion

		#region Macro Node API (Bundles Data -> Passes to Worker)
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetMacroNode(BoundingBox box, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			return this._macroWorker.TryGetNode(this._macroNodes, box, out macroNode);
		}

		public bool TryGetMacroNodeFromPosition(Vec2Int position, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			return this._macroWorker.TryGetNodeFromPosition(this._macroNodes, this._microNodes, position, out macroNode);
		}

		public void GetAllCorridorPositions(List<BoundingBox> macroNodes, HashSet<Vec2Int> corridorPositionsBuffer) {
			this._macroWorker.GetAllCorridorPositions(this._macroNodes, macroNodes, corridorPositionsBuffer);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool GetNeighboringMacroNodesConnectionData(BoundingBox box, out ReadOnlySpan<MacroConnectionData> connections) {
			return this._macroWorker.GetNeighboringNodesConnectionData(this._adjacencyDict, box, out connections);
		}

		public void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true) {
			this._macroWorker.SetNarrativeAccess(this._adjacencyDict, from, to, isAccessible, isBidirectional);
		}

		#endregion

		#region Debug & Test Tools

#if UNITY_EDITOR
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)> GiveRandomTestPoints(int randomPathCount, int seed = 0) {
			return this._microWorker.GiveRandomTestPoints(this._microNodes, randomPathCount, seed);
		}
#endif

		#endregion
	}
}