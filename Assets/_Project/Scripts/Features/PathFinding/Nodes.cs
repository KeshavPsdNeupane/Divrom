using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using UnityEngine;

namespace Kope.Feature.PathFinding {

	/// <summary>
	/// Defines the terrain characteristics and traversal cost categories for a region.
	/// </summary>
	/// <remarks>
	/// Currently uses a fixed enum; intended for future transition to a runtime-dynamic terrain cost system.
	/// </remarks>
	public enum TerrainType {
		OpenGround = 0,
		Mountain = 10,
		DeepWater = 20,
		Forest = 30
	}

	/// <summary>
	/// Represents an immutable 2D integer coordinate in the pathfinding grid.
	/// </summary>
	/// <remarks>
	/// Uses pre-computed hash code caching to maximize performance when used as dictionary keys,
	/// and provides implicit conversions to and from Unity's <see cref="Vector2Int"/>.
	/// </remarks>
	[Serializable]
	public struct Vec2Int : IEquatable<Vec2Int> {
		[SerializeField, ReadOnly] private int _x;
		[SerializeField, ReadOnly] private int _y;
		public readonly int X => this._x;
		public readonly int Y => this._y;

		/// <summary>
		/// Cached hash code generated during construction for fast, zero-allocation dictionary lookups.
		/// </summary>
		private readonly int _hashCode;

		public static readonly Vec2Int Zero = new(0, 0);
		public static readonly Vec2Int One = new(1, 1);
		public static readonly Vec2Int Up = new(0, 1);
		public static readonly Vec2Int Down = new(0, -1);
		public static readonly Vec2Int Left = new(-1, 0);
		public static readonly Vec2Int Right = new(1, 0);

		public Vec2Int(Vector2Int vector) {
			this._x = vector.x;
			this._y = vector.y;
			this._hashCode = HashCode.Combine(this._x, this._y);
		}
		public Vec2Int(Vec2Int vector) {
			this._x = vector._x;
			this._y = vector._y;
			this._hashCode = HashCode.Combine(this._x, this._y);
		}

		public Vec2Int(int _x, int _y) {
			this._x = _x;
			this._y = _y;
			this._hashCode = HashCode.Combine(this._x, this._y);
		}

		public readonly bool Equals(Vec2Int other) {
			return this.X == other.X && this.Y == other.Y;
		}

		public override readonly bool Equals(object obj) {
			return obj is Vec2Int other && this.Equals(other);
		}

		public static bool operator ==(Vec2Int left, Vec2Int right) => left.Equals(right);
		public static bool operator !=(Vec2Int left, Vec2Int right) => !left.Equals(right);

		public static Vec2Int operator +(Vec2Int a, Vec2Int b) => new(a.X + b.X, a.Y + b.Y);
		public static Vec2Int operator -(Vec2Int a, Vec2Int b) => new(a.X - b.X, a.Y - b.Y);
		public static Vec2Int operator *(Vec2Int a, int scalar) => new(a.X * scalar, a.Y * scalar);
		public static Vec2Int operator /(Vec2Int a, int scalar) => new(a.X / scalar, a.Y / scalar);

		public static implicit operator Vector2Int(Vec2Int v) => new(v.X, v.Y);
		public static implicit operator Vec2Int(Vector2Int v) => new(v.x, v.y);

		public override readonly string ToString() => $"({X}, {Y})";
		public override readonly int GetHashCode() => this._hashCode;
	}


	/// <summary>
	/// Represents a fine-grained, high-detail (Tier-2) node in the pathfinding grid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Once a <see cref="MacroGridNode"/> (Tier-1) validates that a high-level path exists, 
	/// <see cref="MicroGridNode"/>s are queried to calculate precise trajectories around local obstacles.
	/// </para>
	/// <para>
	/// Implemented as an immutable value type to eliminate Garbage Collection overhead and avoid 
	/// unnecessary Unity <see cref="UnityEngine.Object"/> wrapper footprint.
	/// </para>
	/// </remarks>
	[Serializable]
	public struct MicroGridNode : IEquatable<MicroGridNode> {
		/// <summary>
		/// Base traversal cost for orthogonal (cardinal) movement.
		/// Scaled by 10 (1.0 * 10) to enable fixed-point integer pathfinding
		/// without floating-point overhead.
		/// </summary>
		public const int DIRECT_COST = 10;

