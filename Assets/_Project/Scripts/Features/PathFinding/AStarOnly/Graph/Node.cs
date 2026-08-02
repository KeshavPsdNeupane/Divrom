using System.Runtime.CompilerServices;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;

/// <summary>
/// Immutable, lightweight representation of a single tile node in the pathfinding grid.
/// Holds spatial position, movement mode compatibility, traversability, and cost multipliers.
/// </summary>
public readonly struct GridNode : System.IEquatable<GridNode> {
	public const int DIRECT_COST = 10;
	public const int DIAGONAL_COST = 14;

	// just cascade from TileTerrainData to avoid a circular dependency between Graph and Tile,
	// and stale values if the constant is ever changed in TileTerrainData.
	public const ushort NON_TRAVERSABLE_REGION_ID = TileTerrainData.NON_TRAVERSABLE_REGION_ID;


	private readonly ushort regionId;
	private readonly Vec2Int position;
	private readonly TileType tileType;
	private readonly MovementCapability movementType;
	private readonly bool isTraversable;
	private readonly float moveCostMultiplier;
	private readonly float swimCostMultiplier;
	private readonly float flyCostMultiplier;

	public ushort RegionId => this.regionId;
	/// <summary> Grid coordinate position of this node. </summary>
	public readonly Vec2Int Position => this.position;

	/// <summary> Movement capabilities supported by this tile (e.g., Ground, Water, Air). </summary>
	public readonly MovementCapability MovementType => this.movementType;

	/// <summary>
	/// Initializes a new immutable <see cref="GridNode"/> instance.
	/// </summary>
	public GridNode(
		ushort regionId,
		Vec2Int position,
		TileType tileType,
		MovementCapability movementType,
		bool isTraversable,
		float moveCostMultiplier,
		float swimCostMultiplier,
		float flyCostMultiplier) {

		this.regionId = regionId;
		this.position = position;
		this.tileType = tileType;
		this.movementType = movementType;
		this.isTraversable = isTraversable;
		this.moveCostMultiplier = moveCostMultiplier;
		this.swimCostMultiplier = swimCostMultiplier;
		this.flyCostMultiplier = flyCostMultiplier;
	}

	/// <summary>
	/// Determines whether two nodes reside within the same connected region and are topologically reachable from each other.
	/// <para>
	/// <b>Note:</b> Reachability and Traversability are distinct concepts. <i>Traversability</i> defines whether an individual 
	/// node can be walked on given an agent's capabilities, whereas <i>Reachability</i> guarantees an existing path exists 
	/// between two traversable nodes via matching region IDs.
	/// </para>
	/// </summary>
	/// <param name="a">The origin grid node.</param>
	/// <param name="b">The destination grid node.</param>
	/// <returns><c>true</c> if both nodes belong to valid, traversable regions and share the same region ID; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsRegionReachable(GridNode a, GridNode b) {
		return a.regionId != NON_TRAVERSABLE_REGION_ID &&
			   b.regionId != NON_TRAVERSABLE_REGION_ID &&
			   a.regionId == b.regionId;
	}

	/// <summary>
	/// Determines whether this node can be traversed by an agent with the specified movement capabilities.
	/// </summary>
	/// <param name="validModes">Bitmask representing the active movement capabilities of the querying agent (e.g., Move, Swim, Fly).</param>
	/// <returns><c>true</c> if the node is master-traversable, non-void, and shares at least one compatible movement mode; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsTraversable(MovementCapability validModes) =>
		this.isTraversable && this.tileType != TileType.VOID && (validModes & this.movementType) != MovementCapability.NoAbilityToMove;



	/// <summary>
	/// Evaluates the lowest movement cost multiplier among all modes shared between this node and the agent.
	/// </summary>
	/// <param name="validModes">Bitmask of movement modes supported by the querying agent.</param>
	/// <returns>The cheapest valid cost multiplier, or <c>-1f</c> if no compatible mode exists.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float GetCostMultiplier(MovementCapability validModes) {
		// Previously this recomputed `validModes & movementType` inside each of the three branches
		// on top of an already-redundant `(validModes & X) != 0` guard (that guard is implied by
		// the second condition, since a nonzero `validModes & X & movementType` requires
		// `validModes & X` to already be nonzero). Hoisting the intersection once removes 3
		// redundant ANDs and 3 redundant zero-checks per call with identical results.
		MovementCapability compatible = validModes & this.movementType;
		if (compatible == MovementCapability.NoAbilityToMove) return -1f;

		float bestCost = -1f;
		if ((compatible & MovementCapability.Move) != 0) {
			bestCost = this.moveCostMultiplier;
		}
		if ((compatible & MovementCapability.Swim) != 0 && (bestCost < 0f || this.swimCostMultiplier < bestCost)) {
			bestCost = this.swimCostMultiplier;
		}
		if ((compatible & MovementCapability.Fly) != 0 && (bestCost < 0f || this.flyCostMultiplier < bestCost)) {
			bestCost = this.flyCostMultiplier;
		}

		return bestCost;
	}

	/// <summary>
	/// Fused traversability + cost lookup for hot-path edge expansion (A* neighbor loop).
	/// <para>
	/// <see cref="IsTraversable"/> and <see cref="GetCostMultiplier"/>, called back-to-back as in the
	/// original expansion loop, each independently compute a <c>validModes &amp; movementType</c>-style
	/// compatibility check. This method computes that intersection exactly once and reuses it for both
	/// the walkability gate and the cost lookup, which matters here because it runs once per candidate
	/// edge during every A* search.
	/// </para>
	/// </summary>
	/// <param name="validModes">Bitmask of movement modes supported by the querying agent.</param>
	/// <param name="costMultiplier">The cheapest valid cost multiplier for the compatible modes, or <c>-1f</c> if untraversable / incompatible.</param>
	/// <returns><c>true</c> if the node is walkable by at least one shared movement mode; otherwise <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetTraversalCost(MovementCapability validModes, out float costMultiplier) {
		costMultiplier = -1f;
		if (!this.isTraversable || this.tileType == TileType.VOID) return false;

		MovementCapability compatible = validModes & this.movementType;
		if (compatible == MovementCapability.NoAbilityToMove) return false;

		if ((compatible & MovementCapability.Move) != 0) {
			costMultiplier = this.moveCostMultiplier;
		}
		if ((compatible & MovementCapability.Swim) != 0 && (costMultiplier < 0f || this.swimCostMultiplier < costMultiplier)) {
			costMultiplier = this.swimCostMultiplier;
		}
		if ((compatible & MovementCapability.Fly) != 0 && (costMultiplier < 0f || this.flyCostMultiplier < costMultiplier)) {
			costMultiplier = this.flyCostMultiplier;
		}

		return true;
	}

	#region Heuristic Distance Calculations


	/// <summary>
	/// Calculates the estimated Manhattan (4-directional grid) heuristic cost <c>h(n)</c> from position <paramref name="a"/> to <paramref name="b"/>.
	/// Uses absolute orthogonal grid distance scaled by the target terrain cost multiplier and <see cref="DIRECT_COST"/>.
	/// </summary>
	/// <param name="a">Starting grid position.</param>
	/// <param name="b">Target grid position.</param>
	/// <param name="toCostMultiplier">Cost multiplier of the destination tile.</param>
	/// <returns>The estimated heuristic cost <c>h(n)</c> to reach the target.</returns>
	public static int ManhattanDistanceTo(Vec2Int a, Vec2Int b, float toCostMultiplier = 1f) {
		int distance = Vec2Int.ManhattanDistanceTo(a, b);
		return Mathf.RoundToInt(distance * toCostMultiplier) * DIRECT_COST;
	}

	/// <summary>
	/// Calculates the estimated Euclidean (straight-line) heuristic cost <c>h(n)</c> from position <paramref name="a"/> to <paramref name="b"/>.
	/// Uses geometric distance scaled by the target terrain cost multiplier and <see cref="DIRECT_COST"/>.
	/// </summary>
	/// <param name="a">Starting grid position.</param>
	/// <param name="b">Target grid position.</param>
	/// <param name="toCostMultiplier">Cost multiplier of the destination tile.</param>
	/// <returns>The estimated straight-line heuristic cost <c>h(n)</c> to reach the target.</returns>
	public static int EuclideanDistanceTo(Vec2Int a, Vec2Int b, float toCostMultiplier = 1f) {
		return Mathf.RoundToInt(Vec2Int.EuclideanDistanceTo(a, b) * toCostMultiplier) * DIRECT_COST;
	}

	/// <summary>
	/// Calculates the estimated Octile (8-directional grid) heuristic cost <c>h(n)</c> from position <paramref name="a"/> to <paramref name="b"/>.
	/// Accounts for combined orthogonal and diagonal steps, scaled by the target terrain cost multiplier and <see cref="DIAGONAL_COST"/>.
	/// </summary>
	/// <param name="a">Starting grid position.</param>
	/// <param name="b">Target grid position.</param>
	/// <param name="toCostMultiplier">Cost multiplier of the destination tile.</param>
	/// <returns>The estimated 8-way heuristic cost <c>h(n)</c> to reach the target.</returns>
	public static int OctileDistanceTo(Vec2Int a, Vec2Int b, float toCostMultiplier = 1f) {
		return Mathf.RoundToInt(Vec2Int.OctileDistanceTo(a, b) * toCostMultiplier) * DIAGONAL_COST;
	}

	#endregion
	#region Overrides and Equality

	/// <summary>
	/// Evaluates equality based strictly on spatial position (unique key per tile).
	/// </summary>
	public bool Equals(GridNode other) {
		return this.position == other.position;
	}

	public override bool Equals(object obj) {
		return obj is GridNode other && this.Equals(other);
	}

	public override int GetHashCode() {
		return this.position.GetHashCode();
	}

	public static bool operator ==(GridNode left, GridNode right) {
		return left.Equals(right);
	}

	public static bool operator !=(GridNode left, GridNode right) {
		return !left.Equals(right);
	}

	public override string ToString() {
		return $"GridNode(Position: {this.position}, MovementType: {this.movementType}, IsTraversable: {this.isTraversable}, " +
			   $"MoveCostMultiplier: {this.moveCostMultiplier}, SwimCostMultiplier: {this.swimCostMultiplier}, FlyCostMultiplier: {this.flyCostMultiplier})";
	}

	#endregion
}