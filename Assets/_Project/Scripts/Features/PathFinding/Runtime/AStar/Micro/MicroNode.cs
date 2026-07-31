using System;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;
using ThirdParty.PriorityQueeu;

namespace Kope.Feature.PathFinding {
	/// <summary>
	/// Represents a evaluated pathfinding node wrapper inside the Open/Closed sets.
	/// </summary>
	public readonly struct MicroPathFindingNode : IHasCost<int>, IEquatable<MicroPathFindingNode> {
		public readonly Vec2Int NodePosition;
		public readonly Vec2Int? Parent;

		#region Passthrough Properties
		public readonly int GCost;
		public readonly int HCost;
		public int FCost {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GCost + HCost;
		}
		#endregion

		#region Constructors

		public MicroPathFindingNode(Vec2Int nodePosition, int gCost, int hCost, Vec2Int? parent) {
			this.NodePosition = nodePosition;
			this.GCost = gCost;
			this.HCost = hCost;
			this.Parent = parent;
		}

		#endregion

		#region IHasCost Implementation
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetCost() => FCost;
		#endregion

		#region IEquatable & Identity
		// Node equality in pathfinding is strictly determined by grid position
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(MicroPathFindingNode other) => NodePosition.Equals(other.NodePosition);

		public override bool Equals(object obj) => obj is MicroPathFindingNode otherNode && Equals(otherNode);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => NodePosition.GetHashCode();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(MicroPathFindingNode left, MicroPathFindingNode right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(MicroPathFindingNode left, MicroPathFindingNode right) => !left.Equals(right);

		public override string ToString() {
			string parentStr = Parent.HasValue ? Parent.Value.ToString() : "None";
			return $"MicroPathFindingNode(Node: {NodePosition}, G: {GCost}, H: {HCost}, F: {FCost}, Parent: {parentStr})";
		}
		#endregion
	}
}