		/// <summary>
		/// Base traversal cost for diagonal movement (sqrt(2) * 10 = 14.14, rounded to 14).
		/// Provides an integer approximation of Euclidean distance for A* cost 
		/// and heuristic evaluations.
		/// </summary>
		public const int DIAGONAL_COST = 14;
		[SerializeField, ReadOnly] private Vec2Int _position;
		[SerializeField, ReadOnly] private bool _isStaticObstacle;
		[SerializeField, ReadOnly] private MacroGridNode _parentMacroGrid;


		public readonly Vec2Int Position => this._position;
		public readonly bool IsStaticObstacle => this._isStaticObstacle;
		public readonly MacroGridNode ParentMacroGrid => this._parentMacroGrid;

		public MicroGridNode(Vec2Int position, bool isStaticObstacle) {
			this._position = position;
			this._isStaticObstacle = isStaticObstacle;
			this._parentMacroGrid = null;
		}

		public MicroGridNode(Vec2Int position, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			this._position = position;
			this._isStaticObstacle = isStaticObstacle;
			this._parentMacroGrid = parentMacroGrid;
		}

		public MicroGridNode(int x, int y, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			this._position = new Vec2Int(x, y);
			this._isStaticObstacle = isStaticObstacle;
			this._parentMacroGrid = parentMacroGrid;
		}

		/// <summary>
		/// Creates a copy of this node with modified optional parameters.
		/// </summary>
		public readonly MicroGridNode CopyWith(Vec2Int? position = null, bool? isStaticObstacle = null,
		 MacroGridNode parentMacroGrid = null) {
			return new MicroGridNode(
				position ?? this._position,
				isStaticObstacle ?? this._isStaticObstacle,
				parentMacroGrid ?? this._parentMacroGrid);
		}

		public readonly bool Equals(MicroGridNode other) {
			return this._position == other._position &&
				   this._isStaticObstacle == other._isStaticObstacle &&
				   this._parentMacroGrid == other._parentMacroGrid;
		}

		public override readonly bool Equals(object obj) => obj is MicroGridNode other && this.Equals(other);
		public static bool operator ==(MicroGridNode left, MicroGridNode right) => left.Equals(right);
		public static bool operator !=(MicroGridNode left, MicroGridNode right) => !left.Equals(right);

		public override readonly string ToString() {
			return $"MicroGridNode(Position: {Position}, IsStaticObstacle: {IsStaticObstacle}, ParentMacroGrid: {ParentMacroGrid?.Bounds})";
		}

		public override readonly int GetHashCode() => this.Position.GetHashCode();
	}

	/// <summary>
	/// Represents a coarse-grained, high-level (Tier-1) region node in the pathfinding grid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Functions as an abstraction layer to evaluate long-distance path existence before calculating fine-grained micro tile paths.
	/// </para>
	/// <para>
	/// Implemented as a reference type (<c>class</c>) to allow shared reference semantics across multiple micro nodes 
	/// and flexible runtime modification of region metadata.
	/// </para>
	/// </remarks>
	[Serializable]
	public sealed class MacroGridNode {
		[SerializeField, ReadOnly] private BoundingBox _bounds;
		[SerializeField, ReadOnly] private TerrainType _terrainType;
		[SerializeField, ReadOnly] private MovementCapability _allowedTraversal;
		public BoundingBox Bounds => this._bounds;
		public TerrainType TerrainType => this._terrainType;
		public MovementCapability AllowedTraversal => this._allowedTraversal;

		private Vec2Int _directionCost = new(-1, -1);
		/// <summary>
		/// Gets the lazy-evaluated orthogonal (per-axis) traversal costs across the bounding box.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="Vec2Int"/> because horizontal (X) and vertical (Y) traversal costs 
		/// differ depending on the rectangular extent of the node.
		/// </remarks>
		public Vec2Int DirectionCost {
			get {
				if (this._directionCost.X < 0 || this._directionCost.Y < 0) {
					this._directionCost = new Vec2Int(
						this.Bounds.Size.X * MicroGridNode.DIRECT_COST,
						this.Bounds.Size.Y * MicroGridNode.DIRECT_COST
					);
				}
				return this._directionCost;
			}
		}
		private int _diagonalCost = -1;

