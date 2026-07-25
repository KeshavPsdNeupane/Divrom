using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding;
using Unity.VisualScripting;
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
			MovementCapability fromCapability, bool toIsNarrativelyAccessible = true,
			bool fromIsNarrativelyAccessible = true) {

			MovementCapability combinedCapability = toCapability | fromCapability;
			bool combinedNarrativeAccess = toIsNarrativelyAccessible && fromIsNarrativelyAccessible;
			return new MacroConnectionData(to, combinedCapability, combinedNarrativeAccess);
		}

		/// <summary>
		/// Creates a copy of this connection with an updated narrative accessibility flag.
		/// </summary>
		/// <param name="isNarrativelyAccessible">The updated narrative accessibility state.</param>
		/// <returns>A new <see cref="MacroConnectionData"/> instance with updated narrative access.</returns>
		public MacroConnectionData WithNarrativeAccess(bool isNarrativelyAccessible) {
			return new MacroConnectionData(this.ToBound, this.AllowedTraversal, isNarrativelyAccessible);
		}

		/// <summary>
		/// Determines whether an entity with specified movement capabilities can traverse this connection.
		/// </summary>
		/// <param name="capability">The movement capability flags of the traversing entity.</param>
		/// <returns><c>true</c> if narrative access is granted and the entity satisfies required movement flags; otherwise, <c>false</c>.</returns>
		public bool IsTraversable(MovementCapability capability) {
			return IsNarrativelyAccessible && (AllowedTraversal & capability) == capability;
		}

		public bool Equals(MacroConnectionData other) {
			return ToBound == other.ToBound &&
				   AllowedTraversal == other.AllowedTraversal &&
				   IsNarrativelyAccessible == other.IsNarrativelyAccessible;
		}

		public override bool Equals(object obj) {
			return obj is MacroConnectionData other && this.Equals(other);
		}

		public override int GetHashCode() {
			return HashCode.Combine(ToBound, AllowedTraversal, IsNarrativelyAccessible);
		}

		public static bool operator ==(MacroConnectionData left, MacroConnectionData right) => left.Equals(right);
		public static bool operator !=(MacroConnectionData left, MacroConnectionData right) => !left.Equals(right);

		public override string ToString() =>
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

		/// <summary>
		/// Cached empty list returned when querying connections for unregistered nodes to avoid GC allocations.
		/// </summary>
		private static readonly List<MacroConnectionData> EmptyConnections = new();

		private readonly SerializableDictionary<BoundingBox, MacroGridNode> _macroNodes;
		private readonly SerializableDictionary<BoundingBox, List<MacroConnectionData>> _adjacencyList;

		/// <summary>
		/// Initializes an empty instance of the <see cref="MacroGraphManager"/> class.
		/// </summary>
		public MacroGraphManager() {
			this._macroNodes = new();
			this._adjacencyList = new();
		}

		/// <summary>
		/// Initializes a new instance of <see cref="MacroGraphManager"/> using pre-populated node and adjacency dictionaries.
		/// </summary>
		/// <param name="macroNodes">Serialized map of bounding boxes to macro grid nodes.</param>
		/// <param name="adjacencyList">Serialized map of bounding boxes to outgoing connection lists.</param>
		public MacroGraphManager(
			SerializableDictionary<BoundingBox, MacroGridNode> macroNodes,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> adjacencyList) {
			this._macroNodes = macroNodes;
			this._adjacencyList = adjacencyList;
		}

		/// <summary>
		/// Registers a macro node in the lookup dictionary and initializes its entry in the adjacency list.
		/// </summary>
		/// <param name="node">The macro node instance to register.</param>
		public void RegisterNode(MacroGridNode node) {
			this._macroNodes[node.Bounds] = node;
			if (!this._adjacencyList.ContainsKey(node.Bounds)) {
				this._adjacencyList[node.Bounds] = new List<MacroConnectionData>();
			}
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

			if (!this._adjacencyList.TryGetValue(from, out var connections)) {
				connections = new List<MacroConnectionData>();
				this._adjacencyList[from] = connections;
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
		public IEnumerable<MacroConnectionData> GetTraversableConnections(BoundingBox from, MovementCapability capability) {
			var connections = this._adjacencyList.TryGetValue(from, out var list)
				? list
				: EmptyConnections;

			// Leverages ZLinq value-enumerable filtering before materializing the list
			return connections.AsValueEnumerable().Where(c => c.IsTraversable(capability)).ToList();
		}

		/// <summary>
		/// Evaluates whether a direct, traversable edge exists from a source macro node to a target macro node.
		/// </summary>
		/// <param name="from">Origin macro node bounding box key.</param>
		/// <param name="to">Target macro node bounding box key.</param>
		/// <param name="capability">Movement capabilities of the traversing entity.</param>
		/// <returns><c>true</c> if a matching traversable connection exists; otherwise, <c>false</c>.</returns>
		public bool CanTraverse(BoundingBox from, BoundingBox to, MovementCapability capability) {
			if (this._adjacencyList.TryGetValue(from, out var connections)) {
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
			if (this._adjacencyList.TryGetValue(from, out var connections)) {
				int index = connections.FindIndex(c => c.ToBound == to);
				if (index >= 0) {
					connections[index] = connections[index].WithNarrativeAccess(isAccessible);
				}
			}
		}
	}
}