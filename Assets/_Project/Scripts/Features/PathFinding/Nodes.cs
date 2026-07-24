using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using UnityEngine;

namespace Kope.Feature.PathFinding {
	/// <summary>
	/// Defines the terrain characteristics of a region.
	/// Currently uses a fixed enum, intended for future transition to a runtime-dynamic system.
	/// </summary>
	public enum TerrainType {
		OpenGround = 0,
		Mountain = 10,
		DeepWater = 20,
		Forest = 30
	}
	[Serializable]
	public readonly struct Vec2Int : IEquatable<Vec2Int> {
		public int X { get; }
		public int Y { get; }
		// caching the hash code for performance and immutability
		private readonly int _hashCode;


		public readonly static Vec2Int Zero = new(0, 0);
		public readonly static Vec2Int One = new(1, 1);
		public readonly static Vec2Int Up = new(0, 1);
		public readonly static Vec2Int Down = new(0, -1);
		public readonly static Vec2Int Left = new(-1, 0);
		public readonly static Vec2Int Right = new(1, 0);


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

		public static bool operator ==(Vec2Int left, Vec2Int right) {
			return left.Equals(right);
		}
		public static bool operator !=(Vec2Int left, Vec2Int right) {
			return !left.Equals(right);
		}

		public static Vec2Int operator +(Vec2Int a, Vec2Int b) {
			return new Vec2Int(a.X + b.X, a.Y + b.Y);
		}

		public static Vec2Int operator -(Vec2Int a, Vec2Int b) {
			return new Vec2Int(a.X - b.X, a.Y - b.Y);
		}
		public static Vec2Int operator *(Vec2Int a, int scalar) {
			return new Vec2Int(a.X * scalar, a.Y * scalar);
		}
		public static Vec2Int operator /(Vec2Int a, int scalar) {
			return new Vec2Int(a.X / scalar, a.Y / scalar);
		}

		public static implicit operator Vector2Int(Vec2Int v) {
			return new Vector2Int(v.X, v.Y);
		}
		public static implicit operator Vec2Int(Vector2Int v) {
			return new Vec2Int(v.x, v.y);
		}
		public override string ToString() {
			return $"({X}, {Y})";
		}
		public override int GetHashCode() {
			return this._hashCode;
		}
	}


	/// <summary>
	/// Represents a single node in the micro grid, providing a fine-grained representation of the world.
	/// <para>This is a <b>Tier-2</b> node, functioning as the high-detail counterpart to the 
	/// <see cref="MacroGridNode"/> (Tier-1). Once a <see cref="MacroGridNode"/> validates that 
	/// a path exists, the <see cref="MicroGridNode"/> system is used to calculate the precise 
	/// trajectory around obstacles and terrain features.</para>
	/// </summary>
	public sealed class MicroGridNode {
		public Vec2Int Position { get; }
		public bool IsStaticObstacle { get; set; }
		public MacroGridNode ParentMacroGrid { get; set; }
		public MicroGridNode(int x, int y, bool isStaticObstacle) {
			Position = new Vec2Int(x, y);
			IsStaticObstacle = isStaticObstacle;
		}
		public MicroGridNode(Vec2Int position, bool isStaticObstacle) {
			Position = position;
			IsStaticObstacle = isStaticObstacle;
		}
		public MicroGridNode(Vec2Int position, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			Position = position;
			IsStaticObstacle = isStaticObstacle;
			ParentMacroGrid = parentMacroGrid;
		}
		public MicroGridNode(int x, int y, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			Position = new Vec2Int(x, y);
			IsStaticObstacle = isStaticObstacle;
			ParentMacroGrid = parentMacroGrid;
		}
		public void SetParentMacroGrid(MacroGridNode parentMacroGrid) {
			ParentMacroGrid = parentMacroGrid;
		}

		public override string ToString() {
			return $"MicroGridNode(Position: {Position}, IsStaticObstacle: {IsStaticObstacle}, ParentMacroGrid: {ParentMacroGrid})";
		}

		public override int GetHashCode() {
			return this.Position.GetHashCode();
		}
	}

	/// <summary>
	/// Represents a single node in the macro grid, providing a coarse-grained representation of the world.
	/// <para>This is a <b>Tier-1</b> node, functioning as a high-level abstraction layer. 
	/// <see cref="MacroGridNode"/>s are used to determine if a valid path exists between 
	/// distant points. Once the macro-level path is confirmed, the system transitions to 
	/// <see cref="MicroGridNode"/>s for specific, low-level navigation.</para>
	/// </summary>
	public sealed class MacroGridNode {
		public BoundingBox Bounds { get; }
		public TerrainType TerrainType { get; }
		public MovementCapability AllowedTraversal { get; }
		public List<MicroGridNode> MicroGridsNodes { get; } = new();
		public int TotalMicroGrids => MicroGridsNodes.Count;

		public MacroGridNode(
			BoundingBox bounds,
			TerrainType terrainType,
			MovementCapability allowedTraversal,
			List<MicroGridNode> microGridsNodes = null) {
			Bounds = bounds;
			TerrainType = terrainType;
			AllowedTraversal = allowedTraversal;
			MicroGridsNodes = microGridsNodes ?? new List<MicroGridNode>();
		}
		public override string ToString() =>
			$"MacroGridNode(Bounds: {Bounds}, TerrainType: {TerrainType}, AllowedTraversal: {AllowedTraversal}, TotalMicroGrids: {TotalMicroGrids})";

		public override int GetHashCode() => Bounds.GetHashCode();
	}



	/// <summary>
	/// Represents a very lightweight bounding box in 2D space, defined by its minimum and maximum corners.
	/// Uses a custom value type with pre-computed hash caching instead of Unity's Bounds or RectInt 
	/// for optimal performance, memory efficiency, and safe use as high-frequency dictionary keys.
	/// </summary>
	public readonly struct BoundingBox : IEquatable<BoundingBox> {
		public Vec2Int Min { get; }
		public Vec2Int Max { get; }
		public Vec2Int Size => this.Max - this.Min;
		public float AspectRatio {
			get {
				// Avoid division by zero, return a sentinel value
				if (Size.Y == 0) return -1f;
				// the float case is impilicitly casted to float, so the division is done
				// in floating point arithmetic
				return (float)Size.X / Size.Y;
			}
		}

		/// <summary>
		/// Pre-computed hash code for the bounding box, calculated during construction.
		/// Can precompile the hash code because the struct is readonly immutable, ensuring that 
		/// the hash code remains consistent throughout its lifetime.
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

		public readonly bool Contains(Vec2Int point) {
			return point.X >= Min.X && point.X <= Max.X &&
				   point.Y >= Min.Y && point.Y <= Max.Y;
		}

		public readonly bool Intersects(BoundingBox other) {
			return !(other.Min.X > Max.X || other.Max.X < Min.X ||
					 other.Min.Y > Max.Y || other.Max.Y < Min.Y);
		}

		public readonly override string ToString() {
			return $"BoundingBox(Min: {Min}, Max: {Max})";
		}

		public readonly bool Equals(BoundingBox other) {
			return this.Min.Equals(other.Min) && this.Max.Equals(other.Max);
		}

		public readonly override bool Equals(object obj) {
			return obj is BoundingBox other && this.Equals(other);
		}

		public readonly override int GetHashCode() {
			return this._hashCode;
		}

		public static bool operator ==(BoundingBox left, BoundingBox right) {
			return left.Equals(right);
		}

		public static bool operator !=(BoundingBox left, BoundingBox right) {
			return !(left == right);
		}
	}
}