		/// <summary>
		/// Gets the lazy-evaluated Euclidean diagonal traversal cost across the bounding box.
		/// </summary>
		/// <remarks>
		/// Calculated as the scaled hypotenuse (sqrt(W*W + H*H) * DIRECT_COST) rounded to an integer.
		/// Unlike per-axis orthogonal costs, corner-to-corner diagonal distance is a single scalar 
		/// that remains constant regardless of which diagonal path is chosen.
		/// </remarks>
		public int DiagonalCost {
			get {
				if (this._diagonalCost < 0) {
					this._diagonalCost = Mathf.RoundToInt(Mathf.Sqrt(
						this.Bounds.Size.X * this.Bounds.Size.X +
						this.Bounds.Size.Y * this.Bounds.Size.Y
					) * MicroGridNode.DIRECT_COST);
				}
				return this._diagonalCost;
			}
		}

		/// <summary>
		/// Internal list of micro grid node positions contained within this macro node's bounds.
		/// </summary>
		/// <remarks>
		/// Stores lightweight <see cref="Vec2Int"/> coordinates instead of full node instances to maintain 
		/// a single source of truth, prevent circular reference memory leaks, and guarantee Unity ScriptableObject serialization compatibility.
		/// </remarks>
		private readonly List<Vec2Int> microGridNodePositions = new();

		/// <summary>
		/// Gets a read-only view of all micro grid node positions bounded by this macro node.
		/// </summary>
		public IReadOnlyList<Vec2Int> MicroGridNodePositions => microGridNodePositions.AsReadOnly();

		/// <summary>
		/// Gets the total number of micro grid nodes assigned to this macro node.
		/// </summary>
		public int TotalMicroGrids => microGridNodePositions.Count;

		public MacroGridNode(
			BoundingBox bounds,
			TerrainType terrainType,
			MovementCapability allowedTraversal,
			List<Vec2Int> microGridsNodesPositions = null) {
			this._bounds = bounds;
			this._terrainType = terrainType;
			this._allowedTraversal = allowedTraversal;
			this.microGridNodePositions = microGridsNodesPositions ?? new List<Vec2Int>();
		}

		/// <summary>
		/// Calculates the orthogonal transition cost between two macro nodes.
		/// </summary>
		/// <param name="to">The target macro node.</param>
		/// <param name="from">The source macro node.</param>
		/// <returns>A <see cref="Vec2Int"/> representing the per-axis center-to-center traversal costs.</returns>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="to"/> or <paramref name="from"/> is null.</exception>
		/// <remarks>
		/// Evaluates the distance from the center of <paramref name="from"/> to the center of <paramref name="to"/> 
		/// by summing their half-extents: (CostFrom / 2) + (CostTo / 2) = (CostFrom + CostTo) / 2.
		/// </remarks>
		public static Vec2Int GetDirectionalTraversalCost(MacroGridNode to, MacroGridNode from) {
			if (to == null || from == null) {
				throw new ArgumentNullException("MacroGridNode parameters cannot be null.");
			}
			return (to.DirectionCost + from.DirectionCost) / 2;
		}

		/// <summary>
		/// Calculates the diagonal transition cost between two macro nodes.
		/// </summary>
		/// <param name="to">The target macro node.</param>
		/// <param name="from">The source macro node.</param>
		/// <returns>The scalar integer cost for diagonal center-to-center transition.</returns>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="to"/> or <paramref name="from"/> is null.</exception>
		/// <remarks>
		/// Combines the half-hypotenuses of both bounding boxes to estimate region transitions: 
		/// (DiagonalCostFrom / 2) + (DiagonalCostTo / 2) = (DiagonalCostFrom + DiagonalCostTo) / 2.
		/// </remarks>
		public static int GetDiagonalTraversalCost(MacroGridNode to, MacroGridNode from) {
			if (to == null || from == null) {
				throw new ArgumentNullException("MacroGridNode parameters cannot be null.");
			}
			return (to.DiagonalCost + from.DiagonalCost) / 2;
		}

