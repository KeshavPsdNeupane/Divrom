using System;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;
using ThirdParty.PriorityQueeu;

namespace Kope.Feature.PathFinding {
	/// <summary>
	/// Encapsulates pathfinding cost components and provides priority sorting logic.
	/// </summary>
	public readonly struct MicroCost : IComparable<MicroCost>, IEquatable<MicroCost> {
		public readonly int GCost;
		public readonly int HCost;

		public int FCost {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GCost + HCost;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MicroCost(int gCost, int hCost) {
			GCost = gCost;
			HCost = hCost;
		}

		/// <summary>
		/// Sorts primarily by lowest FCost. Breaks ties using lowest HCost (closer to target).
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(MicroCost other) {
			int compare = FCost.CompareTo(other.FCost);
			if (compare == 0) {
				compare = HCost.CompareTo(other.HCost);
			}
			return compare;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(MicroCost other) => GCost == other.GCost && HCost == other.HCost;

		public override bool Equals(object obj) => obj is MicroCost other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => (GCost * 397) ^ HCost;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(MicroCost left, MicroCost right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(MicroCost left, MicroCost right) => !left.Equals(right);

		public override string ToString() => $"Cost(F:{FCost}, G:{GCost}, H:{HCost})";
	}

	/// <summary>
	/// Represents a evaluated pathfinding node wrapper inside the Open/Closed sets.
	/// </summary>
	public readonly struct MicroPathFindingNode : IHasCost<MicroCost>, IEquatable<MicroPathFindingNode> {
		public readonly Vec2Int NodePosition;
		public readonly MicroCost Cost;
		public readonly Vec2Int? Parent;

		#region Passthrough Properties
		public int GCost => Cost.GCost;
		public int HCost => Cost.HCost;
		public int FCost => Cost.FCost;
		#endregion

		#region Constructors
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MicroPathFindingNode(Vec2Int nodePosition, MicroCost cost, Vec2Int? parent) {
			NodePosition = nodePosition;
			Cost = cost;
			Parent = parent;
		}

		/// <summary>Convenience overload to construct directly with raw G and H integer costs.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MicroPathFindingNode(Vec2Int nodePosition, int gCost, int hCost, Vec2Int? parent)
			: this(nodePosition, new MicroCost(gCost, hCost), parent) { }

		#endregion

		#region IHasCost Implementation
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MicroCost GetCost() => Cost;
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
			return $"MicroPathFindingNode(Node: {NodePosition}, {Cost}, Parent: {parentStr})";
		}
		#endregion
	}
}