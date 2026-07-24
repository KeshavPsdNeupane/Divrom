using System;
using System.Collections.Generic;
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
	public readonly struct Vec2Int : IEquatable<Vec2Int> {
		public int X { get; }
		public int Y { get; }

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
			this.X = vector.x;
			this.Y = vector.y;
			this._hashCode = HashCode.Combine(X, Y);
		}

		public Vec2Int(int x, int y) {
			this.X = x;
			this.Y = y;
			this._hashCode = HashCode.Combine(X, Y);
		}

		public readonly bool Equals(Vec2Int other) {
			return this.X == other.X && this.Y == other.Y;
		}

		public override bool Equals(object obj) {
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

		public override string ToString() => $"({X}, {Y})";
		public override int GetHashCode() => this._hashCode;
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
	public readonly struct MicroGridNode : IEquatable<MicroGridNode> {
		public Vec2Int Position { get; }
		public bool IsStaticObstacle { get; }
		public MacroGridNode ParentMacroGrid { get; }

		public MicroGridNode(Vec2Int position, bool isStaticObstacle) {
			this.Position = position;
			this.IsStaticObstacle = isStaticObstacle;
			this.ParentMacroGrid = null;
		}

		public MicroGridNode(Vec2Int position, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			this.Position = position;
			this.IsStaticObstacle = isStaticObstacle;
			this.ParentMacroGrid = parentMacroGrid;
		}

		public MicroGridNode(int x, int y, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			this.Position = new Vec2Int(x, y);
			this.IsStaticObstacle = isStaticObstacle;
			this.ParentMacroGrid = parentMacroGrid;
		}

		/// <summary>
		/// Creates a copy of this node with modified optional parameters.
		/// </summary>
		public MicroGridNode CopyWith(Vec2Int? position = null, bool? isStaticObstacle = null,
		 MacroGridNode parentMacroGrid = null) {
			return new MicroGridNode(
				position ?? this.Position,
				isStaticObstacle ?? this.IsStaticObstacle,
				parentMacroGrid ?? this.ParentMacroGrid);
		}

		public bool Equals(MicroGridNode other) {
			return this.Position == other.Position &&
				   this.IsStaticObstacle == other.IsStaticObstacle &&
				   this.ParentMacroGrid == other.ParentMacroGrid;
		}

		public override bool Equals(object obj) => obj is MicroGridNode other && this.Equals(other);
		public static bool operator ==(MicroGridNode left, MicroGridNode right) => left.Equals(right);
		public static bool operator !=(MicroGridNode left, MicroGridNode right) => !left.Equals(right);

		public override string ToString() {
			return $"MicroGridNode(Position: {Position}, IsStaticObstacle: {IsStaticObstacle}, ParentMacroGrid: {ParentMacroGrid?.Bounds})";
		}

		public override int GetHashCode() => this.Position.GetHashCode();
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
	public sealed class MacroGridNode {
		public BoundingBox Bounds { get; }
		public TerrainType TerrainType { get; }
		public MovementCapability AllowedTraversal { get; }

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
			Bounds = bounds;
			TerrainType = terrainType;
			AllowedTraversal = allowedTraversal;
			microGridNodePositions = microGridsNodesPositions ?? new List<Vec2Int>();
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
	public readonly struct BoundingBox : IEquatable<BoundingBox> {
		public Vec2Int Min { get; }
		public Vec2Int Max { get; }

		/// <summary>
		/// Gets the dimensions of the bounding box along the X and Y axes.
		/// </summary>
		public Vec2Int Size => this.Max - this.Min;

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
			this.Min = min;
			this.Max = max;
			this._hashCode = HashCode.Combine(Min, Max);
		}

		public BoundingBox(int minX, int minY, int maxX, int maxY) {
			this.Min = new Vec2Int(minX, minY);
			this.Max = new Vec2Int(maxX, maxY);
			this._hashCode = HashCode.Combine(Min, Max);
		}

		/// <summary>
		/// Determines whether the specified point lies inside or on the boundaries of this box.
		/// </summary>
		public readonly bool Contains(Vec2Int point) {
			return point.X >= Min.X && point.X <= Max.X &&
				   point.Y >= Min.Y && point.Y <= Max.Y;
		}

		/// <summary>
		/// Determines whether this bounding box overlaps with another bounding box.
		/// </summary>
		public readonly bool Intersects(BoundingBox other) {
			return !(other.Min.X > Max.X || other.Max.X < Min.X ||
					 other.Min.Y > Max.Y || other.Max.Y < Min.Y);
		}

		public readonly bool Equals(BoundingBox other) {
			return this.Min.Equals(other.Min) && this.Max.Equals(other.Max);
		}

		public readonly override bool Equals(object obj) {
			return obj is BoundingBox other && this.Equals(other);
		}

		public readonly override int GetHashCode() => this._hashCode;

		public static bool operator ==(BoundingBox left, BoundingBox right) => left.Equals(right);
		public static bool operator !=(BoundingBox left, BoundingBox right) => !(left == right);

		public readonly override string ToString() => $"BoundingBox(Min: {Min}, Max: {Max})";
	}
}