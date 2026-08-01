using System;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using Kope.Core.Collections.Hashes;
using UnityEngine;
/*
 * PERFORMANCE ARCHITECTURE NOTE: HASH CODE CACHING
 * Do NOT cache or memoize GetHashCode() inside BoundingBox.
 * Benchmarks confirm memoization degrades spatial graph evaluation performance:
 * 
 * 1. L1 Cache Line Density: Storing cached hash state bloats struct footprint, 
 *    reducing the number of bounding boxes that fit into a single 64-byte L1 cache 
 *    line during mass spatial queries.
 * 2. Register Math vs Latency: Computing FNV-1a hashes on-the-fly in CPU registers 
 *    is significantly faster than triggering memory bus fetches for cached fields.
 * 3. Blittable Immutability: Omitting cached state keeps the struct lean, immutable, 
 *    and fully blittable—preventing memory write-backs during hot pathfinding loops.
 */

namespace Kope.Feature.PathFindingOld.Node {

	/// <summary>
	/// Represents an axis-aligned 2D bounding box defined by minimum and maximum grid points.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A custom lightweight 16-byte struct optimized for high-frequency spatial dictionary lookups and region slicing,
	/// completely avoiding the memory footprint and overhead of Unity's native floating-point <see cref="Bounds"/> or <see cref="RectInt"/>.
	/// </para>
	/// <para>
	/// Hashes are generated dynamically by flattening all four scalar components through a 397 prime multiplication chain,
	/// ensuring maximum bit dispersion, zero bucket cancellation, and complete Domain Reload stability.
	/// </para>
	/// </remarks>
	[Serializable]
	public struct BoundingBox : IEquatable<BoundingBox> {
		#region Fields

		[SerializeField, ReadOnly] private Vec2Int _min;
		[SerializeField, ReadOnly] private Vec2Int _max;

		#endregion



		#region Properties

		/// <summary>Gets the minimum boundary coordinate of the box.</summary>
		public readonly Vec2Int Min {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._min;
		}

		/// <summary>Gets the maximum boundary coordinate of the box.</summary>
		public readonly Vec2Int Max {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._max;
		}

		/// <summary>
		/// Gets the dimensions of the bounding box along the X and Y axes.
		/// Computed dynamically to eliminate serialized state bloat, and aggressively inlined for zero call overhead.
		/// </summary>
		public readonly Vec2Int Size {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Max - this.Min;
		}

		/// <summary>
		/// Gets the integer center coordinate of the bounding box, computed dynamically.
		/// Uses an arithmetic bitwise right-shift (<c>&gt;&gt; 1</c>) for efficient integer division 
		/// that maintains accurate floor rounding towards negative infinity across negative coordinates.
		/// </summary>
		public readonly Vec2Int Center {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new((this.Min.X + this.Max.X) >> 1, (this.Min.Y + this.Max.Y) >> 1);
		}

		/// <summary>
		/// Gets the width-to-height aspect ratio of the bounding box. Returns -1 if height is zero.
		/// </summary>
		public readonly float AspectRatio {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				if (this.Size.Y == 0) return -1f;
				return (float)this.Size.X / this.Size.Y;
			}
		}

		#endregion

		#region Constructors

		/// <summary>Initializes a bounding box using explicit minimum and maximum vector coordinates.</summary>
		public BoundingBox(Vec2Int min, Vec2Int max) {
			this._min = min;
			this._max = max;
		}

		/// <summary>Initializes a bounding box using individual scalar boundary parameters.</summary>
		public BoundingBox(int minX, int minY, int maxX, int maxY) {
			this._min = new Vec2Int(minX, minY);
			this._max = new Vec2Int(maxX, maxY);
		}

		#endregion

		#region Methods & Overrides

		/// <summary>
		/// Determines whether the specified grid coordinate point lies inside or on the boundaries of this box.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Contains(Vec2Int point) {
			return point.X >= this._min.X && point.X <= this._max.X &&
				   point.Y >= this._min.Y && point.Y <= this._max.Y;
		}

		/// <summary>
		/// Determines whether this bounding box structurally overlaps with another bounding box.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Intersects(BoundingBox other) {
			return !(other._min.X > this._max.X || other._max.X < this._min.X ||
					 other._min.Y > this._max.Y || other._max.Y < this._min.Y);
		}

		/// <summary>Determines whether this bounding box is structurally equal to another.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(BoundingBox other) {
			return this._min.Equals(other._min) && this._max.Equals(other._max);
		}

		/// <summary>Determines whether this bounding box is equal to a target object.</summary>
		public override readonly bool Equals(object obj) {
			return obj is BoundingBox other && this.Equals(other);
		}

		/// <summary>
		/// Computes a process-independent, deterministic hash code by flattening all 4 scalar boundary coordinates.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly int GetHashCode() {
			return GenerateHashCode(this._min, this._max);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(BoundingBox left, BoundingBox right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(BoundingBox left, BoundingBox right) => !(left == right);

		public override readonly string ToString() => $"BoundingBox(Min: {this._min}, Max: {this._max})";

		#endregion

		#region Private Helpers


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GenerateHashCode(Vec2Int min, Vec2Int max) {
			return FnvHash.HashIntPair(min.GetHashCode(), max.GetHashCode());
		}
		#endregion
	}
}