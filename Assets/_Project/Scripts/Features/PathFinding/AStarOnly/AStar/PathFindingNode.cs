using System.Runtime.CompilerServices;
using Kope.Feature.PathFindingNew.Utility;
using ThirdParty.PriorityQueeu;

namespace Kope.Feature.PathFindingNew.PathFinding {

	/// <summary>
	/// Represents the transient, query-specific search state of a node during an active A* pathfinding execution.
	/// <para>
	/// <strong>Architectural Rationale:</strong>
	/// <list type="bullet">
	/// <item><strong>Separation of Concerns:</strong> Unlike persistent graph topology data (<see cref="GridNode"/>), 
	/// <see cref="PathFindingNode"/> is entirely transient. It exists solely to track search costs (<see cref="GCost"/>, <see cref="HCost"/>, <see cref="FCost"/>) 
	/// and backtrace pointers (<see cref="ParentPosition"/>) for a single path query.</item>
	/// <item><strong>Memory Footprint (~24 Bytes):</strong> 
	/// Comprises an 8-byte <see cref="Position"/>, 4-byte <see cref="GCost"/>, 4-byte <see cref="HCost"/>, and an 8-byte <see cref="ParentPosition"/>. 
	/// For 1,000 active nodes in a search space, this totals ~23.4 KB, ensuring high cache locality during inner-loop priority queue expansions.</item>
	/// <item><strong>Position-Centric Identity:</strong> Equality and hash codes are bound strictly to the grid coordinate (<see cref="Position"/>). 
	/// This ensures that open/closed sets treat multiple evaluation paths to the same coordinate as a single unique node slot.</item>
	/// </list>
	/// </para>
	/// </summary>
	public readonly struct PathFindingNode : IHasCost<int> {

		/// <summary>
		/// Default sentinel coordinate representing an unassigned or root parent position (-1, -1).
		/// </summary>
		public static readonly Vec2Int NoParent = new(-1, -1);

		/// <summary>
		/// The coordinate of this node within the grid topology.
		/// </summary>
		public readonly Vec2Int Position;

		/// <summary>
		/// The exact cost of the cheapest path from the start node to this node.
		/// </summary>
		public readonly int GCost;

		/// <summary>
		/// The heuristic estimated cost from this node to the target destination goal.
		/// </summary>
		public readonly int HCost;

		/// <summary>
		/// The grid coordinate of the parent node that led to this node during the search expansion.
		/// </summary>
		public readonly Vec2Int ParentPosition;

		/// <summary>
		/// Gets the total estimated path cost ($F = G + H$). 
		/// Computed dynamically on access to avoid redundant storage overhead.
		/// </summary>
		public readonly int FCost {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.GCost + this.HCost;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetCost() {
			return this.FCost;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PathFindingNode"/> struct.
		/// </summary>
		/// <param name="x">The X coordinate on the grid.</param>
		/// <param name="y">The Y coordinate on the grid.</param>
		/// <param name="gCost">The movement cost from the start node.</param>
		/// <param name="hCost">The heuristic cost to the target goal.</param>
		/// <param name="parentPosition">The grid coordinate of the parent node.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public PathFindingNode(int x, int y, int gCost, int hCost, Vec2Int parentPosition) {
			this.Position = new Vec2Int(x, y);
			this.GCost = gCost;
			this.HCost = hCost;
			this.ParentPosition = parentPosition;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public PathFindingNode(Vec2Int position, int gCost, int hCost, Vec2Int parentPosition = default) {
			this.Position = position;
			this.GCost = gCost;
			this.HCost = hCost;
			this.ParentPosition = parentPosition == default ? NoParent : parentPosition;
		}
		#region IEquatable Implementation

		/// <summary>
		/// Determines whether this node is equal to another based strictly on their spatial grid positions.
		/// </summary>
		/// <param name="other">The other pathfinding node to compare against.</param>
		/// <returns><c>true</c> if both nodes occupy the same grid position; otherwise, <c>false</c>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(PathFindingNode other) => this.Position == other.Position;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj) => obj is PathFindingNode other && this.Equals(other);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(PathFindingNode left, PathFindingNode right) => left.Equals(right);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(PathFindingNode left, PathFindingNode right) => !left.Equals(right);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Position.GetHashCode();


		#endregion
	}
}