using System;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding;
using ZLinq;

namespace Project.Scripts.Features.PathFinding {

	public readonly struct MacroConnectionData : IEquatable<MacroConnectionData> {
		public BoundingBox Bound { get; }
		public MovementCapability AllowedTraversal { get; }
		public bool IsNarrativelyAccessible { get; }

		public MacroConnectionData(
			BoundingBox targetBounds,
			MovementCapability allowedTraversal,
			bool isNarrativelyAccessible = true) {
			Bound = targetBounds;
			AllowedTraversal = allowedTraversal;
			IsNarrativelyAccessible = isNarrativelyAccessible;
		}

		public MacroConnectionData WithNarrativeAccess(bool isNarrativelyAccessible) {
			return new MacroConnectionData(this.Bound, this.AllowedTraversal, isNarrativelyAccessible);
		}

		public bool IsTraversable(MovementCapability capability) {
			return IsNarrativelyAccessible && (AllowedTraversal & capability) == capability;
		}

		public bool Equals(MacroConnectionData other) {
			return Bound == other.Bound && AllowedTraversal == other.AllowedTraversal && IsNarrativelyAccessible == other.IsNarrativelyAccessible;
		}

		public override bool Equals(object obj) {
			return obj is MacroConnectionData other && this.Equals(other);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Bound, AllowedTraversal, IsNarrativelyAccessible);
		}

		public static bool operator ==(MacroConnectionData left, MacroConnectionData right) {
			return left.Equals(right);
		}

		public static bool operator !=(MacroConnectionData left, MacroConnectionData right) {
			return !left.Equals(right);
		}

		public override string ToString() =>
			$"MacroConnectionData(To: {Bound}, AllowedTraversal: {AllowedTraversal}, IsNarrativelyAccessible: {IsNarrativelyAccessible})";
	}


	public class MacroGraphManager {
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

		public void RegisterNode(MacroGridNode node) {
			this._macroNodes[node.Bounds] = node;
			if (!this._adjacencyList.ContainsKey(node.Bounds)) {
				this._adjacencyList[node.Bounds] = new List<MacroConnectionData>();
			}
		}

		public bool TryGetNode(BoundingBox bounds, out MacroGridNode node) {
			return this._macroNodes.TryGetValue(bounds, out node);
		}

		public void AddConnection(
			BoundingBox from,
			BoundingBox to,
			MovementCapability combinedCapability,
			bool isNarrativelyAccessible = true,
			bool isBidirectional = true) {

			AddDirectedConnection(from, to, combinedCapability, isNarrativelyAccessible);
			if (isBidirectional) {
				AddDirectedConnection(to, from, combinedCapability, isNarrativelyAccessible);
			}
		}

		public void AddConnection(MacroConnectionData from, MacroConnectionData to, bool isBidirectional = true) {
			MovementCapability combinedCapability = from.AllowedTraversal | to.AllowedTraversal;
			bool isNarrativelyAccessible = from.IsNarrativelyAccessible && to.IsNarrativelyAccessible;
			AddDirectedConnection(from.Bound, to.Bound, combinedCapability, isNarrativelyAccessible);
			if (isBidirectional) {
				AddDirectedConnection(to.Bound, from.Bound, combinedCapability, isNarrativelyAccessible);
			}
		}

		private void AddDirectedConnection(
			BoundingBox from,
			BoundingBox to,
			MovementCapability capability,
			bool isNarrativelyAccessible) {

			if (!this._adjacencyList.TryGetValue(from, out var connections)) {
				connections = new List<MacroConnectionData>();
				this._adjacencyList[from] = connections;
			}

			// Fixed: Check existence directly on the list without struct null check
			if (!connections.Exists(c => c.Bound == to)) {
				connections.Add(new MacroConnectionData(to, capability, isNarrativelyAccessible));
			}
		}

		public IEnumerable<MacroConnectionData> GetTraversableConnections(BoundingBox from, MovementCapability capability) {
			var connections = this._adjacencyList.TryGetValue(from, out var list)
				? list
				: EmptyConnections;

			// Fixed: Returns standard ZLinq query without ToList() heap allocation
			return connections.AsValueEnumerable().Where(c => c.IsTraversable(capability)).ToList();
		}

		public bool CanTraverse(BoundingBox from, BoundingBox to, MovementCapability capability) {
			if (this._adjacencyList.TryGetValue(from, out var connections)) {
				return connections.AsValueEnumerable().Any(c => c.Bound == to && c.IsTraversable(capability));
			}
			return false;
		}

		public void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true) {
			ToggleConnectionAccess(from, to, isAccessible);
			if (isBidirectional) {
				ToggleConnectionAccess(to, from, isAccessible);
			}
		}

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