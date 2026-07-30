using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using UnityEngine;

namespace Kope.Feature.PathFinding.Node {

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
	[Serializable]
	public sealed class MacroGridNode {
		#region Fields

		[SerializeField, ReadOnly] private BoundingBox _bound;
		[SerializeField, ReadOnly] private TerrainType _terrainType;
		[SerializeField, ReadOnly] private MovementCapability _allowedTraversal;

		/// <summary>
		/// Internal list tracking micro grid coordinate nodes enclosed by this macro node's boundary.
		/// Stores lightweight <see cref="Vec2Int"/> structures rather than full node allocations to ensure 
		/// rock-solid ScriptableObject serialization compatibility and avoid cross-reference memory leaks.
		/// </summary>
		[SerializeField, HideInInspector] private List<Vec2Int> _microGridNodePositions = new();

		#endregion

		#region Properties

		/// <summary>Gets the axis-aligned bounding box defining this macro region.</summary>
		public BoundingBox Bound {
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

		/// <summary>
		/// Base scaling factor for macro-level traversal costs aligned with base grid units.
		/// </summary>
		public const int TRAVERSAL_COST = MicroGridNode.DIRECT_COST;

		/// <summary>Gets an immutable read-only view of all micro grid coordinates housed in this macro zone.</summary>
		public IReadOnlyList<Vec2Int> MicroGridNodePositions {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._microGridNodePositions.AsReadOnly();
		}

		/// <summary>Gets the total count of constituent micro grid coordinates registered to this macro area.</summary>
		public int TotalMicroGrids {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._microGridNodePositions.Count;
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
			List<Vec2Int> microGridsNodesPositions = null) {
			this._bound = bounds;
			this._terrainType = terrainType;
			this._allowedTraversal = allowedTraversal;
			this._microGridNodePositions = microGridsNodesPositions ?? new List<Vec2Int>();
		}

		#endregion

		#region Methods 
		/// <summary>
		/// Adds a unique micro grid position to this region's spatial registry.
		/// </summary>
		public void AddMicroGridNodePosition(Vec2Int position) {
			if (!this._microGridNodePositions.Contains(position)) {
				this._microGridNodePositions.Add(position);
			}
		}

		/// <summary>
		/// Adds a micro grid node coordinate without running duplicate validation checks.
		/// Intended for performance-sensitive batch setups where uniqueness is pre-guaranteed.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrecheckedAddMicroGridNodePosition(Vec2Int position) {
			this._microGridNodePositions.Add(position);
		}

		/// <summary>
		/// Removes a specified micro grid position from this macro node's registry list.
		/// </summary>
		public void RemoveMicroGridNodePosition(Vec2Int position) {
			this._microGridNodePositions.Remove(position);
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
			int octileDistance = Vec2Int.OctileDistanceTo(from.Center, to.Center);
			return octileDistance * TRAVERSAL_COST;
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