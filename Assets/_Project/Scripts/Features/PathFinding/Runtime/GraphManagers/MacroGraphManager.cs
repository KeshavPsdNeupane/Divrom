using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;
using ZLinq;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Represents directed edge connection data between two macro grid nodes.
	/// </summary>
	/// <remarks>
	/// An immutable value type storing traversal rules for graph edges. Combines physical movement capabilities 
	/// (e.g., Walking, Flying via bitmasks) with dynamic story/gameplay availability flags.
	/// </remarks>
	[Serializable]
	public struct MacroConnectionData : IEquatable<MacroConnectionData> {
		[SerializeField, ReadOnly] private BoundingBox _toBound;
		[SerializeField, ReadOnly] private MovementCapability _allowedTraversal;
		[SerializeField, ReadOnly] private bool _isNarrativelyAccessible;
		/// <summary>
		/// Gets the spatial bounding box of the target macro grid node this connection leads to.
		/// </summary>
		public readonly BoundingBox ToBound => this._toBound;

		/// <summary>
		/// Gets the bitmask of movement capabilities required to traverse this connection.
		/// </summary>
		public readonly MovementCapability AllowedTraversal => this._allowedTraversal;

		/// <summary>
		/// Gets a value indicating whether current narrative or story flags permit traversal across this connection.
		/// </summary>
		public readonly bool IsNarrativelyAccessible => this._isNarrativelyAccessible;

		/// <summary>
		/// Initializes a new instance of the <see cref="MacroConnectionData"/> struct.
		/// </summary>
		/// <param name="targetBounds">The spatial bounding box of the destination node.</param>
		/// <param name="allowedTraversal">The movement capabilities allowed across this edge.</param>
		/// <param name="isNarrativelyAccessible">Whether story context permits traversal across this edge.</param>
		public MacroConnectionData(
			BoundingBox targetBounds,
			MovementCapability allowedTraversal,
			bool isNarrativelyAccessible = true) {
			this._toBound = targetBounds;
			this._allowedTraversal = allowedTraversal;
			this._isNarrativelyAccessible = isNarrativelyAccessible;
		}

		/// <summary>
		/// Creates a <see cref="MacroConnectionData"/> instance by evaluating and combining movement capabilities 
		/// and narrative accessibility between source and destination nodes.
		/// </summary>
		/// <param name="to">The spatial <see cref="BoundingBox"/> of the destination macro node.</param>
		/// <param name="toCapability">The movement capabilities allowed by the target node.</param>
		/// <param name="fromCapability">The movement capabilities of the source node.</param>
		/// <param name="toIsNarrativelyAccessible">Whether the target node is accessible within the current narrative context.</param>
		/// <param name="fromIsNarrativelyAccessible">Whether the source node is accessible within the current narrative context.</param>
		/// <returns>A new <see cref="MacroConnectionData"/> instance representing the resolved traversal state.</returns>
		/// <remarks>
		/// Combines capabilities using a bitwise OR (<c>toCapability | fromCapability</c>) and requires both nodes 
		/// to be narratively accessible (<c>toIsNarrativelyAccessible &amp;&amp; fromIsNarrativelyAccessible</c>).
		/// Intended for standalone or external connection creation when building edges outside of <see cref="MacroGraphManager"/>.
		/// </remarks>
		public static MacroConnectionData CreateConnection(
			BoundingBox to, MovementCapability toCapability,
			MovementCapability fromCapability, bool toIsNarrativelyAccessible,
			bool fromIsNarrativelyAccessible) {

			MovementCapability combinedCapability = toCapability | fromCapability;
			bool combinedNarrativeAccess = toIsNarrativelyAccessible && fromIsNarrativelyAccessible;
			return new MacroConnectionData(to, combinedCapability, combinedNarrativeAccess);
		}

		/// <summary>
		/// Creates a copy of this connection with an updated narrative accessibility flag.
		/// </summary>
		/// <param name="isNarrativelyAccessible">The updated narrative accessibility state.</param>
		/// <returns>A new <see cref="MacroConnectionData"/> instance with updated narrative access.</returns>
		public readonly MacroConnectionData WithNarrativeAccess(bool isNarrativelyAccessible) {
			return new MacroConnectionData(this.ToBound, this.AllowedTraversal, isNarrativelyAccessible);
		}

		/// <summary>
		/// Determines whether an entity with the specified movement capabilities can traverse this connection.
		/// </summary>
		/// <param name="capability">The movement capability bitmask of the traversing entity.</param>
		/// <returns>
		/// <c>true</c> if <see cref="IsNarrativelyAccessible"/> is <c>true</c> and <paramref name="capability"/> 
		/// shares at least one flag with <c>AllowedTraversal</c>; otherwise, <c>false</c>.
		/// </returns>
		public readonly bool IsTraversable(MovementCapability capability) {
			return IsNarrativelyAccessible && (AllowedTraversal & capability) != MovementCapability.None;
		}

		public readonly bool Equals(MacroConnectionData other) {
			return ToBound == other.ToBound &&
				   AllowedTraversal == other.AllowedTraversal &&
				   IsNarrativelyAccessible == other.IsNarrativelyAccessible;
		}

		public override readonly bool Equals(object obj) {
			return obj is MacroConnectionData other && this.Equals(other);
		}

		public override readonly int GetHashCode() {
			return HashCode.Combine(ToBound, AllowedTraversal, IsNarrativelyAccessible);
		}

		public static bool operator ==(MacroConnectionData left, MacroConnectionData right) => left.Equals(right);
		public static bool operator !=(MacroConnectionData left, MacroConnectionData right) => !left.Equals(right);

		public override readonly string ToString() =>
			$"MacroConnectionData(To: {ToBound}, AllowedTraversal: {AllowedTraversal}, IsNarrativelyAccessible: {IsNarrativelyAccessible})";
	}


	/// <summary>
	/// Manages high-level macro grid nodes and evaluates graph connectivity for Tier-1 pathfinding.
	/// </summary>
	/// <remarks>
	/// Maintains spatial node lookups and graph adjacency lists, providing zero-allocation capability evaluation 
	/// and runtime story/quest access toggling for region transitions.
	/// </remarks>
	[Serializable]
	public class MacroGraphManager {
		private readonly Dictionary<BoundingBox, MacroGridNode> _macroNodes;
		private readonly Dictionary<BoundingBox, List<MacroConnectionData>> _adjacencyDict;

		public int MacroNodeCount => this._macroNodes.Count;

		/// <summary>
		/// Initializes an empty instance of the <see cref="MacroGraphManager"/> class.
		/// </summary>
		public MacroGraphManager() {
			this._macroNodes = new();
			this._adjacencyDict = new();
		}

		/// <summary>
		/// Initializes a new instance of <see cref="MacroGraphManager"/> using pre-populated node and adjacency dictionaries.
		/// </summary>
		/// <param name="macroNodes">Serialized map of bounding boxes to macro grid nodes.</param>
		/// <param name="adjacencyDict">Serialized map of bounding boxes to outgoing connection lists.</param>
		public MacroGraphManager(
			Dictionary<BoundingBox, MacroGridNode> macroNodes,
			Dictionary<BoundingBox, List<MacroConnectionData>> adjacencyDict) {
			this._macroNodes = macroNodes;
			this._adjacencyDict = adjacencyDict;
			// Debug.Log($"MacroGraphManager initialized with {macroNodes.Count} nodes and {adjacencyDict.Count} adjacency entries.");
			// this._macroNodes.PrintFirstNEntries(5, "Macro Nodes");
			// this._adjacencyDict.PrintFirstNEntries(5, "Adjacency List");
		}

		/// <summary>
		/// Registers a macro node in the lookup dictionary and initializes its entry in the adjacency list.
		/// </summary>
		/// <remarks>
		/// This method only handles basic registration. If the graph is not empty, 
		/// the caller is responsible for evaluating spatial adjacency and establishing 
		/// bidirectional connections between this node and its neighbors.
		/// </remarks>
		/// <param name="node">The macro node instance to register.</param>
		public void RegisterNode(MacroGridNode node) {
			this._macroNodes[node.Bound] = node;

			// Initialize an empty adjacency list for the node if it doesn't already exist.
			// Note: If the graph is not empty, the caller must handle discovering neighbors 
			// and wiring up bidirectional edge connections externally.
			if (!this._adjacencyDict.ContainsKey(node.Bound)) {
				this._adjacencyDict[node.Bound] = new List<MacroConnectionData>();
			}
		}

		/// <summary>
		/// Unconditionally removes a macro grid node and cleans up all associated graph adjacency connections.
		/// </summary>
		/// <remarks>
		/// Performs a direct, standalone removal. Unlike <see cref="TryRemoveNode"/>, this method does not 
		/// expose constituent micro-tile positions via an <c>out</c> parameter, as it assumes any cascading 
		/// cleanup is either handled externally or unnecessary for the caller's context. If the macro node 
		/// is not found, it emits a warning log and exits.
		/// </remarks>
		/// <param name="bounds">The bounding box defining the macro node to remove.</param>
		public void RemoveNode(BoundingBox bounds) {
			// Guard clause: Exit early if the node doesn't exist in the registry
			if (!this._macroNodes.Remove(bounds)) {
				Debug.LogWarning($"Attempted to remove MacroGridNode with bounds {bounds}, but it does not exist.");
				return;
			}

			// If the node exists, strip out any bidirectional edge references from neighboring nodes
			if (this._adjacencyDict.TryGetValue(bounds, out var connections)) {
				foreach (var connection in connections) {
					if (this._adjacencyDict.TryGetValue(connection.ToBound, out var reverseConnections)) {
						reverseConnections.RemoveAll(c => c.ToBound == bounds);
					}
				}

				// Clear and drop the node's own adjacency entry from the dictionary
				connections.Clear();
				this._adjacencyDict.Remove(bounds);
			}
		}

		/// <summary>
		/// Attempts to conditionally remove a macro node by its bounding box, outputting its constituent 
		/// micro tile positions if successful to allow cascading cleanup.
		/// </summary>
		/// <remarks>
		/// Follows the standard .NET <c>Try*</c> pattern. If the macro node exists, it performs the structural 
		/// removal and returns <c>true</c>, handing back the micro-grid coordinates via the <paramref name="microTilesPositions"/> 
		/// <c>out</c> parameter so the caller can use them to cascade the deletion down to the micro graph. 
		/// If absent, it safely returns <c>false</c> without logging warnings.
		/// </remarks>
		/// <param name="bounds">The bounding box of the macro node to remove.</param>
		/// <param name="microTilesPositions">
		/// When this method returns, outputs the read-only list of constituent micro-tile positions if found; 
		/// otherwise, <c>null</c>.
		/// </param>
		/// <returns><c>true</c> if the macro node existed and was successfully removed; otherwise, <c>false</c>.</returns>
		public bool TryRemoveNode(BoundingBox bounds, [MaybeNullWhen(false)] out IReadOnlyList<Vec2Int> microTilesPositions) {
			microTilesPositions = null;

			if (this._macroNodes.TryGetValue(bounds, out MacroGridNode removedNode)) {
				microTilesPositions = removedNode.MicroGridNodePositions;
				// Invoke the core structural cleanup logic to handle graph rewiring
				RemoveNode(bounds);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Attempts to retrieve a registered macro grid node by its bounding box key.
		/// </summary>
		/// <param name="bounds">The spatial bounding box key to look up.</param>
		/// <param name="node">When successful, receives the associated <see cref="MacroGridNode"/> instance.</param>
		/// <returns><c>true</c> if the node was found; otherwise, <c>false</c>.</returns>
		public bool TryGetNode(BoundingBox bounds, out MacroGridNode node) {
			return this._macroNodes.TryGetValue(bounds, out node);
		}



		private readonly HashSet<Vec2Int> corridorPostion = new();
		public HashSet<Vec2Int> GetAllCorridorPositions(List<BoundingBox> macroNodes) {
			corridorPostion.Clear();
			foreach (var macroNode in macroNodes) {
				if (!this._macroNodes.TryGetValue(macroNode, out MacroGridNode node)) {

					Debug.LogWarning($"Macro node with bounds {macroNode} not found in the graph. Skipping.");
					continue;
				}
				//Debug.Log($"Macro node {macroNode} has {node.MicroGridNodePositions.Count} micro positions.");
				corridorPostion.UnionWith(node.MicroGridNodePositions);
			}
			return corridorPostion;
		}




		/// <summary>
		/// Adds a directed or bidirectional edge between two macro grid nodes, calculating 
		/// combined capabilities and narrative access.
		/// </summary>
		/// <param name="from">Origin node bounding box key.</param>
		/// <param name="to">Destination node bounding box key.</param>
		/// <param name="Tocapability">Movement capabilities allowed by the destination node.</param>
		/// <param name="fromCapability">Movement capabilities allowed by the origin node.</param>
		/// <param name="toIsNarrativelyAccessible">Story access state for the destination node.</param>
		/// <param name="fromIsNarrativelyAccessible">Story access state for the origin node.</param>
		/// <param name="isBidirectional">If <c>true</c>, creates connections in both directions.</param>
		public void AddConnection(
			BoundingBox from,
			BoundingBox to,
			MovementCapability Tocapability,
			MovementCapability fromCapability,
			bool toIsNarrativelyAccessible = true,
			bool fromIsNarrativelyAccessible = true,
			bool isBidirectional = true) {

			MovementCapability capability = Tocapability | fromCapability;
			bool isNarrativelyAccessible = toIsNarrativelyAccessible && fromIsNarrativelyAccessible;

			AddDirectedConnection(from, to, capability, isNarrativelyAccessible);
			if (isBidirectional) {
				AddDirectedConnection(to, from, capability, isNarrativelyAccessible);
			}
		}

		/// <summary>
		/// Adds a single directed connection from an origin node to a target node without creating duplicates.
		/// </summary>
		/// <param name="from">Origin node bounding box key.</param>
		/// <param name="to">Destination node bounding box key.</param>
		/// <param name="capability">Combined movement capability mask for the connection.</param>
		/// <param name="isNarrativelyAccessible">Combined narrative access state for the connection.</param>
		private void AddDirectedConnection(
			BoundingBox from,
			BoundingBox to,
			MovementCapability capability,
			bool isNarrativelyAccessible) {

			if (!this._adjacencyDict.TryGetValue(from, out var connections)) {
				connections = new List<MacroConnectionData>();
				this._adjacencyDict[from] = connections;
			}

			if (!connections.Exists(c => c.ToBound == to)) {
				connections.Add(new MacroConnectionData(to, capability, isNarrativelyAccessible));
			}
		}

		/// <summary>
		/// Retrieves all outgoing connections from a macro node that satisfy the entity's movement capabilities and narrative accessibility.
		/// </summary>
		/// <param name="from">Origin macro node bounding box key.</param>
		/// <param name="capability">Movement capabilities of the traversing entity.</param>
		/// <returns>A collection of traversable <see cref="MacroConnectionData"/> instances.</returns>
		public bool GetTraversableConnections(
			BoundingBox from, MovementCapability capability,
			out IEnumerable<MacroConnectionData> connections) {
			connections = null;
			if (!this._adjacencyDict.TryGetValue(from, out var list)) {
				return false;
			}
			connections = list;
			// Leverages ZLinq value-enumerable filtering before materializing the list,
			// why use ZLinq? Because it avoids unnecessary allocations and improves 
			// performance in high-frequency pathfinding scenarios.
			// as this method is called frequently during pathfinding, we want to 
			// avoid unnecessary allocations.
			connections = connections.AsValueEnumerable().Where(c => c.IsTraversable(capability)).ToList();
			return true;
		}

		/// <summary>
		/// Evaluates whether a direct, traversable edge exists from a source macro node to a target macro node.
		/// </summary>
		/// <param name="from">Origin macro node bounding box key.</param>
		/// <param name="to">Target macro node bounding box key.</param>
		/// <param name="capability">Movement capabilities of the traversing entity.</param>
		/// <returns><c>true</c> if a matching traversable connection exists; otherwise, <c>false</c>.</returns>
		public bool CanTraverse(BoundingBox from, BoundingBox to, MovementCapability capability) {
			if (this._adjacencyDict.TryGetValue(from, out var connections)) {
				return connections.AsValueEnumerable().Any(c => c.ToBound == to && c.IsTraversable(capability));
			}
			return false;
		}

		/// <summary>
		/// Updates the narrative accessibility status for connections between two macro nodes.
		/// </summary>
		/// <param name="from">Origin node bounding box key.</param>
		/// <param name="to">Target node bounding box key.</param>
		/// <param name="isAccessible">The new narrative accessibility status.</param>
		/// <param name="isBidirectional">If <c>true</c>, updates narrative access in both directions.</param>
		public void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true) {
			ToggleConnectionAccess(from, to, isAccessible);
			if (isBidirectional) {
				ToggleConnectionAccess(to, from, isAccessible);
			}
		}

		/// <summary>
		/// Updates a specific connection's narrative access flag in-place within the adjacency list.
		/// </summary>
		/// <param name="from">Origin node bounding box key.</param>
		/// <param name="to">Target node bounding box key.</param>
		/// <param name="isAccessible">The new narrative accessibility status.</param>
		private void ToggleConnectionAccess(BoundingBox from, BoundingBox to, bool isAccessible) {
			if (this._adjacencyDict.TryGetValue(from, out var connections)) {
				int index = connections.FindIndex(c => c.ToBound == to);
				if (index >= 0) {
					connections[index] = connections[index].WithNarrativeAccess(isAccessible);
				}
			}
		}
	}
}