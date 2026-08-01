using System;
using System.Runtime.CompilerServices;
using Kope.EntityIdentity;
using UnityEngine;

namespace Kope.Feature.PathFindingOld.Node {

	/// <summary>
	/// Represents a coarse-grained, high-level (Tier-1) region node in the pathfinding grid graph.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Functions as an abstraction layer to evaluate macroscopic path viability before delegating 
	/// down to high-density micro tiles.
	/// </para>
	/// <para>
	/// Implemented as a reference type (<c>class</c>) to permit shared node referencing across lists 
	/// and maintain mutable state metadata for dynamic environment changes.
	/// </para>
	/// </remarks>
	public sealed class MacroGridNode {
		#region Fields

		private BoundingBox _bound;
		private readonly TerrainType _terrainType;
		private readonly MovementCapability _allowedTraversal;
		private readonly bool _isBlocked;

		/// <summary>
		/// Internal list tracking micro grid coordinate nodes enclosed by this macro node's boundary.
		/// Stores lightweight <see cref="Vec2Int"/> structures rather than full node allocations to ensure 
		/// rock-solid ScriptableObject serialization compatibility and avoid cross-reference memory leaks.
		/// </summary>
		private readonly Vec2Int[] _microGridNodePositions;


		#endregion

		#region Properties

		/// <summary>Gets the axis-aligned bounding box defining this macro region.</summary>
		public BoundingBox BBox {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._bound;
		}

		/// <summary>Gets the structural terrain type classified for this zone.</summary>
		public TerrainType TerrainType {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._terrainType;
		}

		/// <summary>Gets the movement capabilities permitted within this region.</summary>
		public MovementCapability AllowedTraversal {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._allowedTraversal;
		}
		public bool IsBlocked {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._isBlocked;
		}

		/// <summary>
		/// Base scaling factor for macro-level traversal costs aligned with base grid units.
		/// </summary>
		public const int TRAVERSAL_COST = MicroGridNode.DIRECT_COST;
		public const int DIAGONAL_TRAVERSAL_COST = MicroGridNode.DIAGONAL_COST;

		/// <summary>Gets an immutable read-only view of all micro grid coordinates housed in this macro zone.</summary>
		public ReadOnlySpan<Vec2Int> MicroGridNodePositions {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._microGridNodePositions;
		}

		/// <summary>Gets the total count of constituent micro grid coordinates registered to this macro area.</summary>
		public int TotalMicroGrids {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._microGridNodePositions.Length;
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="MacroGridNode"/> class.
		/// </summary>
		public MacroGridNode(
			BoundingBox bounds,
			TerrainType terrainType,
			MovementCapability allowedTraversal,
			bool isNarrativelyAccessible,
			Vec2Int[] microGridsNodesPositions) {
			this._bound = bounds;
			this._terrainType = terrainType;
			this._allowedTraversal = allowedTraversal;
			this._isBlocked = isNarrativelyAccessible;
			this._microGridNodePositions = microGridsNodesPositions ?? Array.Empty<Vec2Int>();
			// this.MicroGridNodePositionsSet = new HashSet<Vec2Int>(this._microGridNodePositions);
		}

		#endregion

		#region Methods 


		public bool CanTraverse(MovementCapability capability) {
			// Check if the macro node is narratively accessible and if the allowed traversal 
			// capabilities include the specified capability.
			return !this.IsBlocked && (this.AllowedTraversal & capability) != MovementCapability.None;
		}
		#endregion

		#region Cost Calculation
		/// <summary>
		/// Calculates the directional movement cost between two macro region nodes using Manhattan distance.
		/// </summary>
		/// <remarks>
		/// Because macro regions vary in physical scale, this computes linear Manhattan spacing between 
		/// centers multiplied by <see cref="TRAVERSAL_COST"/>, avoiding expensive square root math 
		/// while ensuring equal step cost scaling.
		/// </remarks>
		/// <param name="to">Destination macro grid node.</param>
		/// <param name="from">Origin macro grid node.</param>
		/// <returns>Computed integer directional path traversal cost.</returns>
		/// <exception cref="ArgumentNullException">Thrown if either node parameter is null.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ManHattenCost(BoundingBox to, BoundingBox from) {
			int manhattanDistance = Vec2Int.ManhattanDistanceTo(from.Center, to.Center);
			return manhattanDistance * TRAVERSAL_COST;
		}
		public static int EuclideanCost(BoundingBox to, BoundingBox from) {
			float euclideanDistance = Vec2Int.EuclideanDistanceTo(from.Center, to.Center);
			return Mathf.RoundToInt(euclideanDistance * TRAVERSAL_COST);
		}
		public static int OctileCost(BoundingBox to, BoundingBox from) {
			int octileDistance = Vec2Int.OctileDistanceTo(
				from.Center, to.Center, TRAVERSAL_COST,
				DIAGONAL_TRAVERSAL_COST);
			return octileDistance;
		}
		#endregion


		#region Overrides
		public override string ToString() =>
			$"MacroGridNode(Bounds: {this._bound}, TerrainType: {this.TerrainType}, AllowedTraversal: {this.AllowedTraversal}, TotalMicroGrids: {this.TotalMicroGrids})";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this._bound.GetHashCode();

		#endregion
	}
}