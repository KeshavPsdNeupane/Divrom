using System;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Feature.PathFinding.Node {

	/// <summary>
	/// Represents an immutable 2D integer coordinate in the pathfinding grid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Implemented as a high-performance struct optimized for spatial lookups. It features 
	/// pre-computed hash code caching to minimize dictionary allocation and lookup overhead,
	/// alongside explicit and implicit conversions with Unity's native <see cref="Vector2Int"/>.
	/// </para>
	/// </remarks>
	[Serializable]
	public struct Vec2Int : IEquatable<Vec2Int> {
		#region Static Fields

		/// <summary>Zero coordinate vector (0, 0).</summary>
		public static readonly Vec2Int Zero = new(0, 0);
		/// <summary>Unit scale vector (1, 1).</summary>
		public static readonly Vec2Int One = new(1, 1);
		/// <summary>Upward directional vector (0, 1).</summary>
		public static readonly Vec2Int Up = new(0, 1);
		/// <summary>Downward directional vector (0, -1).</summary>
		public static readonly Vec2Int Down = new(0, -1);
		/// <summary>Leftward directional vector (-1, 0).</summary>
		public static readonly Vec2Int Left = new(-1, 0);
		/// <summary>Rightward directional vector (1, 0).</summary>
		public static readonly Vec2Int Right = new(1, 0);

		#endregion

		#region Fields

		[SerializeField, ReadOnly] private int _x;
		[SerializeField, ReadOnly] private int _y;

		/// <summary>
		/// Cached hash code generated during construction for fast, zero-allocation dictionary lookups.
		/// </summary>
		private readonly int _hashCode;

		#endregion

		#region Properties

		/// <summary>Gets the X coordinate component.</summary>
		public readonly int X {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._x;
		}

		/// <summary>Gets the Y coordinate component.</summary>
		public readonly int Y {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._y;
		}

		/// <summary>Gets the squared Euclidean magnitude of the vector (avoids square root overhead).</summary>
		public readonly int SquareMagnitude {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				return this._x * this._x + this._y * this._y;
			}
		}

		#endregion

		#region Constructors

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> from a Unity <see cref="Vector2Int"/>.</summary>
		public Vec2Int(Vector2Int vector) {
			this._x = vector.x;
			this._y = vector.y;
			this._hashCode = HashCode.Combine(this._x, this._y);
		}

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> copying an existing instance.</summary>
		public Vec2Int(Vec2Int vector) {
			this._x = vector._x;
			this._y = vector._y;
			this._hashCode = HashCode.Combine(this._x, this._y);
		}

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> with explicit X and Y coordinate values.</summary>
		public Vec2Int(int x, int y) {
			this._x = x;
			this._y = y;
			this._hashCode = HashCode.Combine(this._x, this._y);
		}

		#endregion

		#region Operators & Overrides

		/// <summary>Determines whether this vector is structurally equal to another.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(Vec2Int other) {
			return this.X == other.X && this.Y == other.Y;
		}

		/// <summary>Determines whether this vector is equal to a target object.</summary>
		public override readonly bool Equals(object obj) {
			return obj is Vec2Int other && this.Equals(other);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Vec2Int left, Vec2Int right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Vec2Int left, Vec2Int right) => !left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vec2Int operator +(Vec2Int a, Vec2Int b) => new(a.X + b.X, a.Y + b.Y);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vec2Int operator -(Vec2Int a, Vec2Int b) => new(a.X - b.X, a.Y - b.Y);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vec2Int operator *(Vec2Int a, int scalar) => new(a.X * scalar, a.Y * scalar);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vec2Int operator /(Vec2Int a, int scalar) => new(a.X / scalar, a.Y / scalar);

		public static implicit operator Vector2Int(Vec2Int v) => new(v.X, v.Y);
		public static implicit operator Vec2Int(Vector2Int v) => new(v.x, v.y);

		public override readonly string ToString() => $"({X}, {Y})";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly int GetHashCode() => this._hashCode;

		#endregion
	}
}