		/// <summary>
		/// Adds a micro grid node position if it is not already registered.
		/// </summary>
		/// <param name="position">The micro grid coordinate to add.</param>
		public void AddMicroGridNodePosition(Vec2Int position) {
			// List<T> is used for native Unity ScriptableObject serialization support.
			// O(N) lookup is fine for small node counts per region. If N grows significantly,
			// implement a serializable HashSet<Vec2Int> wrapper.
			if (!microGridNodePositions.Contains(position)) {
				microGridNodePositions.Add(position);
			}
		}

		/// <summary>
		/// Adds a micro grid node position directly without checking for existing duplicates.
		/// </summary>
		/// <param name="position">The micro grid coordinate to add.</param>
		/// <remarks>
		/// Intended for performance-critical batch initialization steps where caller checks guarantee uniqueness.
		/// </remarks>
		public void PrecheckedAddMicroGridNodePosition(Vec2Int position) {
			microGridNodePositions.Add(position);
		}

		/// <summary>
		/// Removes a micro grid node position from this macro node's registry.
		/// </summary>
		/// <param name="position">The micro grid coordinate to remove.</param>
		public void RemoveMicroGridNodePosition(Vec2Int position) {
			microGridNodePositions.Remove(position);
		}

		public override string ToString() =>
			$"MacroGridNode(Bounds: {Bounds}, TerrainType: {TerrainType}, AllowedTraversal: {AllowedTraversal}, TotalMicroGrids: {TotalMicroGrids})";

		public override int GetHashCode() => Bounds.GetHashCode();
	}


	/// <summary>
	/// Represents an axis-aligned 2D bounding box defined by minimum and maximum grid points.
	/// </summary>
	/// <remarks>
	/// Custom lightweight struct designed with pre-calculated hash codes for high-frequency spatial dictionary lookups,
	/// avoiding the GC/overhead of Unity's native <see cref="Bounds"/> or <see cref="RectInt"/>.
	/// </remarks>
	[Serializable]
	public struct BoundingBox : IEquatable<BoundingBox> {
		[SerializeField, ReadOnly] private Vec2Int _min;
		[SerializeField, ReadOnly] private Vec2Int _max;
		public readonly Vec2Int Min => this._min;
		public readonly Vec2Int Max => this._max;

		/// <summary>
		/// Gets the dimensions of the bounding box along the X and Y axes.
		/// </summary>
		public readonly Vec2Int Size => this.Max - this.Min;

		/// <summary>
		/// Gets the width-to-height ratio of the bounding box. Returns -1 if height is zero.
		/// </summary>
		public float AspectRatio {
			get {
				if (Size.Y == 0) return -1f;
				return (float)Size.X / Size.Y;
			}
		}

		/// <summary>
		/// Cached hash code constructed at instantiation to ensure safe, zero-allocation dictionary operations.
		/// </summary>
		private readonly int _hashCode;

		public BoundingBox(Vec2Int min, Vec2Int max) {
			this._min = min;
			this._max = max;
			this._hashCode = HashCode.Combine(this._min, this._max);
		}

		public BoundingBox(int minX, int minY, int maxX, int maxY) {
			this._min = new Vec2Int(minX, minY);
			this._max = new Vec2Int(maxX, maxY);
			this._hashCode = HashCode.Combine(this._min, this._max);
		}

		/// <summary>
		/// Determines whether the specified point lies inside or on the boundaries of this box.
		/// </summary>
		public readonly bool Contains(Vec2Int point) {
			return point.X >= this._min.X && point.X <= this._max.X &&
				   point.Y >= this._min.Y && point.Y <= this._max.Y;
		}

		/// <summary>
		/// Determines whether this bounding box overlaps with another bounding box.
		/// </summary>
		public readonly bool Intersects(BoundingBox other) {
			return !(other._min.X > this._max.X || other._max.X < this._min.X ||
					 other._min.Y > this._max.Y || other._max.Y < this._min.Y);
		}

		public readonly bool Equals(BoundingBox other) {
			return this._min.Equals(other._min) && this._max.Equals(other._max);
		}

		public readonly override bool Equals(object obj) {
			return obj is BoundingBox other && this.Equals(other);
		}

		public readonly override int GetHashCode() => this._hashCode;

		public static bool operator ==(BoundingBox left, BoundingBox right) => left.Equals(right);
		public static bool operator !=(BoundingBox left, BoundingBox right) => !(left == right);

		public readonly override string ToString() => $"BoundingBox(Min: {this._min}, Max: {this._max})";
	}
}