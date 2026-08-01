using System;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Feature.PathFindingOld.Node {
	/// <summary>
	/// Represents a fine-grained, high-detail (Tier-2) node in the pathfinding grid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Once a <see cref="MacroGridNode"/> (Tier-1) validates that a high-level pathway exists, 
	/// individual <see cref="MicroGridNode"/> instances are queried to establish precise local step vectors around obstacles.
	/// </para>
	/// <para>
	/// Implemented as an immutable value type to entirely eliminate garbage collection allocations 
	/// and avoid Unity component wrapper overhead.
	/// </para>
	/// </remarks>
	[Serializable]
	public struct MicroGridNode : IEquatable<MicroGridNode> {
		#region Constants

		/// <summary>
		/// Base traversal cost for orthogonal (cardinal) movement. 
		/// Scaled by 10 to implement integer fixed-point path arithmetic.
		/// </summary>
		public const int DIRECT_COST = 100;

		/// <summary>
		/// Base traversal cost for diagonal movement (<c>1.414 * 10 = 14</c>).
		/// Provides an efficient integer approximation for diagonal path weight calculations.
		/// </summary>
		public const int DIAGONAL_COST = 141;

		#endregion

		#region Fields

		[SerializeField, ReadOnly] private Vec2Int _position;
		[SerializeField, ReadOnly] private bool _isStaticObstacle;
		[SerializeField, ReadOnly] private BoundingBox _parentBBox;

		#endregion

		#region Properties

		/// <summary>Gets the micro grid map position coordinate.</summary>
		public readonly Vec2Int Position {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._position;
		}

		/// <summary>Gets a value indicating whether this node represents an immovable structural obstacle.</summary>
		public readonly bool IsStaticObstacle {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._isStaticObstacle;
		}

		/// <summary>Gets the parent macro region node that bounds this micro node.</summary>
		public readonly BoundingBox ParentBBox {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._parentBBox;
		}

		#endregion

		#region Constructors

		/// <summary>Initializes a micro grid node bound to a specific parent macro node.</summary>
		public MicroGridNode(Vec2Int position, bool isStaticObstacle, BoundingBox parentBBox) {
			this._position = position;
			this._isStaticObstacle = isStaticObstacle;
			this._parentBBox = parentBBox;
		}

		/// <summary>Initializes a micro grid node using distinct coordinate integers and a parent macro node.</summary>
		public MicroGridNode(int x, int y, bool isStaticObstacle, BoundingBox parentBBox) {
			this._position = new Vec2Int(x, y);
			this._isStaticObstacle = isStaticObstacle;
			this._parentBBox = parentBBox;
		}

		#endregion

		#region Methods & Overrides
		/// <summary>
		/// Creates a cloned instance of this node with selectively overridden properties.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly MicroGridNode CopyWith(
			Vec2Int? position = null,
			bool? isStaticObstacle = null,
			BoundingBox? parentBBox = null) {
			return new MicroGridNode(
				position ?? this._position,
				isStaticObstacle ?? this._isStaticObstacle,
				parentBBox ?? this._parentBBox);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(MicroGridNode other) {
			return this._position == other._position &&
				   this._isStaticObstacle == other._isStaticObstacle &&
				   this._parentBBox == other._parentBBox;
		}
		#endregion
		#region Cost Calculation
		public static int ManhattanCost(Vec2Int a, Vec2Int b) {
			return Vec2Int.ManhattanDistanceTo(a, b) * DIRECT_COST;
		}
		public static int EuclideanCost(Vec2Int a, Vec2Int b) {
			return Vec2Int.EuclideanDistanceTo(a, b) * DIRECT_COST;
		}
		public static int OctileCost(Vec2Int a, Vec2Int b) {
			return Vec2Int.OctileDistanceTo(a, b, DIRECT_COST, DIAGONAL_COST);
		}
		#endregion




		#region  Overrides & Operators
		public override readonly bool Equals(object obj) => obj is MicroGridNode other && this.Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(MicroGridNode left, MicroGridNode right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(MicroGridNode left, MicroGridNode right) => !left.Equals(right);

		public override readonly string ToString() {
			return $"MicroGridNode(Position: {Position}, IsStaticObstacle: {IsStaticObstacle}," +
			$" ParentMacroGrid: {ParentBBox})";
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly int GetHashCode() => this.Position.GetHashCode();

		#endregion
	}
}