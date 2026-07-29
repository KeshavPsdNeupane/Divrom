using System;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Feature.PathFinding.Node {

	[Serializable]
	public struct MicroGridNodeSaveData {
		// Internal field names are optimized for serialization size, not readability.
		// Access data via public properties instead.
		[SerializeField, ReadOnly] private BoundingBox _pmG;
		[SerializeField, ReadOnly] private byte _isO;
		public readonly BoundingBox ParentMacroGrid {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._pmG;
		}
		public readonly bool IsStaticObstacle {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._isO != 0;
		}
		public MicroGridNodeSaveData(BoundingBox parentMacroGrid, bool isStaticObstacle) {
			this._pmG = parentMacroGrid;
			this._isO = (byte)(isStaticObstacle ? 1 : 0);
		}
	}



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
		public const int DIRECT_COST = 10;

		/// <summary>
		/// Base traversal cost for diagonal movement (<c>1.414 * 10 = 14</c>).
		/// Provides an efficient integer approximation for diagonal path weight calculations.
		/// </summary>
		public const int DIAGONAL_COST = 14;

		#endregion

		#region Fields

		[SerializeField, ReadOnly] private Vec2Int _position;
		[SerializeField, ReadOnly] private bool _isStaticObstacle;
		[SerializeField, ReadOnly] private MacroGridNode _parentMacroGrid;

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
		public readonly MacroGridNode ParentMacroGrid {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._parentMacroGrid;
		}

		#endregion

		#region Constructors

		/// <summary>Initializes a micro grid node bound to a specific parent macro node.</summary>
		public MicroGridNode(Vec2Int position, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			this._position = position;
			this._isStaticObstacle = isStaticObstacle;
			this._parentMacroGrid = parentMacroGrid;
		}

		/// <summary>Initializes a micro grid node using distinct coordinate integers and a parent macro node.</summary>
		public MicroGridNode(int x, int y, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			this._position = new Vec2Int(x, y);
			this._isStaticObstacle = isStaticObstacle;
			this._parentMacroGrid = parentMacroGrid;
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
			MacroGridNode parentMacroGrid = null) {
			return new MicroGridNode(
				position ?? this._position,
				isStaticObstacle ?? this._isStaticObstacle,
				parentMacroGrid ?? this._parentMacroGrid);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(MicroGridNode other) {
			return this._position == other._position &&
				   this._isStaticObstacle == other._isStaticObstacle &&
				   this._parentMacroGrid == other._parentMacroGrid;
		}

		public override readonly bool Equals(object obj) => obj is MicroGridNode other && this.Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(MicroGridNode left, MicroGridNode right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(MicroGridNode left, MicroGridNode right) => !left.Equals(right);

		public override readonly string ToString() {
			return $"MicroGridNode(Position: {Position}, IsStaticObstacle: {IsStaticObstacle}, ParentMacroGrid: {ParentMacroGrid?.Bound})";
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly int GetHashCode() => this.Position.GetHashCode();

		#endregion



		#region Internal Save Data Conversion
		/// <summary>
		/// Converts this <see cref="MicroGridNode"/> instance into a serializable save data representation.
		/// The key (position) is returned separately to facilitate dictionary serialization.
		/// The parent macro grid node is stored as a bounding box, and the static obstacle
		/// flag is stored as a char (0 or 1) to minimize serialized data size.
		/// </summary>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly (Vec2Int key, MicroGridNodeSaveData data) ToSaveData() {
			return (this._position, new MicroGridNodeSaveData(
				this._parentMacroGrid.Bound,
				this._isStaticObstacle
				)
			);
		}

		/// <summary>
		/// Creates a <see cref="MicroGridNode"/> instance from serialized save data.
		/// Infer the key (position) from the dictionary key in the serialized dictionary, 
		/// and use the provided save data to reconstruct the node.
		/// and infer the parent macro grid node from the bounding box stored in the save data
		/// on the parent macro grid dictionary.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="isStaticObstacle"></param>
		/// <param name="parentMacroGrid"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MicroGridNode FromSaveData(Vec2Int key, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			return new MicroGridNode(
				key,
				isStaticObstacle,
				parentMacroGrid
			);
		}

		#endregion
	}
}