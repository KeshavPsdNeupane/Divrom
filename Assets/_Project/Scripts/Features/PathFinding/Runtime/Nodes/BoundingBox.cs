using System;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Feature.PathFinding.Node {

	/// <summary>
	/// Represents an axis-aligned 2D bounding box defined by minimum and maximum grid points.
	/// </summary>
	/// <remarks>
	/// A custom lightweight struct optimized with pre-calculated hash codes for high-frequency 
	/// spatial dictionary lookups, entirely avoiding the memory footprint and overhead of Unity's 
	/// native floating-point <see cref="Bounds"/> or <see cref="RectInt"/>.
	/// </remarks>
	[Serializable]
	public struct BoundingBox : IEquatable<BoundingBox> {
		#region Fields

		[SerializeField, ReadOnly] private Vec2Int _min;
		[SerializeField, ReadOnly] private Vec2Int _max;

		/// <summary>
		/// Cached hash code constructed at instantiation to ensure safe, zero-allocation dictionary operations.
		/// </summary>
		private readonly int _hashCode;

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
		/// Calculated on the fly to eliminate serialized state storage bloat across scriptable objects,
		/// and aggressively inlined to completely remove method call overhead in tight pathfinding loops.
		/// </summary>
		public readonly Vec2Int Size {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				// Aligns with Unity's native math API design: aggressively inline trivial 
				// property getters to ensure maximum execution speed in high-frequency code paths.
				return this.Max - this.Min;
			}
		}

		/// <summary>
		/// Gets the integer center coordinate of the bounding box, computed dynamically.
		/// Uses an arithmetic bitwise right-shift (<c>&gt;&gt; 1</c>) for efficient integer division 
		/// that maintains accurate floor rounding towards negative infinity across negative coordinates.
		/// </summary>
		public readonly Vec2Int Center {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				return new Vec2Int((this.Min.X + this.Max.X) >> 1, (this.Min.Y + this.Max.Y) >> 1);
			}
		}


		/// <summary>
		/// Gets the width-to-height aspect ratio of the bounding box. Returns -1 if height is zero.
		/// </summary>
		public readonly float AspectRatio {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				if (Size.Y == 0) return -1f;
				return (float)Size.X / Size.Y;
			}
		}

		#endregion

		#region Constructors

		/// <summary>Initializes a bounding box using explicit minimum and maximum vector coordinates.</summary>
		public BoundingBox(Vec2Int min, Vec2Int max) {
			this._min = min;
			this._max = max;
			this._hashCode = HashCode.Combine(this._min, this._max);
		}

		/// <summary>Initializes a bounding box using individual scalar boundary parameters.</summary>
		public BoundingBox(int minX, int minY, int maxX, int maxY) {
			this._min = new Vec2Int(minX, minY);
			this._max = new Vec2Int(maxX, maxY);
			this._hashCode = HashCode.Combine(this._min, this._max);
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(BoundingBox other) {
			return this._min.Equals(other._min) && this._max.Equals(other._max);
		}

		public readonly override bool Equals(object obj) {
			return obj is BoundingBox other && this.Equals(other);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override int GetHashCode() => this._hashCode;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(BoundingBox left, BoundingBox right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(BoundingBox left, BoundingBox right) => !(left == right);

		public readonly override string ToString() => $"BoundingBox(Min: {this._min}, Max: {this._max})";

		#endregion
	}
}