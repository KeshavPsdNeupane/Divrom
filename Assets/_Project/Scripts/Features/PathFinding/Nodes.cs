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

	/// <summary>
	/// Represents a single node in the micro grid, providing a fine-grained representation of the world.
	/// <para>This is a <b>Tier-2</b> node, functioning as the high-detail counterpart to the 
	/// <see cref="MacroGridNode"/> (Tier-1). Once a <see cref="MacroGridNode"/> validates that 
	/// a path exists, the <see cref="MicroGridNode"/> system is used to calculate the precise 
	/// trajectory around obstacles and terrain features.</para>
	/// </summary>
	public sealed class MicroGridNode {
		public Vector2Int Position { get; }
		public bool IsStaticObstacle { get; set; }
		public MacroGridNode ParentMacroGrid { get; set; }
		public MicroGridNode(int x, int y, bool isStaticObstacle) {
			Position = new Vector2Int(x, y);
			IsStaticObstacle = isStaticObstacle;
		}
		public MicroGridNode(Vector2Int position, bool isStaticObstacle) {
			Position = position;
			IsStaticObstacle = isStaticObstacle;
		}
		public MicroGridNode(Vector2Int position, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			Position = position;
			IsStaticObstacle = isStaticObstacle;
			ParentMacroGrid = parentMacroGrid;
		}
		public MicroGridNode(int x, int y, bool isStaticObstacle, MacroGridNode parentMacroGrid) {
			Position = new Vector2Int(x, y);
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
		public BoundingBox Bounds { get; set; }
		public TerrainType TerrainType { get; set; }
		public MovementCapability AllowedTraversal { get; set; }
		public List<MacroConnection> Connections { get; } = new();
		public List<MicroGridNode> MicroGridsNodes { get; } = new();

		public int TotalMicroGrids => MicroGridsNodes.Count;

		public MacroGridNode(
			BoundingBox bounds, TerrainType terrainType,
			 MovementCapability allowedTraversal, List<MicroGridNode> microGridsNodes
			 , List<MacroConnection> connections) {
			this.Bounds = bounds;
			this.TerrainType = terrainType;
			this.AllowedTraversal = allowedTraversal;
			this.MicroGridsNodes = microGridsNodes;
			this.Connections = connections;
		}
		public MacroGridNode(BoundingBox bounds, TerrainType terrainType, MovementCapability allowedTraversal) {
			this.Bounds = bounds;
			this.TerrainType = terrainType;
			this.AllowedTraversal = allowedTraversal;
		}
		public MacroGridNode(BoundingBox bounds, TerrainType terrainType, MovementCapability allowedTraversal, List<MicroGridNode> microGridsNodes) {
			this.Bounds = bounds;
			this.TerrainType = terrainType;
			this.AllowedTraversal = allowedTraversal;
			this.MicroGridsNodes = microGridsNodes;
		}
		public void AddConnections(List<MacroConnection> connections) {
			this.Connections.AddRange(connections);
		}
		public void AddConnection(MacroConnection connection) {
			this.Connections.Add(connection);
		}
		public override string ToString() {
			return $"MacroGridNode(Bounds: {Bounds}, TerrainType: {TerrainType}, AllowedTraversal: {AllowedTraversal}, TotalMicroGrids: {TotalMicroGrids})";
		}

		public override int GetHashCode() {
			// Bounds are unique identifiers for MacroGridNodes.
			return this.Bounds.GetHashCode();
		}
	}

	/// <summary>
	/// Represents a connection between two MacroGridNodes, allowing traversal between 
	/// them based on the specified MovementCapability. 
	/// <br/><br/>
	/// The <see cref="IsNarrativelyAccessible"/> property acts as a master toggle for 
	/// storytelling purposes. If set to false, traversal is blocked regardless of the 
	/// <see cref="AllowedTraversal"/> capability. This allows connections to remain part 
	/// of the graph structure while being temporarily or permanently disabled for 
	/// gameplay progression.
	/// </summary>
	public sealed class MacroConnection {
		// using the bounding boxes of the connected MacroGridNodes as identifiers for
		//  the connection so there wont be a cyclic nested reference between MacroGridNode 
		// and MacroConnection, which would cause a stack overflow in the ToString() method.
		public BoundingBox From { get; set; }
		public BoundingBox To { get; set; }
		public MovementCapability AllowedTraversal { get; set; }
		public bool IsNarrativelyAccessible { get; set; } = true;

		public MacroConnection(BoundingBox from, BoundingBox to, MovementCapability allowedTraversal, bool isNarrativelyAccessible = true) {
			this.From = from;
			this.To = to;
			this.AllowedTraversal = allowedTraversal;
			this.IsNarrativelyAccessible = isNarrativelyAccessible;
		}

		public bool IsTraversable(MovementCapability capability) {
			return this.IsNarrativelyAccessible && (this.AllowedTraversal & capability)
			 == capability;
		}
		public override string ToString() {
			return $"MacroConnection(From: {From}, To: {To}, AllowedTraversal: {AllowedTraversal}, IsNarrativelyAccessible: {IsNarrativelyAccessible})";
		}

		public override int GetHashCode() {
			return HashCode.Combine(From, To, AllowedTraversal, IsNarrativelyAccessible);
		}
	}

	/// <summary>
	/// Represents a very lightweight bounding box in 2D space, defined by its minimum and maximum corners.
	/// Uses a custom value type with pre-computed hash caching instead of Unity's Bounds or RectInt 
	/// for optimal performance, memory efficiency, and safe use as high-frequency dictionary keys.
	/// </summary>
	public readonly struct BoundingBox : IEquatable<BoundingBox> {
		public Vector2Int Min { get; }
		public Vector2Int Max { get; }
		public Vector2Int Size => this.Max - this.Min;
		public float AspectRatio {
			get {
				// Avoid division by zero, return a sentinel value
				if (Size.y == 0) return -1f;
				// the float case is impilicitly casted to float, so the division is done
				// in floating point arithmetic
				return (float)Size.x / Size.y;
			}
		}

		/// <summary>
		/// Pre-computed hash code for the bounding box, calculated during construction.
		/// Can precompile the hash code because the struct is readonly immutable, ensuring that 
		/// the hash code remains consistent throughout its lifetime.
		/// </summary>
		private readonly int _hashCode;

		public BoundingBox(Vector2Int min, Vector2Int max) {
			this.Min = min;
			this.Max = max;
			this._hashCode = HashCode.Combine(Min, Max);
		}
		public BoundingBox(int minX, int minY, int maxX, int maxY) {
			this.Min = new Vector2Int(minX, minY);
			this.Max = new Vector2Int(maxX, maxY);
			this._hashCode = HashCode.Combine(Min, Max);
		}

		public readonly bool Contains(Vector2Int point) {
			return point.x >= Min.x && point.x <= Max.x &&
				   point.y >= Min.y && point.y <= Max.y;
		}

		public readonly bool Intersects(BoundingBox other) {
			return !(other.Min.x > Max.x || other.Max.x < Min.x ||
					 other.Min.y > Max.y || other.Max.y < Min.y);
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
