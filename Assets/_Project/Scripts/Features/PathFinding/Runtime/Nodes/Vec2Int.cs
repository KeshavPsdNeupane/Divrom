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
	/// Implemented as an ultra-lightweight 8-byte struct (<c>sizeof(int) * 2</c>) optimized for high-frequency 
	/// grid lookups and A* pathfinding algorithms. 
	/// </para>
	/// <para>
	/// Features a deterministic, process-independent prime hash calculation (multiplier <c>397</c>) that eliminates 
	/// field serialization overhead and guarantees zero domain reload desync when stored in ScriptableObject dictionaries.
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
			get => this._x * this._x + this._y * this._y;
		}

		/// <summary>Gets the Euclidean magnitude of the vector.</summary>
		public readonly float Magnitude {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Mathf.Sqrt(this.SquareMagnitude);
		}

		/// <summary>
		/// Calculates the Manhattan distance between two 2D integer points.
		/// </summary>
		/// <remarks>
		/// Operates directly on <see cref="Vec2Int"/> coordinates rather than <see cref="BoundingBox"/> instances 
		/// to maintain a clean separation of concerns, allowing distance evaluations on arbitrary coordinate pairs.
		/// </remarks>
		/// <param name="firstCenter">The first coordinate point.</param>
		/// <param name="secondCenter">The second coordinate point.</param>
		/// <returns>The calculated integer Manhattan distance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ManhattanDistanceTo(Vec2Int firstCenter, Vec2Int secondCenter) {
			return Mathf.Abs(firstCenter.X - secondCenter.X) + Mathf.Abs(firstCenter.Y - secondCenter.Y);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float EuclideanDistanceTo(Vec2Int firstCenter, Vec2Int secondCenter) {
			int x = firstCenter.X - secondCenter.X;
			int y = firstCenter.Y - secondCenter.Y;
			return Mathf.Sqrt(x * x + y * y);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int OctileDistanceTo(Vec2Int first, Vec2Int second) {
			int dx = Mathf.Abs(first.X - second.X);
			int dy = Mathf.Abs(first.Y - second.Y);

			int min = dx < dy ? dx : dy;
			int max = dx > dy ? dx : dy;

			return max + Mathf.FloorToInt(0.41421356f * min);
		}
		#endregion

		#region Constructors

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> from a Unity <see cref="Vector2Int"/>.</summary>
		public Vec2Int(Vector2Int vector) {
			this._x = vector.x;
			this._y = vector.y;
		}

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> copying an existing instance.</summary>
		public Vec2Int(Vec2Int vector) {
			this._x = vector._x;
			this._y = vector._y;
		}

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> with explicit X and Y coordinate values.</summary>
		public Vec2Int(int x, int y) {
			this._x = x;
			this._y = y;
		}
		public Vec2Int(Vector3 vector) {
			this._x = Mathf.FloorToInt(vector.x);
			this._y = Mathf.FloorToInt(vector.y);
		}

		/// <summary>Initializes a new instance of <see cref="Vec2Int"/> by truncating floating-point 
		/// coordinates to integers. Also takes the floor of negative values (e.g., -1.5 becomes -2).</summary>
		/// <remarks>
		/// This constructor is useful for converting world-space positions to grid coordinates,
		/// ensuring that any point within a grid cell maps to the correct integer index.
		/// </remarks>
		/// </summary>
		public Vec2Int(float x, float y) {
			this._x = Mathf.FloorToInt(x);
			this._y = Mathf.FloorToInt(y);
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

		//	public static implicit operator UnityEngine.Vector3(Vec2Int v) => new(v.X, v.Y, 0f);
		/// <summary>
		/// Converts this cell index to the world-space position of its center point.
		/// </summary>
		/// <remarks>
		/// A cell index is a half-open interval — index <c>x</c> spans the world-space range
		/// <c>[x, x + 1)</c> under this project's <see cref="Mathf.FloorToInt"/> world-to-grid
		/// convention. Its true center therefore sits at <c>x + 0.5</c>, not <c>x</c>.
		/// <para>
		/// Prefer this over the implicit <see cref="Vector3"/> cast whenever the result
		/// will be rendered, positioned, or compared against transform positions — the raw cast lands
		/// on the cell's corner and reproduces the grid/world half-cell offset bug.
		/// </para>
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Vector3 ToWorldCenter() => new(this._x + 0.5f, this._y + 0.5f, 0f);

		#endregion



		public override readonly string ToString() => $"({X}, {Y})";

		/// <summary>
		/// Computes a process-independent, deterministic hash code for fast dictionary lookups.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly int GetHashCode() => GenerateHashCode(this._x, this._y);
		#region Private Helpers

		/// <summary>
		/// Generates a deterministic hash code using prime multiplication.
		/// Guaranteed to yield identical hash values across Unity Domain Reloads and editor restarts.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GenerateHashCode(int x, int y) {
			unchecked {
				int hash = 17;
				hash = (hash * 397) ^ x;
				hash = (hash * 397) ^ y;
				return hash;
			}
		}

		#endregion
	}
}