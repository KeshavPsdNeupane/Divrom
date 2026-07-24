using System;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding;
using ZLinq;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Represents directed edge connection data between two <see cref="MacroGridNode"/> instances.
	/// </summary>
	/// <remarks>
	/// Immutable value type storing traversal rules for graph edges. Combines physical movement capabilities 
	/// (e.g., Walking, Flying via bitmasks) with dynamic story/gameplay availability flags.
	/// </remarks>
	public readonly struct MacroConnectionData : IEquatable<MacroConnectionData> {

		/// <summary>
		/// Gets the bounding box of the target macro grid node this connection leads to.
		/// </summary>
		public BoundingBox Bound { get; }

		/// <summary>
		/// Gets the movement capabilities required to traverse this connection.
		/// </summary>
		public MovementCapability AllowedTraversal { get; }

		/// <summary>
		/// Gets a value indicating whether current narrative or story flags allow traversal across this connection.
		/// </summary>
		public bool IsNarrativelyAccessible { get; }

		public MacroConnectionData(
			BoundingBox targetBounds,
			MovementCapability allowedTraversal,
			bool isNarrativelyAccessible = true) {
			Bound = targetBounds;
			AllowedTraversal = allowedTraversal;
			IsNarrativelyAccessible = isNarrativelyAccessible;
		}

		/// <summary>
		/// Creates a new <see cref="MacroConnectionData"/> copy with updated narrative accessibility.
		/// </summary>
		/// <param name="isNarrativelyAccessible">The new accessibility state.</param>
		/// <returns>A new connection struct instance with updated narrative access.</returns>
		public MacroConnectionData WithNarrativeAccess(bool isNarrativelyAccessible) {
			return new MacroConnectionData(this.Bound, this.AllowedTraversal, isNarrativelyAccessible);
		}

		/// <summary>
		/// Determines whether an entity with the specified movement capability can traverse this connection.
		/// </summary>
		/// <param name="capability">The movement capabilities of the traversing entity.</param>
		/// <returns><c>true</c> if narrative access is granted and the entity satisfies required movement flags; otherwise, <c>false</c>.</returns>
		public bool IsTraversable(MovementCapability capability) {
			return IsNarrativelyAccessible && (AllowedTraversal & capability) == capability;
		}

		public bool Equals(MacroConnectionData other) {
			return Bound == other.Bound &&
				   AllowedTraversal == other.AllowedTraversal &&
				   IsNarrativelyAccessible == other.IsNarrativelyAccessible;
		}

		public override bool Equals(object obj) {
			return obj is MacroConnectionData other && this.Equals(other);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Bound, AllowedTraversal, IsNarrativelyAccessible);
		}

		public static bool operator ==(MacroConnectionData left, MacroConnectionData right) => left.Equals(right);
		public static bool operator !=(MacroConnectionData left, MacroConnectionData right) => !left.Equals(right);

		public override string ToString() =>
			$"MacroConnectionData(To: {Bound}, AllowedTraversal: {AllowedTraversal}, IsNarrativelyAccessible: {IsNarrativelyAccessible})";
	}


	/// <summary>
	/// Manages high-level macro grid nodes and evaluates graph connectivity for tier-1 pathfinding.
	/// </summary>
	/// <remarks>
	/// Maintains spatial node lookups and graph adjacency lists, providing zero-allocation capability evaluation 
	/// and runtime story/quest access toggling for region transitions.
	/// </remarks>
	public class MacroGraphManager {

		/// <summary>
		/// Cached empty list instance returned when querying connections for unregistered nodes to avoid allocations.
		/// </summary>
		private static readonly List<MacroConnectionData> EmptyConnections = new();

		private readonly SerializableDictionary<BoundingBox, MacroGridNode> _macroNodes;
		private readonly SerializableDictionary<BoundingBox, List<MacroConnectionData>> _adjacencyList;

		public MacroGraphManager() {
			this._macroNodes = new();
			this._adjacencyList = new();
		}

		public MacroGraphManager(
			SerializableDictionary<BoundingBox, MacroGridNode> macroNodes,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> adjacencyList) {
			this._macroNodes = macroNodes;
			this._adjacencyList = adjacencyList;
		}

		/// <summary>
		/// Registers a macro node in the graph lookup and initializes its adjacency list entry.
		/// </summary>
		/// <param name="node">The macro node instance to register.</param>
		public void RegisterNode(MacroGridNode node) {
			this._macroNodes[node.Bounds] = node;
			if (!this._adjacencyList.ContainsKey(node.Bounds)) {
				this._adjacencyList[node.Bounds] = new List<MacroConnectionData>();
			}
		}

		/// <summary>
		/// Attempts to retrieve a registered macro grid node by its bounding box.
		/// </summary>
		/// <param name="bounds">The key bounding box to look up.</param>
		/// <param name="node">When successful, receives the associated <see cref="MacroGridNode"/>.</param>
		/// <returns><c>true</c> if the node was found; otherwise, <c>false</c>.</returns>
		public bool TryGetNode(BoundingBox bounds, out MacroGridNode node) {
			return this._macroNodes.TryGetValue(bounds, out node);
		}

		/// <summary>
		/// Adds a directed or bidirectional edge between two macro grid nodes with combined capabilities and narrative accessibility.
		/// </summary>
		/// <param name="from">Origin node bounds.</param>
		/// <param name="to">Destination node bounds.</param>
		/// <param name="Tocapability">Allowed capabilities when moving towards destination.</param>
		/// <param name="fromCapability">Allowed capabilities when moving towards origin.</param>
		/// <param name="toIsNarrativelyAccessible">Story access state toward destination.</param>
		/// <param name="fromIsNarrativelyAccessible">Story access state toward origin.</param>
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
		/// Adds a single directed edge from one macro node to another without duplicating existing entries.
		/// </summary>
		private void AddDirectedConnection(
			BoundingBox from,
			BoundingBox to,
			MovementCapability capability,
			bool isNarrativelyAccessible) {

			if (!this._adjacencyList.TryGetValue(from, out var connections)) {
				connections = new List<MacroConnectionData>();
				this._adjacencyList[from] = connections;
			}

			if (!connections.Exists(c => c.Bound == to)) {
				connections.Add(new MacroConnectionData(to, capability, isNarrativelyAccessible));
			}
		}

		/// <summary>
		/// Retrieves all outgoing connections from a given macro node that satisfy specified movement capabilities and narrative states.
		/// </summary>
		/// <param name="from">The origin macro node bounds.</param>
		/// <param name="capability">Movement capabilities to filter by.</param>
		/// <returns>A collection of traversable connections.</returns>
		public IEnumerable<MacroConnectionData> GetTraversableConnections(BoundingBox from, MovementCapability capability) {
			var connections = this._adjacencyList.TryGetValue(from, out var list)
				? list
				: EmptyConnections;

			// Leverages ZLinq value-enumerable filtering before materializing list
			return connections.AsValueEnumerable().Where(c => c.IsTraversable(capability)).ToList();
		}

		/// <summary>
		/// Evaluates whether a direct, traversable edge exists from a source macro node to a target macro node.
		/// </summary>
		/// <param name="from">Origin macro node bounds.</param>
		/// <param name="to">Target macro node bounds.</param>
		/// <param name="capability">Movement capabilities of the entity.</param>
		/// <returns><c>true</c> if a matching traversable edge exists; otherwise, <c>false</c>.</returns>
		public bool CanTraverse(BoundingBox from, BoundingBox to, MovementCapability capability) {
			if (this._adjacencyList.TryGetValue(from, out var connections)) {
				return connections.AsValueEnumerable().Any(c => c.Bound == to && c.IsTraversable(capability));
			}
			return false;
		}

		/// <summary>
		/// Updates the narrative accessibility status for connections between two nodes.
		/// </summary>
		/// <param name="from">Origin node bounds.</param>
		/// <param name="to">Target node bounds.</param>
		/// <param name="isAccessible">The new narrative accessibility status.</param>
		/// <param name="isBidirectional">If <c>true</c>, updates narrative access in both directions.</param>
		public void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true) {
			ToggleConnectionAccess(from, to, isAccessible);
			if (isBidirectional) {
				ToggleConnectionAccess(to, from, isAccessible);
			}
		}

		/// <summary>
		/// Internal helper to update a specific connection's narrative access flag in-place inside the list.
		/// </summary>
		private void ToggleConnectionAccess(BoundingBox from, BoundingBox to, bool isAccessible) {
			if (this._adjacencyList.TryGetValue(from, out var connections)) {
				int index = connections.FindIndex(c => c.Bound == to);
				if (index >= 0) {
					connections[index] = connections[index].WithNarrativeAccess(isAccessible);
				}
			}
		}
	}
}